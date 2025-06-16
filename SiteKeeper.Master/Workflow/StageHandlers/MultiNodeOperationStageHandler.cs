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

            public ActiveOperationContext(Operation operation, IProgress<StageProgress> progress, MasterActionContext parentMasterActionContext)
            {
                Operation = operation;
                ProgressReporter = progress;
                ParentMasterActionContext = parentMasterActionContext;
                OperationCompletionSource = new TaskCompletionSource<MultiNodeOperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                ConfirmedLogFlushNodes = new ConcurrentBag<string>();
                LogFlushCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
        #endregion

        private readonly ILogger<MultiNodeOperationStageHandler> _logger;
        private readonly IAgentConnectionManagerService _agentConnectionManager;
        private readonly IJournalService _journalService;
        private readonly INodeHealthMonitorService _nodeHealthMonitorService;
        private readonly ConcurrentDictionary<string, ActiveOperationContext> _activeOperations = new();

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
            if (!_activeOperations.TryAdd(operation.Id, opContext))
            {
                throw new InvalidOperationException($"Operation with ID {operation.Id} is already running.");
            }
            
            _logger.LogInformation("Starting multi-node operation stage: {OperationType} for MasterAction {MasterActionId}", operation.Type, masterActionContext.MasterActionId);

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
                    _logger.LogWarning("Multi-node operation stage {OperationId} was cancelled. Notifying slaves to terminate their tasks.", operation.Id);
                    operation.OverallStatus = OperationOverallStatus.Cancelling;
                    
                    var tasksToCancel = operation.NodeTasks.Where(t => !t.Status.IsTerminal()).ToList();
                    foreach (var task in tasksToCancel)
                    {
                        task.Status = NodeTaskStatus.Cancelling;
                        await _agentConnectionManager.SendCancelTaskAsync(task.NodeName, new CancelTaskOnAgentRequest { OperationId = operation.Id, TaskId = task.TaskId, Reason = "Operation cancelled by master."});
                    }
                    
                    await MonitorCancellationCompletion(opContext, TimeSpan.FromSeconds(15));
                    
                    operation.OverallStatus = OperationOverallStatus.Cancelled;
                    operation.EndTime = DateTime.UtcNow;

                    var cancelledResult = new MultiNodeOperationResult { IsSuccess = false, FinalOperationState = operation };
                    await FlushAllNodeLogsAsync(cancelledResult, masterActionContext);
                    return cancelledResult;
                }
                finally
                {
                    healthMonitoringCts.Cancel();
                    _activeOperations.TryRemove(operation.Id, out _);
                    _logger.LogInformation("Multi-node operation stage {OperationId} finished and cleaned up.", operation.Id);
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
            if (!_activeOperations.TryGetValue(readinessReport.OperationId, out var opContext)) return;

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
        
        /// <summary>
        /// Commands all nodes to flush their logs for a completed operation and waits for their confirmation.
        /// </summary>
        private async Task FlushAllNodeLogsAsync(MultiNodeOperationResult operationResult, MasterActionContext masterActionContext)
        {
            var participatingNodes = operationResult.FinalOperationState.NodeTasks.Select(t => t.NodeName).Distinct().ToList();
            if (!participatingNodes.Any()) return;
            if (!_activeOperations.TryGetValue(operationResult.FinalOperationState.Id, out var opContext)) return;

            masterActionContext.LogInfo($"Operation stage complete. Waiting for {participatingNodes.Count} node(s) to flush final logs...");
            
            foreach (var nodeName in participatingNodes)
            {
                await _agentConnectionManager.RequestLogFlushForTask(nodeName, operationResult.FinalOperationState.Id);
            }

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var completedTask = await Task.WhenAny(opContext.LogFlushCompletionSource.Task, timeoutTask);
            
            if (completedTask == timeoutTask)
                masterActionContext.Logger.LogWarning("Timed out waiting for all nodes to confirm log flush for OperationId {OperationId}.", operationResult.FinalOperationState.Id);
            else 
                 masterActionContext.LogInfo("All node logs have been flushed successfully.");
        }

        /// <summary>
        /// Confirms that a slave has finished flushing its logs, received via the AgentHub.
        /// </summary>
        public void ConfirmLogFlush(string operationId, string nodeName)
        {
            if (_activeOperations.TryGetValue(operationId, out var opContext))
            {
                opContext.ConfirmedLogFlushNodes.Add(nodeName);
                _logger.LogDebug("Received log flush confirmation from node {NodeName} for operation {OperationId}.", nodeName, operationId);

                var totalNodes = opContext.Operation.NodeTasks.Select(t => t.NodeName).Distinct().Count();
                if (opContext.ConfirmedLogFlushNodes.Count >= totalNodes)
                {
                    _logger.LogInformation("All nodes have confirmed log flush for operation {OperationId}.", operationId);
                    opContext.LogFlushCompletionSource.TrySetResult();
                }
            }
        }
    }
}
