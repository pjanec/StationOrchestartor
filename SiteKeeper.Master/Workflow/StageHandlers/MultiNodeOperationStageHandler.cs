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
    /// A specialized, stateful stage handler that implements <see cref="IStageHandler{TInput, TOutput}"/>
    /// for executing and managing operations distributed across multiple slave nodes.
    /// </summary>
    /// <remarks>
    /// This class is typically registered as a singleton service. It is responsible for the entire lifecycle
    /// of a multi-node operation phase within a larger <see cref="MasterAction"/> workflow. This includes:
    /// <list type="bullet">
    ///   <item><description>Managing state for active multi-node operations using <see cref="ActiveOperationContext"/>.</description></item>
    ///   <item><description>Coordinating readiness checks with slave agents via <see cref="IAgentConnectionManagerService.SendPrepareForTaskInstructionAsync"/>.</description></item>
    ///   <item><description>Dispatching tasks to ready slave agents using <see cref="IAgentConnectionManagerService.SendSlaveTaskAsync"/>.</description></item>
    ///   <item><description>Processing asynchronous feedback from slaves (readiness, progress, completion) via methods like <see cref="ProcessSlaveTaskReadinessAsync"/> and <see cref="ProcessTaskStatusUpdateAsync"/>.</description></item>
    ///   <item><description>Tracking progress and aggregating results for the <see cref="MultiNodeOperationResult"/>.</description></item>
    ///   <item><description>Handling operation cancellation requests and node health monitoring during execution.</description></item>
    ///   <item><description>Journaling significant events and logs via <see cref="IJournalService"/> and <see cref="MasterActionContext"/>.</description></item>
    ///   <item><description>Utilizing an internal log channel per operation for asynchronous log processing from slaves (<see cref="JournalSlaveLogAsync"/>, <see cref="ConsumeLogChannelAsync"/>, <see cref="FlushAllNodeLogsAsync"/>).</description></item>
    /// </list>
    /// It relies on being called by the <see cref="MasterActionCoordinatorService"/> which sets up the overall <see cref="MasterActionContext"/>.
    /// </remarks>
    public class MultiNodeOperationStageHandler : IStageHandler<MultiNodeOperationInput, MultiNodeOperationResult>
    {
        #region Private State Management Class
        /// <summary>
        /// A private nested class to hold the complete state for each active multi-node operation
        /// being managed by this handler. This allows the singleton handler to manage multiple operations
        /// if the overarching system design were to allow concurrent MasterActions (currently, MasterActionCoordinator enforces one at a time).
        /// </summary>
        private class ActiveOperationContext
        {
            /// <summary>Gets the core <see cref="Operation"/> object containing all tasks, parameters, and overall state for this multi-node stage.</summary>
            public Operation Operation { get; }
            /// <summary>Gets a reference to the parent <see cref="MasterActionContext"/> for logging, journaling, and cancellation.</summary>
            public MasterActionContext ParentMasterActionContext { get; }
            /// <summary>
            /// Gets the <see cref="TaskCompletionSource{TResult}"/> that the <see cref="ExecuteAsync"/> method awaits.
            /// This is set to completed, faulted, or cancelled only when the entire multi-node operation stage finishes.
            /// </summary>
            public TaskCompletionSource<MultiNodeOperationResult> OperationCompletionSource { get; }
            /// <summary>Gets a thread-safe collection to track which nodes have confirmed their logs are flushed after task completion.</summary>
            public ConcurrentBag<string> ConfirmedLogFlushNodes { get; }
            /// <summary>Gets the <see cref="TaskCompletionSource"/> for the log flush phase. It is completed when all expected online nodes have confirmed log flush.</summary>
            public TaskCompletionSource LogFlushCompletionSource { get; }
            /// <summary>Gets the progress reporter for this specific multi-node stage, used to update the parent <see cref="MasterActionContext"/>.</summary>
            public IProgress<StageProgress> ProgressReporter { get; }
			
            /// <summary>Gets the <see cref="Channel{T}"/> used to queue log entries from slave agents for asynchronous journaling.</summary>
			public Channel<SlaveTaskLogEntry> LogChannel { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="ActiveOperationContext"/> class.
            /// </summary>
            /// <param name="operation">The operation to be managed.</param>
            /// <param name="progress">The progress reporter for this stage.</param>
            /// <param name="parentMasterActionContext">The context of the parent master action.</param>
            public ActiveOperationContext(Operation operation, IProgress<StageProgress> progress, MasterActionContext parentMasterActionContext)
            {
                Operation = operation;
                ProgressReporter = progress;
                ParentMasterActionContext = parentMasterActionContext;
                OperationCompletionSource = new TaskCompletionSource<MultiNodeOperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                ConfirmedLogFlushNodes = new ConcurrentBag<string>();
                LogFlushCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                LogChannel = Channel.CreateUnbounded<SlaveTaskLogEntry>(new UnboundedChannelOptions { SingleReader = true });
            }
        }
        #endregion

        private readonly ILogger<MultiNodeOperationStageHandler> _logger;
        private readonly IAgentConnectionManagerService _agentConnectionManager;
        private readonly IJournalService _journalService;
        private readonly INodeHealthMonitorService _nodeHealthMonitorService;
        // Key: Internal Operation.Id (from MultiNodeOperationInput.OperationToExecute.Id)
        private readonly ConcurrentDictionary<string, ActiveOperationContext> _activeOperations = new();
        // Key: NodeTask.TaskId, Value: Internal Operation.Id
        private readonly ConcurrentDictionary<string, string> _taskIdToOperationIdMap = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiNodeOperationStageHandler"/> class.
        /// </summary>
        /// <param name="logger">The logger for recording handler activities and errors.</param>
        /// <param name="agentConnectionManager">Service for sending commands to slave agents.</param>
        /// <param name="journalService">Service for recording operation progress and results to the journal.</param>
        /// <param name="nodeHealthMonitorService">Service for querying node health and connectivity.</param>
        /// <exception cref="ArgumentNullException">Thrown if any of the injected services are null.</exception>
        public MultiNodeOperationStageHandler(
            ILogger<MultiNodeOperationStageHandler> logger,
            IAgentConnectionManagerService agentConnectionManager,
            IJournalService journalService,
            INodeHealthMonitorService nodeHealthMonitorService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _agentConnectionManager = agentConnectionManager ?? throw new ArgumentNullException(nameof(agentConnectionManager));
            _journalService = journalService ?? throw new ArgumentNullException(nameof(journalService));
            _nodeHealthMonitorService = nodeHealthMonitorService ?? throw new ArgumentNullException(nameof(nodeHealthMonitorService));
        }

        /// <summary>
        /// Executes a multi-node operation as a single stage within a <see cref="MasterAction"/> workflow. This is the main entry point for this handler.
        /// </summary>
        /// <remarks>
        /// This method orchestrates the entire lifecycle of the multi-node operation stage:
        /// <list type="number">
        ///   <item><description>Initializes an <see cref="ActiveOperationContext"/> to manage the state of the operation.</description></item>
        ///   <item><description>Registers the operation and its tasks for tracking.</description></item>
        ///   <item><description>Starts background tasks for consuming slave logs (<see cref="ConsumeLogChannelAsync"/>) and monitoring node health (<see cref="MonitorNodeHealthForOperation"/>).</description></item>
        ///   <item><description>Initiates the readiness check and task dispatch sequence (<see cref="StartReadinessCheckAndDispatchSequenceAsync"/>).</description></item>
        ///   <item><description>Awaits the completion of the operation, which is signaled by <see cref="ActiveOperationContext.OperationCompletionSource"/>.</description></item>
        ///   <item><description>Handles cancellation requests by propagating cancellation to tasks and awaiting their graceful termination.</description></item>
        ///   <item><description>Ensures all node logs are flushed (<see cref="FlushAllNodeLogsAsync"/>) before returning the final result.</description></item>
        ///   <item><description>Performs cleanup of internal state upon completion, failure, or cancellation.</description></item>
        /// </list>
        /// </remarks>
        /// <param name="input">The <see cref="MultiNodeOperationInput"/> DTO containing the pre-configured <see cref="Operation"/> object to execute, including its list of <see cref="NodeTask"/>s.</param>
        /// <param name="masterActionContext">The context of the parent <see cref="MasterAction"/>, providing access to logging, journaling, and overall cancellation.</param>
        /// <param name="progress">The progress reporter for this stage, used to update the <see cref="MasterActionContext"/> with stage-level progress.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> for the parent <see cref="MasterAction"/>. If cancellation is requested, this handler will attempt to gracefully cancel ongoing tasks.</param>
        /// <returns>A <see cref="Task{TResult}"/> that represents the asynchronous execution of the stage. The task result is a <see cref="MultiNodeOperationResult"/> containing the final state of the <see cref="Operation"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if an operation with the same internal ID is already being managed by this handler.</exception>
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
                return new MultiNodeOperationResult { IsSuccess = true, FinalOperationState = operation ?? new Operation("empty-op", OperationType.NoOp, masterActionContext.MasterActionId) };
            }

            var opContext = new ActiveOperationContext(operation, progress, masterActionContext);

            if (!_activeOperations.TryAdd(operation.Id, opContext))
            {
                _logger.LogError("Failed to add operation {OperationId} to active operations; an operation with this ID may already be running.", operation.Id);
                throw new InvalidOperationException($"An operation with the internal ID {operation.Id} is already running or failed to be added to tracking.");
            }
        
            foreach (var task in operation.NodeTasks)
            {
                _taskIdToOperationIdMap[task.TaskId] = operation.Id;
            }
              
            _logger.LogInformation("Starting multi-node operation stage: {OperationType} (OpId: {OperationId}) for MasterAction {MasterActionId}",
                operation.Type, operation.Id, masterActionContext.MasterActionId);

            // Set ambient context for NLog MDLC, specific to this operation instance
            using (MappedDiagnosticsLogicalContext.SetScoped("SK-OperationId", operation.Id))
            {
                var logConsumerTask = ConsumeLogChannelAsync(opContext);

                var healthMonitoringCts = new CancellationTokenSource();
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, healthMonitoringCts.Token); // Link with parent cancellation
                _ = MonitorNodeHealthForOperation(opContext, linkedCts.Token);

                // Register callback for parent MasterAction cancellation
                using (cancellationToken.Register(() =>
                       {
                           _logger.LogWarning("Parent MasterAction cancellation requested for multi-node operation {OperationId}.", operation.Id);
                           opContext.OperationCompletionSource.TrySetCanceled();
                       }
                ))
                {
                    try
                    {
                        await StartReadinessCheckAndDispatchSequenceAsync(operation);
                        var operationResult = await opContext.OperationCompletionSource.Task; // Wait for operation to complete/fail/cancel

                        _logger.LogInformation("Multi-node operation {OperationId} reached terminal state: {Status}. Preparing to flush logs.", operation.Id, operationResult.FinalOperationState.OverallStatus);
                        await FlushAllNodeLogsAsync(operationResult, masterActionContext);

                        _logger.LogInformation("Log flushing complete for {OperationId}. Returning final result.", operation.Id);
                        return operationResult;
                    }
                    catch (TaskCanceledException) // This catches cancellation signaled on opContext.OperationCompletionSource.Task
                    {
                         _logger.LogWarning("Multi-node operation stage {OperationId} was cancelled. Handling cancellation logic...", operation.Id);
                        operation.OverallStatus = OperationOverallStatus.Cancelling; // Mark primary operation state

                        var tasksToCancel = operation.NodeTasks.Where(t => !t.Status.IsTerminal()).ToList();
                        _logger.LogInformation("Identified {Count} non-terminal tasks to process for cancellation for OpId {OperationId}.", tasksToCancel.Count, operation.Id);

                        foreach (var task in tasksToCancel)
                        {
                            var nodeState = await _nodeHealthMonitorService.GetNodeCachedStateAsync(task.NodeName);
                            if (nodeState?.ConnectivityStatus is AgentConnectivityStatus.Offline or AgentConnectivityStatus.Unreachable)
                            {
                                _logger.LogInformation("Task {TaskId} on offline/unreachable node {NodeName} for OpId {OperationId} marked as Cancelled.", task.TaskId, task.NodeName, operation.Id);
                                task.Status = NodeTaskStatus.Cancelled;
                                task.StatusMessage = "Task cancelled; target node was offline or unreachable.";
                                task.EndTime = DateTime.UtcNow;
                            }
                            else
                            {
                                _logger.LogInformation("Requesting cancellation for task {TaskId} on online node {NodeName} for OpId {OperationId}.", task.TaskId, task.NodeName, operation.Id);
                                task.Status = NodeTaskStatus.Cancelling;
                                // Asynchronously send cancel command, do not await each one here to parallelize requests
                                _ = _agentConnectionManager.SendCancelTaskAsync(task.NodeName, new CancelTaskOnAgentRequest { OperationId = operation.Id, TaskId = task.TaskId, Reason = "Operation cancelled by master." });
                            }
                        }

                        await MonitorCancellationCompletion(opContext, TimeSpan.FromSeconds(15)); // Wait for tasks to be confirmed cancelled or timeout

                        foreach (var task in operation.NodeTasks.Where(t => t.Status == NodeTaskStatus.Cancelling))
                        {
                            _logger.LogInformation("Finalizing status for task {TaskId} (OpId {OperationId}) from 'Cancelling' to 'Cancelled' after monitor.", task.TaskId, operation.Id);
                            task.Status = NodeTaskStatus.Cancelled;
                            task.StatusMessage = task.StatusMessage ?? "Task cancellation finalized by master after monitoring period.";
                            task.EndTime = DateTime.UtcNow;
                        }

                        operation.OverallStatus = OperationOverallStatus.Cancelled;
                        operation.EndTime = DateTime.UtcNow;
                        masterActionContext.LogWarning($"Multi-node operation {operation.Id} for MasterAction {masterActionContext.MasterActionId} was cancelled.");

                        var cancelledResult = new MultiNodeOperationResult { IsSuccess = false, FinalOperationState = operation };

                        // Attempt to flush logs even on cancellation before setting the final result
                        await FlushAllNodeLogsAsync(cancelledResult, masterActionContext);
                        opContext.OperationCompletionSource.TrySetResult(cancelledResult); // Ensure TCS is completed
                        return cancelledResult;
                    }
                    catch(Exception ex)
                    {
                        _logger.LogError(ex, "Unhandled exception during ExecuteAsync for multi-node operation {OperationId}.", operation.Id);
                        operation.OverallStatus = OperationOverallStatus.Failed;
                        operation.StatusMessage = $"Stage handler failed: {ex.Message}";
                        operation.EndTime = DateTime.UtcNow;
                        var failedResult = new MultiNodeOperationResult { IsSuccess = false, FinalOperationState = operation };
                        await FlushAllNodeLogsAsync(failedResult, masterActionContext);
                        opContext.OperationCompletionSource.TrySetResult(failedResult); // Ensure TCS is completed
                        throw; // Re-throw to be caught by MasterActionCoordinator
                    }
                    finally
                    {
                        _logger.LogDebug("Initiating cleanup for multi-node operation {OperationId} in ExecuteAsync.finally.", operation.Id);
                        healthMonitoringCts.Cancel(); // Stop the specific health monitor for this operation
                        _activeOperations.TryRemove(operation.Id, out _);

                        foreach (var task in operation.NodeTasks) // Clean up TaskId map
                        {
                            _taskIdToOperationIdMap.TryRemove(task.TaskId, out _);
                        }

                        opContext.LogChannel.Writer.TryComplete(); // Signal log consumer to finish
                        await logConsumerTask; // Ensure log consumer finishes processing queued logs
 
                        _logger.LogInformation("Multi-node operation stage {OperationId} (MasterAction {MasterActionId}) finished execution and cleanup.",
                            operation.Id, masterActionContext.MasterActionId);
                    }
                }
            }
        }
        
        /// <summary>
        /// A background task that periodically checks the health of nodes involved in an active operation.
        /// If a node involved in a non-terminal task becomes disconnected (Offline or Unreachable),
        /// this method will mark the corresponding task as <see cref="NodeTaskStatus.NodeOfflineDuringTask"/>
        /// and trigger a recalculation of the overall operation status.
        /// </summary>
        /// <param name="opContext">The context of the active multi-node operation.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to stop the monitoring when the operation completes or is cancelled.</param>
        private async Task MonitorNodeHealthForOperation(ActiveOperationContext opContext, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken); // Check interval

                    var activeTasks = opContext.Operation.NodeTasks.Where(t => !t.Status.IsTerminal()).ToList();
                    if (!activeTasks.Any())
                    {
                        _logger.LogDebug("No active tasks remaining for health monitoring in OpId {OperationId}. Exiting monitor loop.", opContext.Operation.Id);
                        return; // All tasks are terminal, no need to monitor further.
                    }

                    foreach (var task in activeTasks)
                    {
                        var nodeState = await _nodeHealthMonitorService.GetNodeCachedStateAsync(task.NodeName);
                        if (nodeState?.ConnectivityStatus is AgentConnectivityStatus.Offline or AgentConnectivityStatus.Unreachable)
                        {
                            _logger.LogError("Node '{NodeName}' disconnected or became unreachable during active task {TaskId} for OpId {OperationId}. Failing the task.",
                                task.NodeName, task.TaskId, opContext.Operation.Id);
                            task.Status = NodeTaskStatus.NodeOfflineDuringTask;
                            task.StatusMessage = $"Node went offline or became unreachable during task execution. Last known connectivity: {nodeState.ConnectivityStatus}.";
                            task.EndTime = DateTime.UtcNow;
                            await RecalculateOperationStatusAsync(opContext); // This might complete the operation
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("Node health monitoring loop for OpId {OperationId} was cancelled.", opContext.Operation.Id);
                    return; // Expected when cancellationToken is triggered
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in node health monitoring loop for operation {OperationId}.", opContext.Operation.Id);
                    // Continue monitoring if possible, or consider if this error should fail the operation
                }
            }
             _logger.LogInformation("Exited node health monitoring loop for OpId {OperationId} due to cancellation request.", opContext.Operation.Id);
        }

        /// <summary>
        /// Monitors tasks that are in the 'Cancelling' state and waits for them to transition to a terminal state
        /// (e.g., Cancelled, Failed, or NodeOfflineDuringTask), or until a timeout is reached.
        /// This is used during the operation cancellation process.
        /// </summary>
        /// <param name="opContext">The context of the active multi-node operation being cancelled.</param>
        /// <param name="timeout">The maximum time to wait for nodes to confirm cancellation.</param>
        private async Task MonitorCancellationCompletion(ActiveOperationContext opContext, TimeSpan timeout)
        {
            var timeoutCts = new CancellationTokenSource(timeout);
            _logger.LogInformation("Monitoring for cancellation confirmation from nodes for OpId: {OperationId}", opContext.Operation.Id);

            try
            {
                while (!timeoutCts.IsCancellationRequested)
                {
                    var tasksStillCancelling = opContext.Operation.NodeTasks.Where(t => t.Status == NodeTaskStatus.Cancelling).ToList();
                    
                    if (!tasksStillCancelling.Any())
                    {
                        _logger.LogInformation("All tasks have moved past 'Cancelling' state for OpId {OperationId}.", opContext.Operation.Id);
                        return;
                    }

                    // Check if all remaining 'Cancelling' tasks are on nodes that are now offline.
                    // If so, we don't need to wait for their explicit confirmation.
                    bool allRemainingCancellingAreOffline = true;
                    foreach (var task in tasksStillCancelling)
                    {
                        var nodeState = await _nodeHealthMonitorService.GetNodeCachedStateAsync(task.NodeName);
                        if (nodeState?.ConnectivityStatus != AgentConnectivityStatus.Offline &&
                            nodeState?.ConnectivityStatus != AgentConnectivityStatus.Unreachable)
                        {
                            allRemainingCancellingAreOffline = false;
                            break;
                        }
                    }

                    if (allRemainingCancellingAreOffline)
                    {
                        _logger.LogInformation("All tasks still in 'Cancelling' state for OpId {OperationId} are on offline/unreachable nodes. Proceeding with cancellation finalization.", opContext.Operation.Id);
                        return;
                    }
                    await Task.Delay(500, timeoutCts.Token); // Check status periodically
                }
            }
            catch (TaskCanceledException) // This means the timeoutCts was cancelled (timeout occurred)
            {
                _logger.LogWarning("Timeout reached while waiting for cancellation confirmation for OpId {OperationId}.", opContext.Operation.Id);
            }
            finally
            {
                timeoutCts.Dispose();
            }

            // If we reach here, it means timeout occurred or loop exited for other reasons before all tasks confirmed.
            // Log and potentially mark remaining 'Cancelling' tasks based on last known node status.
            var tasksLeftCancelling = opContext.Operation.NodeTasks.Where(t => t.Status == NodeTaskStatus.Cancelling).ToList();
            if (tasksLeftCancelling.Any())
            {
                _logger.LogWarning("{Count} tasks still in 'Cancelling' state after monitoring period for OpId {OperationId}. Forcibly marking them based on node status.",
                    tasksLeftCancelling.Count, opContext.Operation.Id);
                foreach (var task in tasksLeftCancelling)
                {
                    var nodeState = await _nodeHealthMonitorService.GetNodeCachedStateAsync(task.NodeName);
                    if (nodeState?.ConnectivityStatus is AgentConnectivityStatus.Offline or AgentConnectivityStatus.Unreachable)
                    {
                        task.Status = NodeTaskStatus.Cancelled; // Or NodeOfflineDuringTask if more appropriate
                        task.StatusMessage = "Task cancellation confirmed as node became unresponsive.";
                    }
                    else
                    {
                        // If node is still online but didn't confirm, it might be considered a failed cancellation.
                        // However, for simplicity in cancellation flow, master might just mark it as Cancelled.
                        task.Status = NodeTaskStatus.Cancelled;
                        task.StatusMessage = "Node did not confirm cancellation within timeout; master marked as cancelled.";
                    }
                    task.EndTime = DateTime.UtcNow;
                }
            }
        }
        
        /// <summary>
        /// Initiates the readiness check phase for all tasks in the operation.
        /// Sets the operation status to <see cref="OperationOverallStatus.AwaitingNodeReadiness"/> and sends
        /// <see cref="PrepareForTaskInstruction"/> to each relevant agent via <see cref="IAgentConnectionManagerService"/>.
        /// Also starts a timeout monitor for the readiness phase.
        /// </summary>
        /// <param name="operation">The <see cref="Operation"/> containing tasks to prepare.</param>
        private async Task StartReadinessCheckAndDispatchSequenceAsync(Operation operation)
        {
            operation.StartTime = operation.StartTime == DateTime.MinValue ? DateTime.UtcNow : operation.StartTime; // Set start time if not already set
            operation.OverallStatus = OperationOverallStatus.AwaitingNodeReadiness;
            operation.StatusMessage = "Awaiting readiness reports from nodes.";
            // Report initial progress for the stage
            if(_activeOperations.TryGetValue(operation.Id, out var opCtx))
            {
                opCtx.ProgressReporter.Report(new StageProgress { ProgressPercent = 5, StatusMessage = "Initiating readiness checks..." });
            }

            _logger.LogInformation("OpId {OperationId}: Starting readiness checks for {TaskCount} tasks.", operation.Id, operation.NodeTasks.Count);
            foreach (var task in operation.NodeTasks)
            {
                task.Status = NodeTaskStatus.AwaitingReadiness; // Initial status before sending prepare instruction
                string? prepParamsJson = null;
                // Example: Serialize TaskPayload only if it's for TestOrchestration or specific types needing it for prep
                if (task.TaskType == SlaveTaskType.TestOrchestration && task.TaskPayload != null)
                {
                    try
                    {
                        prepParamsJson = JsonSerializer.Serialize(task.TaskPayload, _jsonOptions);
                    }
                    catch(Exception ex)
                    {
                        _logger.LogError(ex, "Failed to serialize TaskPayload for TestOrchestration task {TaskId} on node {NodeName}.", task.TaskId, task.NodeName);
                        // Decide if this task should fail immediately or proceed without prepParams
                    }
                }

                var prepareInstruction = new PrepareForTaskInstruction
                {
                    OperationId = task.OperationId, // Should be Operation.Id
                    TaskId = task.TaskId,
                    ExpectedTaskType = task.TaskType,
                    PreparationParametersJson = prepParamsJson,
                    TargetResource = task.TargetResource // Assuming NodeTask has TargetResource
                };

                // Update task status before sending
                task.Status = NodeTaskStatus.ReadinessCheckSent;
                task.StatusMessage = "Prepare instruction sent, awaiting readiness report.";
                task.LastUpdateTime = DateTime.UtcNow;
                await _agentConnectionManager.SendPrepareForTaskInstructionAsync(task.NodeName, prepareInstruction);
                _logger.LogDebug("OpId {OperationId}: Sent PrepareForTaskInstruction for TaskId {TaskId} to Node {NodeName}.", operation.Id, task.TaskId, task.NodeName);
            }

            // Start a timer to monitor for readiness check timeouts.
            // The duration should be configurable or based on operation parameters.
            _ = MonitorReadinessTimeout(operation.Id, TimeSpan.FromSeconds(_activeOperations.TryGetValue(operation.Id, out var activeOpCtx) ? activeOpCtx.Operation.DefaultTaskTimeoutSeconds : 30));
        }

        /// <summary>
        /// Processes a <see cref="SlaveTaskReadinessReport"/> received from a slave agent via the <see cref="AgentHub"/>.
        /// Updates the corresponding <see cref="NodeTask"/>'s status. If the slave is ready, it dispatches the actual
        /// <see cref="SlaveTaskInstruction"/> to the agent and starts monitoring for execution timeout.
        /// If not ready, the task is marked as <see cref="NodeTaskStatus.NotReadyForTask"/>.
        /// Finally, it recalculates the overall operation status.
        /// </summary>
        /// <param name="readinessReport">The <see cref="SlaveTaskReadinessReport"/> DTO from the slave agent.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous processing of the readiness report.</returns>
        public async Task ProcessSlaveTaskReadinessAsync(SlaveTaskReadinessReport readinessReport)
        {
            if (!_taskIdToOperationIdMap.TryGetValue(readinessReport.TaskId, out var internalOpId) ||
                !_activeOperations.TryGetValue(internalOpId, out var opContext))
            {
                _logger.LogWarning("Received readiness report for an unknown or completed task/operation: TaskId {TaskId}, MappedOpId {InternalOpId}",
                    readinessReport.TaskId, internalOpId ?? "N/A");
                return;
            }

            var task = opContext.Operation.NodeTasks.FirstOrDefault(t => t.TaskId == readinessReport.TaskId);
            if (task == null)
            {
                _logger.LogWarning("Readiness report for unknown TaskId {TaskId} in active OpId {OpId}", readinessReport.TaskId, internalOpId);
                return;
            }

            if (task.Status.IsTerminal()) // If task already completed/failed/cancelled (e.g. by timeout)
            {
                 _logger.LogInformation("Received readiness report for already terminal task {TaskId} with status {Status}. Ignoring.", task.TaskId, task.Status);
                return;
            }

            task.LastUpdateTime = DateTime.UtcNow;
            if (readinessReport.IsReady)
            {
                task.Status = NodeTaskStatus.ReadyToExecute;
                task.StatusMessage = "Agent reported ready for task execution.";
                _logger.LogInformation("Node {NodeName} is READY for task {TaskId} (OpId {OpId}). Dispatching task.",
                    task.NodeName, task.TaskId, internalOpId);

                var slaveTaskInstruction = new SlaveTaskInstruction
                {
                    OperationId = task.OperationId, // This is the internal Operation.Id
                    TaskId = task.TaskId,
                    TaskType = task.TaskType,
                    ParametersJson = task.TaskPayload != null ? JsonSerializer.Serialize(task.TaskPayload, _jsonOptions) : null,
                    TimeoutSeconds = task.TimeoutSeconds > 0 ? task.TimeoutSeconds : opContext.Operation.DefaultTaskTimeoutSeconds // Use task-specific or operation default
                };

                await _agentConnectionManager.SendSlaveTaskAsync(task.NodeName, slaveTaskInstruction);
                task.Status = NodeTaskStatus.TaskDispatched;
                task.StatusMessage = "Task instruction dispatched to agent.";
                task.StartTime = DateTime.UtcNow; // Mark task start time

                var executionTimeout = TimeSpan.FromSeconds(slaveTaskInstruction.TimeoutSeconds ?? opContext.Operation.DefaultTaskTimeoutSeconds);
                _ = MonitorExecutionTimeoutAsync(opContext, task.TaskId, executionTimeout);
            }
            else
            {
                task.Status = NodeTaskStatus.NotReadyForTask;
                task.StatusMessage = $"Agent reported NOT READY: {readinessReport.ReasonIfNotReady}";
                task.EndTime = DateTime.UtcNow; // Terminal state
                _logger.LogWarning("Node {NodeName} is NOT READY for task {TaskId} (OpId {OpId}). Reason: {Reason}",
                    task.NodeName, task.TaskId, internalOpId, readinessReport.ReasonIfNotReady);
            }
            await RecalculateOperationStatusAsync(opContext);
        }

        /// <summary>
        /// Processes a <see cref="SlaveTaskProgressUpdate"/> received from a slave agent via the <see cref="AgentHub"/>.
        /// Updates the corresponding <see cref="NodeTask"/>'s status, progress percentage, and status message.
        /// If the task update indicates a terminal state, the task's end time is recorded, and its result payload is processed and journaled.
        /// Finally, it recalculates the overall operation status.
        /// </summary>
        /// <param name="statusUpdate">The <see cref="SlaveTaskProgressUpdate"/> DTO from the slave agent.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous processing of the status update.</returns>
        public async Task ProcessTaskStatusUpdateAsync(SlaveTaskProgressUpdate statusUpdate)
        {
            if (!_taskIdToOperationIdMap.TryGetValue(statusUpdate.TaskId, out var internalOpId) ||
                !_activeOperations.TryGetValue(internalOpId, out var opContext))
            {
                _logger.LogWarning("Received task status update for an unknown or completed task/operation: TaskId {TaskId}, MappedOpId {InternalOpId}",
                    statusUpdate.TaskId, internalOpId ?? "N/A");
                return;
            }

            var nodeTask = opContext.Operation.NodeTasks.FirstOrDefault(t => t.TaskId == statusUpdate.TaskId);
            if (nodeTask == null)
            {
                 _logger.LogWarning("Status update for unknown TaskId {TaskId} in active OpId {OpId}", statusUpdate.TaskId, internalOpId);
                return;
            }

            if (nodeTask.Status.IsTerminal() && statusUpdate.Status != nodeTask.Status.ToString()) // If task already completed/failed/cancelled by master
            {
                 _logger.LogInformation("Received status update '{SlaveStatus}' for already terminal task {TaskId} (MasterStatus: {MasterStatus}). Ignoring update, but processing logs if any.",
                    statusUpdate.Status, nodeTask.TaskId, nodeTask.Status);
                // Potentially still process logs if the update contains them, even if master thinks task is terminal.
                // This can happen if slave sends final log and final status in quick succession and master processes its own timeout/cancellation first.
                // For now, we just log and ignore the status part. Log processing is handled by JournalSlaveLogAsync.
                return;
            }

            nodeTask.LastUpdateTime = statusUpdate.TimestampUtc;
            if (Enum.TryParse<NodeTaskStatus>(statusUpdate.Status, true, out var parsedStatus))
            {
                nodeTask.Status = parsedStatus;
            }
            else
            {
                _logger.LogWarning("Failed to parse NodeTaskStatus '{StatusString}' for TaskId {TaskId}", statusUpdate.Status, nodeTask.TaskId);
                // Keep existing status or set to Unknown? For now, keep existing.
            }
            nodeTask.ProgressPercent = statusUpdate.ProgressPercent ?? nodeTask.ProgressPercent;
            nodeTask.StatusMessage = statusUpdate.Message ?? nodeTask.StatusMessage; // Preserve existing message if new one is null

            if (nodeTask.Status.IsTerminal())
            {
                nodeTask.EndTime = statusUpdate.TimestampUtc;
                if (!string.IsNullOrEmpty(statusUpdate.ResultJson))
                {
                    try
                    {
                        // Ensure ResultPayload is initialized
                        nodeTask.ResultPayload ??= new Dictionary<string, object>();
                        var deserializedResult = JsonSerializer.Deserialize<Dictionary<string, object>>(statusUpdate.ResultJson, _jsonOptions);
                        if (deserializedResult != null)
                        {
                            foreach(var kvp in deserializedResult) nodeTask.ResultPayload[kvp.Key] = kvp.Value;
                        }
                        _logger.LogInformation("Task {TaskId} (OpId {OpId}) completed with ResultPayload.", nodeTask.TaskId, internalOpId);
                    }
                    catch(Exception ex)
                    {
                         _logger.LogError(ex, "Failed to deserialize ResultJson for task {TaskId} (OpId {OpId}). JSON: {Json}", nodeTask.TaskId, internalOpId, statusUpdate.ResultJson);
                         nodeTask.ResultPayload ??= new Dictionary<string, object>();
                         nodeTask.ResultPayload["DeserializationError"] = $"Failed to parse ResultJson: {ex.Message}";
                    }
                }
                // Journal the result of the individual node task. This is important for detailed diagnostics.
                await _journalService.RecordNodeTaskResultAsync(opContext.ParentMasterActionContext, nodeTask);
            }
            await RecalculateOperationStatusAsync(opContext);
        }
        
        /// <summary>
        /// Recalculates the overall status and progress of a multi-node operation based on the current statuses of its individual node tasks.
        /// Updates the <see cref="Operation.OverallStatus"/>, <see cref="Operation.ProgressPercent"/>, and <see cref="Operation.StatusMessage"/>.
        /// If all tasks are terminal, it sets the operation's end time and attempts to complete the <see cref="ActiveOperationContext.OperationCompletionSource"/>.
        /// </summary>
        /// <param name="opContext">The context of the active multi-node operation.</param>
        private async Task RecalculateOperationStatusAsync(ActiveOperationContext opContext)
        {
            var operation = opContext.Operation;
            var tasks = operation.NodeTasks;

            if (!tasks.Any())
            {
                operation.OverallStatus = OperationOverallStatus.Succeeded; // No tasks means success by default? Or should be an error?
                operation.ProgressPercent = 100;
                operation.StatusMessage = "Operation has no tasks to execute.";
                 _logger.LogWarning("RecalculateOperationStatusAsync called for OpId {OperationId} with no tasks.", operation.Id);
            }
            else
            {
                int totalTasks = tasks.Count;
                int succeededTasks = tasks.Count(t => t.Status == NodeTaskStatus.Succeeded || t.Status == NodeTaskStatus.SucceededWithIssues);
                int failedTasks = tasks.Count(t => t.Status == NodeTaskStatus.Failed || t.Status == NodeTaskStatus.TaskDispatchFailed_Execute || t.Status == NodeTaskStatus.DispatchFailed_Prepare || t.Status == NodeTaskStatus.NodeOfflineDuringTask || t.Status == NodeTaskStatus.TimedOut || t.Status == NodeTaskStatus.ReadinessCheckTimedOut || t.Status == NodeTaskStatus.NotReadyForTask);
                int cancelledTasks = tasks.Count(t => t.Status == NodeTaskStatus.Cancelled || t.Status == NodeTaskStatus.CancellationFailed);
                int terminalTasks = tasks.Count(t => t.Status.IsTerminal());
                int nonTerminalTasks = totalTasks - terminalTasks;

                // Calculate average progress for UI display, considering only non-terminal tasks if any, else 100 if all terminal
                operation.ProgressPercent = nonTerminalTasks > 0
                    ? (int)tasks.Where(t => !t.Status.IsTerminal()).Average(t => t.ProgressPercent)
                    : 100;
                
                operation.StatusMessage = $"Tasks: Total={totalTasks}, Succeeded={succeededTasks}, Failed={failedTasks}, Cancelled={cancelledTasks}, InProgress/Pending={nonTerminalTasks}.";

                if (terminalTasks == totalTasks) // All tasks have reached a terminal state
                {
                    if (tasks.All(t => t.Status == NodeTaskStatus.Succeeded || t.Status == NodeTaskStatus.SucceededWithIssues))
                        operation.OverallStatus = OperationOverallStatus.Succeeded;
                    else if (tasks.Any(t => t.Status == NodeTaskStatus.Cancelled || t.Status == NodeTaskStatus.Cancelling)) // Check for Cancelling as well if cancellation was initiated
                        operation.OverallStatus = OperationOverallStatus.Cancelled;
                    else if (tasks.Any(t => t.Status == NodeTaskStatus.SucceededWithIssues) && !tasks.Any(t=> t.Status == NodeTaskStatus.Failed)) // Only SucceededWithIssues and Succeeded
                        operation.OverallStatus = OperationOverallStatus.SucceededWithErrors;
                    else // Any other mix including Failed, TimedOut etc.
                        operation.OverallStatus = OperationOverallStatus.Failed;
                    
                    operation.EndTime = DateTime.UtcNow;
                    _logger.LogInformation("OpId {OperationId}: All tasks ({TotalTasks}) are terminal. OverallStatus: {OverallStatus}",
                        operation.Id, totalTasks, operation.OverallStatus);
                }
                else // Still tasks running/pending
                {
                    // Determine a sensible ongoing status. If any task is Failing/Retrying, that might be the overall status.
                    // For simplicity, keep as InProgress if not all terminal, unless specific logic for other ongoing states is added.
                    if (operation.OverallStatus != OperationOverallStatus.Cancelling) // Don't override if cancellation is in progress
                    {
                         operation.OverallStatus = OperationOverallStatus.InProgress;
                    }
                }
            }
            
            // Report stage progress to the MasterActionContext
            opContext.ProgressReporter.Report(new StageProgress
            {
                ProgressPercent = operation.ProgressPercent,
                StatusMessage = operation.StatusMessage
            });
            
            // If the operation has reached a terminal state, try to set the result for the ExecuteAsync method.
            if (operation.OverallStatus.IsCompleted())
            {
                var result = new MultiNodeOperationResult
                {
                    IsSuccess = (operation.OverallStatus == OperationOverallStatus.Succeeded || operation.OverallStatus == OperationOverallStatus.SucceededWithErrors),
                    FinalOperationState = operation
                };
                bool resultSet = opContext.OperationCompletionSource.TrySetResult(result);
                if (resultSet)
                {
                    _logger.LogInformation("OpId {OperationId}: OperationCompletionSource set to {OverallStatus}.", operation.Id, operation.OverallStatus);
                }
                else
                {
                    // This can happen if cancellation sets the TCS first, or if called multiple times rapidly.
                    _logger.LogDebug("OpId {OperationId}: Failed to set OperationCompletionSource; it might have been set already (e.g., by cancellation). Current TCS Status: {TcsStatus}",
                        operation.Id, opContext.OperationCompletionSource.Task.Status);
                }
            }
        }
        
        /// <summary>
        /// Monitors the readiness check phase for an operation. If nodes do not report readiness within a specified timeout,
        /// their corresponding tasks are marked as <see cref="NodeTaskStatus.ReadinessCheckTimedOut"/>.
        /// </summary>
        /// <param name="operationId">The ID of the operation whose readiness is being monitored.</param>
        /// <param name="timeout">The duration to wait for readiness reports before timing out.</param>
        private async Task MonitorReadinessTimeout(string operationId, TimeSpan timeout)
        {
            await Task.Delay(timeout);

            if (_activeOperations.TryGetValue(operationId, out var opContext) &&
                (opContext.Operation.OverallStatus == OperationOverallStatus.AwaitingNodeReadiness ||
                 opContext.Operation.NodeTasks.Any(t=> t.Status == NodeTaskStatus.ReadinessCheckSent) )) // Check if still awaiting or some tasks are stuck
            {
                _logger.LogWarning("Readiness check timed out for operation {OperationId} after {TotalSeconds}s.", operationId, timeout.TotalSeconds);
                bool changed = false;
                foreach (var task in opContext.Operation.NodeTasks.Where(t => t.Status == NodeTaskStatus.ReadinessCheckSent))
                {
                    task.Status = NodeTaskStatus.ReadinessCheckTimedOut;
                    task.StatusMessage = $"Agent did not respond to readiness check within the {timeout.TotalSeconds}s timeout period.";
                    task.EndTime = DateTime.UtcNow; // Mark as terminal
                    task.LastUpdateTime = DateTime.UtcNow;
                    changed = true;
                    _logger.LogWarning("Task {TaskId} on Node {NodeName} for OpId {OperationId} marked as ReadinessCheckTimedOut.", task.TaskId, task.NodeName, operationId);
                }
                if(changed) await RecalculateOperationStatusAsync(opContext);
            }
        }
        
        /// <summary>
        /// Asynchronously consumes log entries from an operation's dedicated <see cref="Channel{T}"/>
        /// and journals them using <see cref="IJournalService.AppendToStageLogAsync"/>.
        /// This runs as a background task for the duration of an active multi-node operation.
        /// </summary>
        /// <param name="opContext">The context of the active multi-node operation, containing the log channel.</param>
        private async Task ConsumeLogChannelAsync(ActiveOperationContext opContext)
        {
            _logger.LogDebug("Log consumer started for OpId: {OperationId}", opContext.Operation.Id);
            try
            {
                await foreach (var logEntry in opContext.LogChannel.Reader.ReadAllAsync(opContext.ParentMasterActionContext.CancellationToken))
                {
                    try
                    {
                        await _journalService.AppendToStageLogAsync(opContext.ParentMasterActionContext.MasterActionId, logEntry);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error writing slave log from channel to journal for OpId {OperationId}, TaskId {TaskId}.",
                            logEntry.OperationId, logEntry.TaskId);
                        // Optionally, consider a retry mechanism or dead-letter queue for failed journal writes.
                    }
                }
            }
            catch (OperationCanceledException)
            {
                 _logger.LogInformation("Log channel consumer for OpId {OperationId} cancelled.", opContext.Operation.Id);
            }
            catch (ChannelClosedException)
            {
                 _logger.LogInformation("Log channel consumer for OpId {OperationId} finished as channel was closed.", opContext.Operation.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Log channel consumer for OpId {OperationId} failed unexpectedly.", opContext.Operation.Id);
            }
            _logger.LogDebug("Log consumer finished for OpId: {OperationId}", opContext.Operation.Id);
        }

        /// <summary>
        /// Ensures all buffered logs from participating slave agents for a completed or cancelled operation are requested for flushing,
        /// waits for their confirmation or a timeout, and then ensures all received logs are written to the journal
        /// by awaiting the completion of the log consumer channel for the operation.
        /// </summary>
        /// <param name="operationResult">The <see cref="MultiNodeOperationResult"/> containing the final state of the operation, used to identify participating nodes.</param>
        /// <param name="masterActionContext">The <see cref="MasterActionContext"/> of the parent master action, used for logging informational messages about the flush process.</param>
        private async Task FlushAllNodeLogsAsync(MultiNodeOperationResult operationResult, MasterActionContext masterActionContext)
        {
            var operation = operationResult.FinalOperationState;
            var allParticipatingNodes = operation.NodeTasks.Select(t => t.NodeName).Distinct().ToList();

            if (!allParticipatingNodes.Any())
            {
                masterActionContext.LogInfo("No participating nodes in operation {OperationId} to flush logs from.", operation.Id);
                // Still ensure any master-side logs for this opContext are flushed from its channel
                if (_activeOperations.TryGetValue(operation.Id, out var opCtxForEmptyOp) && opCtxForEmptyOp != null)
                {
                    opCtxForEmptyOp.LogChannel.Writer.TryComplete();
                    await opCtxForEmptyOp.LogChannel.Reader.Completion;
                }
                return;
            }

            if (!_activeOperations.TryGetValue(operation.Id, out var opContext) || opContext == null)
            {
                masterActionContext.LogError(null, $"Cannot flush logs for operation {operation.Id}: ActiveOperationContext not found. This might indicate the operation was already cleaned up.");
                return;
            }

            var onlineNodesToFlush = new List<string>();
            foreach (var nodeName in allParticipatingNodes)
            {
                var nodeState = await _nodeHealthMonitorService.GetNodeCachedStateAsync(nodeName);
                if (nodeState?.ConnectivityStatus == AgentConnectivityStatus.Online)
                {
                    onlineNodesToFlush.Add(nodeName);
                }
                else
                {
                     masterActionContext.LogInfo($"Node '{nodeName}' is not online (Status: {nodeState?.ConnectivityStatus}). Skipping log flush request for this node for OpId {operation.Id}.");
                }
            }

            if (onlineNodesToFlush.Any())
            {
                masterActionContext.LogInfo($"Operation {operation.Id} stage complete. Requesting log flush from {onlineNodesToFlush.Count} online node(s): {string.Join(", ", onlineNodesToFlush)}...");
                foreach (var nodeName in onlineNodesToFlush)
                {
                    await _agentConnectionManager.RequestLogFlushForTask(nodeName, operation.Id);
                }

                var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // Configurable timeout for log flush confirmations
                try
                {
                    // Wait until all *expected online* nodes have confirmed, or timeout
                    while (opContext.ConfirmedLogFlushNodes.Count < onlineNodesToFlush.Count && !timeoutCts.IsCancellationRequested)
                    {
                        await Task.Delay(250, timeoutCts.Token);
                    }

                    if (timeoutCts.IsCancellationRequested)
                    {
                         masterActionContext.LogWarning($"Timed out waiting for all {onlineNodesToFlush.Count} online nodes to confirm log flush for operation {operation.Id}. Received: {opContext.ConfirmedLogFlushNodes.Count}. Missing: {string.Join(", ", onlineNodesToFlush.Except(opContext.ConfirmedLogFlushNodes))}");
                    }
                    else
                    {
                        masterActionContext.LogInfo($"All {onlineNodesToFlush.Count} online nodes have confirmed their logs have been sent for operation {operation.Id}.");
                    }
                }
                catch (TaskCanceledException) // Catches cancellation from timeoutCts
                {
                     masterActionContext.LogWarning($"Log flush confirmation wait was cancelled (likely timed out) for operation {operation.Id}. Received: {opContext.ConfirmedLogFlushNodes.Count} of {onlineNodesToFlush.Count}.");
                }
                finally
                {
                    timeoutCts.Dispose();
                }
            }
            else
            {
                masterActionContext.LogInfo($"No online nodes to flush logs from for operation {operation.Id}. Skipping wait for confirmations.");
            }
  
            masterActionContext.LogInfo($"Completing log channel for operation {operation.Id}. This will allow the log consumer to finish.");
            opContext.LogChannel.Writer.TryComplete(); // Mark the channel as "complete for writing".

            // Wait for the channel reader's Completion task. This ensures all logs already in the channel are processed.
            await opContext.LogChannel.Reader.Completion;
            masterActionContext.LogInfo($"All received logs for operation {operation.Id} have been processed by the journaler.");
        }

        /// <summary>
        /// Handles an incoming log entry from a slave agent by queuing it to the appropriate active operation's log channel.
        /// This method is typically called by a service that processes messages from the <see cref="AgentHub"/>.
        /// </summary>
        /// <param name="logEntry">The <see cref="SlaveTaskLogEntry"/> DTO containing the log details from the slave.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous queuing of the log entry.</returns>
        public Task JournalSlaveLogAsync(SlaveTaskLogEntry logEntry)
        {
            string? internalOpId = null;
            if (!string.IsNullOrEmpty(logEntry.TaskId) && _taskIdToOperationIdMap.TryGetValue(logEntry.TaskId, out var mappedOpId))
            {
                internalOpId = mappedOpId;
            }
            else if (!string.IsNullOrEmpty(logEntry.OperationId)) // Fallback if TaskId is not in map but OperationId might be known
            {
                // This assumes logEntry.OperationId directly matches an active internalOpId, which might be true for some general slave logs not tied to a specific task.
                if (_activeOperations.ContainsKey(logEntry.OperationId))
                {
                    internalOpId = logEntry.OperationId;
                }
            }

            if (internalOpId != null && _activeOperations.TryGetValue(internalOpId, out var opContext))
            {
                _logger.LogTrace("OpId {OpId}, TaskId {TaskId}: Queuing slave log entry to internal channel.", internalOpId, logEntry.TaskId ?? "N/A");
                if (!opContext.LogChannel.Writer.TryWrite(logEntry))
                {
                    _logger.LogWarning("Could not write log to channel for OpId {OpId}, TaskId {TaskId}; channel may be closed or full.", internalOpId, logEntry.TaskId ?? "N/A");
                }
            }
            else
            {
                 _logger.LogWarning("Could not journal slave log: No active operation context found for OperationId '{OperationId}' / TaskId '{TaskId}'. Log from Node '{NodeName}': {Message}",
                    logEntry.OperationId, logEntry.TaskId ?? "N/A", logEntry.NodeName, logEntry.LogMessage);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Processes a log flush confirmation received from a slave agent via the <see cref="AgentHub"/>.
        /// This method updates the tracking for an active multi-node operation, potentially completing its log flush phase.
        /// </summary>
        /// <param name="operationId">The unique identifier of the operation for which logs were flushed. This is the internal <see cref="Operation.Id"/>.</param>
        /// <param name="nodeName">The name of the slave node that confirmed the log flush.</param>
        public void ConfirmLogFlush(string operationId, string nodeName)
        {
            if (_activeOperations.TryGetValue(operationId, out var opContext))
            {
                opContext.ConfirmedLogFlushNodes.Add(nodeName);
                _logger.LogDebug("Received log flush confirmation from node {NodeName} for operation {OperationId}.", nodeName, operationId);

                // Check if all *expected online* nodes have confirmed.
                // This requires knowing which nodes were online when flush was requested.
                // For simplicity here, we might assume all nodes in the operation were targeted if online.
                // A more robust check would compare against the list of nodes to which RequestLogFlushForTask was actually sent.
                var onlineNodesInOperation = opContext.Operation.NodeTasks
                    .Select(t => t.NodeName)
                    .Distinct()
                    .Where(nn => _nodeHealthMonitorService.GetNodeCachedStateAsync(nn).Result?.ConnectivityStatus == AgentConnectivityStatus.Online)
                    .ToList();

                // If the count of confirmed nodes is greater than or equal to the count of nodes that were online when flush was requested.
                if (opContext.ConfirmedLogFlushNodes.Count >= onlineNodesInOperation.Count)
                {
                    _logger.LogInformation("All expected online nodes ({Count}) have confirmed log flush for operation {OperationId}.", onlineNodesInOperation.Count, operationId);
                    opContext.LogFlushCompletionSource.TrySetResult();
                }
            }
            else
            {
                _logger.LogWarning("Received log flush confirmation for an unknown or already completed operation: {OperationId} from node {NodeName}", operationId, nodeName);
            }
        }

        /// <summary>
        /// Monitors a single dispatched task for execution timeout. If a timeout occurs, the task's status is updated
        /// to <see cref="NodeTaskStatus.TimedOut"/>, and the overall operation status is recalculated.
        /// </summary>
        /// <param name="opContext">The context of the active multi-node operation containing the task.</param>
        /// <param name="taskId">The ID of the specific task to monitor.</param>
        /// <param name="timeout">The duration to wait before considering the task timed out.</param>
        private async Task MonitorExecutionTimeoutAsync(ActiveOperationContext opContext, string taskId, TimeSpan timeout)
        {
            await Task.Delay(timeout); // Wait for the timeout period

            // Re-check if the operation is still active and the task exists.
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
                    task.LastUpdateTime = DateTime.UtcNow;
                    await RecalculateOperationStatusAsync(currentOpContext);
                }
            }
        }    }
}
