using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Abstractions.Workflow;
using SiteKeeper.Master.Model.InternalData;
using SiteKeeper.Master.Workflow.DTOs;
using SiteKeeper.Shared.DTOs.MasterSlave;
using SiteKeeper.Shared.Enums;
using SiteKeeper.Shared.Enums.Extensions;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using SiteKeeper.Master.Services;
using SiteKeeper.Shared.DTOs.AgentHub;
using System.Threading.Channels;
using NLog;

namespace SiteKeeper.Master.Workflow.StageHandlers
{
    /// <summary>
    /// A specialized, stateful stage handler that executes and manages operations distributed across multiple slave nodes.
    /// This class is registered as a singleton and is responsible for the entire lifecycle of a multi-node operation
    /// within a master workflow, including readiness checks, task dispatch, progress tracking, cancellation, and health monitoring.
    /// </summary>
    public class MultiNodeOperationStageHandler : IStageHandler<MultiNodeOperationInput, MultiNodeOperationResult>
    {
        #region Private State Management Class
        /// <summary>
        /// A private nested class to hold the complete state for each active multi-node operation
        /// being managed by this handler. This allows the singleton handler to manage multiple operations concurrently if needed in the future,
        /// although the current MasterActionCoordinator enforces a single workflow at a time.
        /// </summary>
        private class ActiveOperationContext
        {
            /// <summary>The core Operation object containing all tasks and parameters.</summary>
            public Operation Operation { get; }
            /// <summary>A reference to the parent MasterAction's context for logging and journaling.</summary>
            public MasterActionContext ParentMasterActionContext { get; }
            /// <summary>The TaskCompletionSource that the parent workflow awaits. It is completed only when the operation finishes.</summary>
            public TaskCompletionSource<MultiNodeOperationResult> OperationCompletionSource { get; }
            /// <summary>A thread-safe collection to track which nodes have confirmed their logs are flushed.</summary>
            public ConcurrentBag<string> ConfirmedLogFlushNodes { get; }
            /// <summary>The TaskCompletionSource for the log flush phase. It is completed when all nodes have confirmed.</summary>
            public TaskCompletionSource LogFlushCompletionSource { get; }
            /// <summary>The progress reporter for this specific stage.</summary>
            public IProgress<StageProgress> ProgressReporter { get; }
			
            // for hanling logs arriving after the operation is completed
			public Channel<SlaveTaskLogEntry> LogChannel { get; }

            //// This will signal that the master-side journal writes are complete.
            //public TaskCompletionSource AllJournalWritesCompletedSource { get; }


            public ActiveOperationContext(Operation operation, IProgress<StageProgress> progress, MasterActionContext parentMasterActionContext)
            {
                Operation = operation;
                ProgressReporter = progress;
                ParentMasterActionContext = parentMasterActionContext;
                OperationCompletionSource = new TaskCompletionSource<MultiNodeOperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                ConfirmedLogFlushNodes = new ConcurrentBag<string>();
                LogFlushCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                // Initialize an unbounded channel for this operation's logs.
                LogChannel = Channel.CreateUnbounded<SlaveTaskLogEntry>(new UnboundedChannelOptions { SingleReader = true });
            }
        }
        #endregion

        private readonly ILogger<MultiNodeOperationStageHandler> _logger;
        private readonly IAgentConnectionManagerService _agentConnectionManager;
        private readonly IJournalService _journalService;
        private readonly INodeHealthMonitorService _nodeHealthMonitorService;
        private readonly ConcurrentDictionary<string, ActiveOperationContext> _activeOperations = new();
        private readonly ConcurrentDictionary<string, string> _taskIdToOperationIdMap = new();

        public MultiNodeOperationStageHandler(
            ILogger<MultiNodeOperationStageHandler> logger,
            IAgentConnectionManagerService agentConnectionManager,
            IJournalService journalService,
            INodeHealthMonitorService nodeHealthMonitorService)
        {
            _logger = logger;
            _agentConnectionManager = agentConnectionManager;
            _journalService = journalService;
            _nodeHealthMonitorService = nodeHealthMonitorService;
        }

        /// <summary>
        /// Executes a multi-node operation as a single stage within a Master Action. This is the main entry point for the handler.
        /// </summary>
        /// <param name="input">The DTO containing the pre-built Operation object to execute.</param>
        /// <param name="masterActionContext">The context of the parent Master Action.</param>
        /// <param name="progress">The progress reporter for this stage.</param>
        /// <param name="cancellationToken">The cancellation token for the parent Master Action.</param>
        /// <returns>A MultiNodeOperationResult containing the final state of the operation.</returns>
        public async Task<MultiNodeOperationResult> ExecuteAsync(
            MultiNodeOperationInput input,
            MasterActionContext masterActionContext,
            IProgress<StageProgress> progress,
            CancellationToken cancellationToken)
        {
            var operation = input.OperationToExecute;
            if (operation == null || !operation.NodeTasks.Any())
            {
                masterActionContext.LogInfo("MultiNodeOperationStageHandler received no tasks to execute. Stage succeeded immediately.");
                return new MultiNodeOperationResult { IsSuccess = true, FinalOperationState = operation ?? new Operation("empty", OperationType.NoOp) };
            }

            var opContext = new ActiveOperationContext(operation, progress, masterActionContext);

            // use the UNIQUE internal operation ID as the key
            if (!_activeOperations.TryAdd(operation.Id, opContext))
            {
                throw new InvalidOperationException($"An operation with the internal ID {operation.Id} is already running.");
            }
        
            // Populate the TaskId map
            foreach (var task in operation.NodeTasks)
            {
                _taskIdToOperationIdMap[task.TaskId] = operation.Id;
            }
              
            _logger.LogInformation("Starting multi-node operation stage: {OperationType} for MasterAction {MasterActionId}", operation.Type, masterActionContext.MasterActionId);

            // The parent MasterActionId is already set by the coordinator. Here, we add the
            // more granular, stage-specific Operation.Id to the ambient context.
            using (MappedDiagnosticsLogicalContext.SetScoped("SK-OperationId", operation.Id))
            {
                // Start a background task that will consume logs from the channel for this operation's lifetime.
                var logConsumerTask = ConsumeLogChannelAsync(opContext);

                var healthMonitoringCts = new CancellationTokenSource();
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, healthMonitoringCts.Token);
                _ = MonitorNodeHealthForOperation(opContext, linkedCts.Token);

                using (cancellationToken.Register(() => opContext.OperationCompletionSource.TrySetCanceled()))
                {
                    try
                    {
                        await StartReadinessCheckAndDispatchSequenceAsync(operation);
                        var operationResult = await opContext.OperationCompletionSource.Task;
                        await FlushAllNodeLogsAsync(operationResult, masterActionContext);
                        return operationResult;
                    }
                    catch (TaskCanceledException)
                    {
                         _logger.LogWarning("Multi-node operation stage {OperationId} was cancelled by master request. Handling cancellation...", operation.Id);
                        operation.OverallStatus = OperationOverallStatus.Cancelling;

                        var tasksToCancel = operation.NodeTasks.Where(t => !t.Status.IsTerminal()).ToList();

                        foreach (var task in tasksToCancel)
                        {
                            // Check the real-time status of the node for this task.
                            var nodeState = await _nodeHealthMonitorService.GetNodeCachedStateAsync(task.NodeName);
        
                            // If the node is already offline, we can immediately mark its task as Cancelled.
                            // There is no agent to send a command to or wait for a response from.
                            if (nodeState?.ConnectivityStatus is AgentConnectivityStatus.Offline or AgentConnectivityStatus.Unreachable)
                            {
                                _logger.LogInformation("Task {TaskId} on offline node {NodeName} is being marked as Cancelled immediately.", task.TaskId, task.NodeName);
                                task.Status = NodeTaskStatus.Cancelled;
                                task.StatusMessage = "Task cancelled; the target node was offline.";
                                task.EndTime = DateTime.UtcNow;
                            }
                            else
                            {
                                // If the node is online, request a graceful cancellation and set its status to Cancelling.
                                _logger.LogInformation("Requesting cancellation for task {TaskId} on online node {NodeName}.", task.TaskId, task.NodeName);
                                task.Status = NodeTaskStatus.Cancelling;
                                await _agentConnectionManager.SendCancelTaskAsync(task.NodeName, new CancelTaskOnAgentRequest { OperationId = operation.Id, TaskId = task.TaskId, Reason = "Operation cancelled by master." });
                            }
                        }

                        // Now, wait for all tasks that were marked 'Cancelling' to either be confirmed as cancelled by the slave
                        // or for their nodes to go offline. This method has a built-in timeout.
                        await MonitorCancellationCompletion(opContext, TimeSpan.FromSeconds(15));

                        // After the monitor returns, explicitly finalize the status of any tasks
                        // that were in the 'Cancelling' state, especially if the node went offline.
                        foreach (var task in operation.NodeTasks.Where(t => t.Status == NodeTaskStatus.Cancelling))
                        {
                            _logger.LogInformation("Finalizing status for task {TaskId} from 'Cancelling' to 'Cancelled' after monitor completion.", task.TaskId);
                            task.Status = NodeTaskStatus.Cancelled;
                            task.StatusMessage = "Task cancellation confirmed as node is offline or did not respond.";
                            task.EndTime = DateTime.UtcNow;
                        }

                        // After waiting, finalize the operation state.
                        operation.OverallStatus = OperationOverallStatus.Cancelled;
                        operation.EndTime = DateTime.UtcNow;

                        var cancelledResult = new MultiNodeOperationResult { IsSuccess = false, FinalOperationState = operation };
                        await FlushAllNodeLogsAsync(cancelledResult, masterActionContext);
    
                        // Explicitly set the TaskCompletionSource with the final result to unblock the awaiter.
                        opContext.OperationCompletionSource.TrySetResult(cancelledResult);

                        // Since we are handling the exception and completing the source, we return the result instead of letting the exception propagate.
                        return cancelledResult;
                    }
                    finally
                    {
                        healthMonitoringCts.Cancel();
                        _activeOperations.TryRemove(operation.Id, out _);

                        foreach (var task in operation.NodeTasks)
                        {
                            _taskIdToOperationIdMap.TryRemove(task.TaskId, out _);
                        }

                        await logConsumerTask;
 

                        _logger.LogInformation("Multi-node operation stage {OperationId} finished and cleaned up.", operation.Id);
                    }
                }
            }
        }
        
        /// <summary>
        /// A background task that periodically checks the health of nodes involved in an operation.
        /// If a node disconnects, this method will fail its associated task.
        /// </summary>
        private async Task MonitorNodeHealthForOperation(ActiveOperationContext opContext, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);

                    var activeTasks = opContext.Operation.NodeTasks.Where(t => !t.Status.IsTerminal()).ToList();
                    if (!activeTasks.Any()) return;

                    foreach (var task in activeTasks)
                    {
                        var nodeState = await _nodeHealthMonitorService.GetNodeCachedStateAsync(task.NodeName);
                        if (nodeState?.ConnectivityStatus is AgentConnectivityStatus.Offline or AgentConnectivityStatus.Unreachable)
                        {
                            _logger.LogError("Node '{NodeName}' disconnected during active task {TaskId}. Failing the task.", task.NodeName, task.TaskId);
                            task.Status = NodeTaskStatus.NodeOfflineDuringTask;
                            task.StatusMessage = $"Node went offline or became unreachable during task execution. Last known status: {nodeState.ConnectivityStatus}.";
                            task.EndTime = DateTime.UtcNow;
                            await RecalculateOperationStatusAsync(opContext);
                        }
                    }
                }
                catch (TaskCanceledException) { return; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in node health monitoring loop for operation {OperationId}.", opContext.Operation.Id);
                }
            }
        }

        /// <summary>
        /// Waits for cancellation to be acknowledged by all reachable nodes. If a node is disconnected, it's considered "accounted for."
        /// </summary>
        private async Task MonitorCancellationCompletion(ActiveOperationContext opContext, TimeSpan timeout)
        {
            var timeoutCts = new CancellationTokenSource(timeout);
            _logger.LogInformation("Monitoring for cancellation confirmation from nodes for OpId: {OperationId}", opContext.Operation.Id);

            try
            {
                while (!timeoutCts.IsCancellationRequested)
                {
                    bool allAccountedFor = true;
                    var tasksStillCancelling = opContext.Operation.NodeTasks.Where(t => t.Status == NodeTaskStatus.Cancelling).ToList();
                    
                    if (!tasksStillCancelling.Any())
                    {
                        _logger.LogInformation("All nodes have confirmed terminal status for OpId {OperationId}.", opContext.Operation.Id);
                        return; // All tasks have moved past the "Cancelling" state
                    }

                    foreach (var task in tasksStillCancelling)
                    {
                        var nodeState = await _nodeHealthMonitorService.GetNodeCachedStateAsync(task.NodeName);
                        if (nodeState?.ConnectivityStatus != AgentConnectivityStatus.Offline && nodeState?.ConnectivityStatus != AgentConnectivityStatus.Unreachable)
                        {
                            allAccountedFor = false; // Still waiting for this reachable node.
                            break;
                        }
                    }

                    if (allAccountedFor)
                    {
                        _logger.LogInformation("All reachable nodes have confirmed terminal status after cancellation request for OpId: {OperationId}", opContext.Operation.Id);
                        return;
                    }
                    await Task.Delay(500, timeoutCts.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // This means the timeout was reached
            }

            _logger.LogWarning("Timed out waiting for all nodes to confirm cancellation for OpId: {OperationId}. Forcibly marking remaining tasks as Cancelled.", opContext.Operation.Id);
            foreach (var task in opContext.Operation.NodeTasks.Where(t => !t.Status.IsTerminal()))
            {
                task.Status = NodeTaskStatus.Cancelled;
                task.StatusMessage = "Node did not confirm cancellation within timeout; master marked as cancelled.";
                task.EndTime = DateTime.UtcNow;
            }
        }
        
        /// <summary>
        /// Sends a "Prepare for Task" instruction to all nodes involved in the operation.
        /// </summary>
        private async Task StartReadinessCheckAndDispatchSequenceAsync(Operation operation)
        {
            operation.StartTime = DateTime.UtcNow;
            operation.OverallStatus = OperationOverallStatus.AwaitingNodeReadiness;
            
            foreach (var task in operation.NodeTasks)
            {
                string? prepParamsJson = null;
                if (task.TaskType == SlaveTaskType.TestOrchestration)
                {
                    prepParamsJson = JsonSerializer.Serialize(task.TaskPayload);
                }

                var prepareInstruction = new PrepareForTaskInstruction
                {
                    OperationId = task.OperationId,
                    TaskId = task.TaskId,
                    ExpectedTaskType = task.TaskType,
                    PreparationParametersJson = prepParamsJson
                };
                await _agentConnectionManager.SendPrepareForTaskInstructionAsync(task.NodeName, prepareInstruction);
                task.Status = NodeTaskStatus.ReadinessCheckSent;
            }

            _ = MonitorReadinessTimeout(operation.Id, TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// Processes a readiness report from a slave, received via the AgentHub.
        /// </summary>
        public async Task ProcessSlaveTaskReadinessAsync(SlaveTaskReadinessReport readinessReport)
        {
            if (!_taskIdToOperationIdMap.TryGetValue(readinessReport.TaskId, out var internalOpId) ||
                !_activeOperations.TryGetValue(internalOpId, out var opContext))
            {
                _logger.LogWarning("Received readiness report for an unknown or completed task/operation: TaskId {TaskId}", readinessReport.TaskId);
                return;
            }

            var task = opContext.Operation.NodeTasks.FirstOrDefault(t => t.TaskId == readinessReport.TaskId);
            if (task == null) return;

            if (readinessReport.IsReady)
            {
                task.Status = NodeTaskStatus.ReadyToExecute;
                task.StatusMessage = "Agent reported ready for task execution.";
                _logger.LogInformation("Node {NodeName} is ready for task {TaskId}.", task.NodeName, task.TaskId);

                var slaveTaskInstruction = new SlaveTaskInstruction
                {
                    OperationId = task.OperationId,
                    TaskId = task.TaskId,
                    TaskType = task.TaskType,
                    ParametersJson = JsonSerializer.Serialize(task.TaskPayload),
                    TimeoutSeconds = 30
                };

                await _agentConnectionManager.SendSlaveTaskAsync(task.NodeName, slaveTaskInstruction);
                task.Status = NodeTaskStatus.TaskDispatched;

                // Start monitoring for execution timeout for this specific task.
                var executionTimeout = TimeSpan.FromSeconds(slaveTaskInstruction.TimeoutSeconds ?? 30);
                _ = MonitorExecutionTimeoutAsync(opContext, task.TaskId, executionTimeout);
            }
            else
            {
                task.Status = NodeTaskStatus.NotReadyForTask;
                task.StatusMessage = $"Agent reported not ready: {readinessReport.ReasonIfNotReady}";
                task.EndTime = DateTime.UtcNow;
                _logger.LogWarning("Node {NodeName} is not ready for task {TaskId}. Reason: {Reason}", task.NodeName, task.TaskId, readinessReport.ReasonIfNotReady);
            }
            await RecalculateOperationStatusAsync(opContext);
        }

        /// <summary>
        /// Processes a progress/status update from a slave, received via the AgentHub.
        /// </summary>
        public async Task ProcessTaskStatusUpdateAsync(SlaveTaskProgressUpdate statusUpdate)
        {
            // Find the context for the operation this update belongs to.
            if (!_activeOperations.TryGetValue(statusUpdate.OperationId, out var opContext))
            {
                _logger.LogWarning("Received task status update for an unknown or completed operation: {OperationId}", statusUpdate.OperationId);
                return;
            }

            var nodeTask = opContext.Operation.NodeTasks.FirstOrDefault(t => t.TaskId == statusUpdate.TaskId);
            if (nodeTask == null)
            {
                _logger.LogWarning("Received status update for an unknown task: {TaskId} in operation {OperationId}", statusUpdate.TaskId, statusUpdate.OperationId);
                return;
            }

            // Update the NodeTask state with the new information from the slave.
            if (Enum.TryParse<NodeTaskStatus>(statusUpdate.Status, true, out var parsedStatus))
            {
                nodeTask.Status = parsedStatus;
            }
            nodeTask.ProgressPercent = statusUpdate.ProgressPercent ?? nodeTask.ProgressPercent;
            nodeTask.StatusMessage = statusUpdate.Message;
            nodeTask.LastUpdateTime = statusUpdate.TimestampUtc;

            if (nodeTask.Status.IsTerminal())
            {
                nodeTask.EndTime = statusUpdate.TimestampUtc;
                if (!string.IsNullOrEmpty(statusUpdate.ResultJson))
                {
                    try {
                        nodeTask.ResultPayload = JsonSerializer.Deserialize<Dictionary<string, object>>(statusUpdate.ResultJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    } catch(Exception ex) {
                         _logger.LogError(ex, "Failed to deserialize ResultJson for task {TaskId}.", nodeTask.TaskId);
                    }
                }
                await _journalService.RecordNodeTaskResultAsync(opContext.ParentMasterActionContext, nodeTask);
            }
            await RecalculateOperationStatusAsync(opContext);
        }
        
        /// <summary>
        /// Recalculates the overall status of an operation based on the current statuses of its individual node tasks.
        /// </summary>
        private async Task RecalculateOperationStatusAsync(ActiveOperationContext opContext)
        {
            var operation = opContext.Operation;
            var tasks = operation.NodeTasks;

            if (!tasks.Any()) {
                operation.OverallStatus = OperationOverallStatus.Succeeded;
                operation.ProgressPercent = 100;
            } else {
                int succeeded = tasks.Count(t => t.Status == NodeTaskStatus.Succeeded);
                int failedOrCancelled = tasks.Count(t => t.Status.IsTerminal() && t.Status != NodeTaskStatus.Succeeded);
                
                operation.ProgressPercent = (int)tasks.Average(t => t.ProgressPercent);
                operation.StatusMessage = $"In Progress: {tasks.Count(t => !t.Status.IsTerminal())}, Succeeded: {succeeded}, Failed/Cancelled: {failedOrCancelled}";

                if (tasks.All(t => t.Status.IsTerminal()))
                {
                    if (tasks.Any(t => t.Status == NodeTaskStatus.Cancelled || t.Status == NodeTaskStatus.Cancelling))
                        operation.OverallStatus = OperationOverallStatus.Cancelled;
                    else if (tasks.Any(t => t.Status != NodeTaskStatus.Succeeded))
                        operation.OverallStatus = OperationOverallStatus.Failed;
                    else
                        operation.OverallStatus = OperationOverallStatus.Succeeded;
                    
                    operation.EndTime = DateTime.UtcNow;
                }
            }
            
            opContext.ProgressReporter.Report(new StageProgress {
                ProgressPercent = operation.ProgressPercent,
                StatusMessage = operation.StatusMessage
            });
            
            if (operation.OverallStatus.IsCompleted())
            {
                var result = new MultiNodeOperationResult {
                    IsSuccess = operation.OverallStatus == OperationOverallStatus.Succeeded,
                    FinalOperationState = operation
                };
                opContext.OperationCompletionSource.TrySetResult(result);
            }
        }
        
        /// <summary>
        /// Monitors for readiness check timeouts.
        /// </summary>
        private async Task MonitorReadinessTimeout(string operationId, TimeSpan timeout)
        {
            await Task.Delay(timeout);

            // The key for _activeOperations is the internal operation ID again, so this is now correct.
            if (_activeOperations.TryGetValue(operationId, out var opContext) && opContext.Operation.OverallStatus == OperationOverallStatus.AwaitingNodeReadiness)
            {
                _logger.LogWarning("Readiness check timed out for operation {OperationId}", operationId);
                bool changed = false;
                foreach (var task in opContext.Operation.NodeTasks.Where(t => t.Status == NodeTaskStatus.ReadinessCheckSent))
                {
                    task.Status = NodeTaskStatus.ReadinessCheckTimedOut;
                    task.StatusMessage = "Agent did not respond to readiness check within the timeout period.";
                    task.EndTime = DateTime.UtcNow;
                    changed = true;
                }
                if(changed) await RecalculateOperationStatusAsync(opContext);
            }
        }
        
        // ADD this new consumer method
        private async Task ConsumeLogChannelAsync(ActiveOperationContext opContext)
        {
            try
            {
                // This will efficiently loop and process logs as they are added to the channel.
                await foreach (var logEntry in opContext.LogChannel.Reader.ReadAllAsync())
                {
                    try
                    {
                        // The logEntry.OperationId is the internal ID for the stage's operation,
                        // but the JournalService tracks journals by the parent MasterActionId.
                        await _journalService.AppendToStageLogAsync(opContext.ParentMasterActionContext.MasterActionId, logEntry);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error writing slave log from channel to journal for OpId {OperationId}", logEntry.OperationId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Log channel consumer for OpId {OperationId} failed unexpectedly.", opContext.Operation.Id);
            }
        }

        /// <summary>
        /// Commands all nodes to flush their logs for a completed operation and waits for their confirmation.
        /// </summary>
        private async Task FlushAllNodeLogsAsync(MultiNodeOperationResult operationResult, MasterActionContext masterActionContext)
        {
            var allParticipatingNodes = operationResult.FinalOperationState.NodeTasks.Select(t => t.NodeName).Distinct().ToList();
            if (!allParticipatingNodes.Any()) return;
            if (!_activeOperations.TryGetValue(operationResult.FinalOperationState.Id, out var opContext)) return;

            // 1. Identify which participating nodes are actually online.
            var onlineNodes = new List<string>();
            foreach (var nodeName in allParticipatingNodes)
            {
                // Use the health monitor to get the current cached status.
                var nodeState = await _nodeHealthMonitorService.GetNodeCachedStateAsync(nodeName);
                if (nodeState?.ConnectivityStatus == AgentConnectivityStatus.Online)
                {
                    onlineNodes.Add(nodeName);
                }
            }

            // 2. Only request and wait for flushes from online nodes.
            if (onlineNodes.Any())
            {
                masterActionContext.LogInfo($"Operation stage complete. Requesting log flush from {onlineNodes.Count} online node(s)...");

                foreach (var nodeName in onlineNodes)
                {
                    await _agentConnectionManager.RequestLogFlushForTask(nodeName, operationResult.FinalOperationState.Id);
                }

                // 3. Implement a smarter wait loop that only considers the online nodes.
                var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try
                {
                    while (opContext.ConfirmedLogFlushNodes.Count < onlineNodes.Count && !timeoutCts.IsCancellationRequested)
                    {
                        await Task.Delay(250, timeoutCts.Token);
                    }

                    if (timeoutCts.IsCancellationRequested)
                    {
                         masterActionContext.Logger.LogWarning("Timed out waiting for all online nodes to confirm log flush.");
                    }
                    else
                    {
                        masterActionContext.LogInfo("All online nodes have confirmed their logs have been sent.");
                    }
                }
                catch (TaskCanceledException)
                {
                     masterActionContext.Logger.LogWarning("Timed out waiting for all online nodes to confirm log flush.");
                }
                finally
                {
                    timeoutCts.Dispose();
                }
            }
            else
            {
                masterActionContext.LogInfo("No online nodes to flush logs from. Skipping wait.");
            }
  
            // Mark the channel as "complete for writing". No new items can be added.
            opContext.LogChannel.Writer.Complete();

            //  Wait for the channel reader's Completion task. This task only finishes
            //  when the channel is marked complete AND all items have been read and processed.
            await opContext.LogChannel.Reader.Completion;

            masterActionContext.LogInfo("All received logs have been successfully written to the journal.");
        }

        public Task JournalSlaveLogAsync(SlaveTaskLogEntry logEntry)
        {
            // This method receives a log entry that may not have a TaskId if it's a general operation log.
            // However, for logs from a slave task, it SHOULD have a TaskId.
            // The lookup must be resilient.
            string? internalOpId = null;
            if (!string.IsNullOrEmpty(logEntry.TaskId))
            {
                _taskIdToOperationIdMap.TryGetValue(logEntry.TaskId, out internalOpId);
            }

            _logger.LogDebug("STAGE-HANDLER: Journaling slave log for TaskId '{TaskId}'. Mapped to InternalOpId: '{InternalOpId}'", logEntry.TaskId, internalOpId ?? "Not Found");

            if (internalOpId == null || !_activeOperations.TryGetValue(internalOpId, out var opContext))
            {
                 _logger.LogWarning("Could not journal slave log: No active operation context found for MasterActionId {OperationId} / TaskId {TaskId}", 
                    logEntry.OperationId, logEntry.TaskId);
                return Task.CompletedTask;
            }
 
            _logger.LogDebug("STAGE-HANDLER: Found active context for OpId {OpId}. Enqueuing log message to internal channel.", internalOpId);

            // Instead of writing directly, just enqueue the log entry. This is a fast, non-blocking operation.
            if (!opContext.LogChannel.Writer.TryWrite(logEntry))
            {
                _logger.LogWarning("Could not write log to channel for OpId {opId}; channel may be closed.", logEntry.OperationId);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Confirms that a slave has finished flushing its logs, received via the AgentHub.
        /// </summary>
        public void ConfirmLogFlush(string operationId, string nodeName)
        {
            // lookup using the internal Operation.Id as the key.
            // This is the "translation" from the specific operationId back to the internal context object.
            if (_activeOperations.TryGetValue(operationId, out var opContext))
            {
                opContext.ConfirmedLogFlushNodes.Add(nodeName);
                _logger.LogDebug("Received log flush confirmation from node {NodeName} for internal operation {OperationId}.", nodeName, operationId);

                var totalNodes = opContext.Operation.NodeTasks.Select(t => t.NodeName).Distinct().Count();
                if (opContext.ConfirmedLogFlushNodes.Count >= totalNodes)
                {
                    _logger.LogInformation("All nodes have confirmed log flush for internal operation {OperationId}.", operationId);
                    // Signal that the slave confirmation phase is complete.
                    opContext.LogFlushCompletionSource.TrySetResult();
                }
            }
            else
            {
                // This log message is now more accurate.
                _logger.LogWarning("Received log flush confirmation for an unknown or already completed operation: {OperationId}", operationId);
            }
        }

        /// <summary>
        /// Monitors a single dispatched task for execution timeout.
        /// </summary>
        private async Task MonitorExecutionTimeoutAsync(ActiveOperationContext opContext, string taskId, TimeSpan timeout)
        {
            await Task.Delay(timeout);

            // Re-check that the operation is still active.
            if (_activeOperations.TryGetValue(opContext.Operation.Id, out var currentOpContext))
            {
                var task = currentOpContext.Operation.NodeTasks.FirstOrDefault(t => t.TaskId == taskId);

                // If the task exists and is not yet in a terminal state, it has timed out.
                if (task != null && !task.Status.IsTerminal())
                {
                    _logger.LogWarning("Execution timed out for task {TaskId} in operation {OperationId} after {Seconds} seconds.", taskId, opContext.Operation.Id, timeout.TotalSeconds);
                    task.Status = NodeTaskStatus.TimedOut;
                    task.StatusMessage = $"Task did not complete within the {timeout.TotalSeconds}-second timeout period and was marked as timed out by the master.";
                    task.EndTime = DateTime.UtcNow;
                    await RecalculateOperationStatusAsync(currentOpContext);
                }
            }
        }    }
}
