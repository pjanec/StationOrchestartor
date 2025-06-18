namespace SiteKeeper.Master.Services
{
    // Using directives required by NodeCoordinator
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
    using System.Text.Json.Serialization;
    using SiteKeeper.Shared.DTOs.AgentHub;
    using System.Threading.Channels;
    using NLog; // Added for MappedDiagnosticsLogicalContext

    public class NodeCoordinator : SiteKeeper.Master.Abstractions.Workflow.INodeCoordinator<SiteKeeper.Master.Abstractions.Workflow.NodeActionResult>
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

        private class ActiveActionContext
        {
            public NodeAction ExecutingAction { get; }
            public MasterActionContext ParentMasterActionContext { get; }
            public TaskCompletionSource<NodeActionResult> ActionCompletionSource { get; }
            public ConcurrentBag<string> ConfirmedLogFlushNodes { get; }
            public TaskCompletionSource LogFlushCompletionSource { get; }
            public IProgress<StageProgress> ProgressReporter { get; }
			public Channel<SlaveTaskLogEntry> LogChannel { get; }

            public ActiveActionContext(NodeAction action, IProgress<StageProgress> progress, MasterActionContext parentMasterActionContext)
            {
                ExecutingAction = action;
                ProgressReporter = progress;
                ParentMasterActionContext = parentMasterActionContext;
                ActionCompletionSource = new TaskCompletionSource<NodeActionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                ConfirmedLogFlushNodes = new ConcurrentBag<string>();
                LogFlushCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                LogChannel = Channel.CreateUnbounded<SlaveTaskLogEntry>(new UnboundedChannelOptions { SingleReader = true });
            }
        }

        private readonly ILogger<NodeCoordinator> _logger; // Logger for NodeCoordinator
        private readonly IAgentConnectionManagerService _agentConnectionManager;
        private readonly IJournalService _journalService;
        private readonly INodeHealthMonitorService _nodeHealthMonitorService;

        private readonly ConcurrentDictionary<string, ActiveActionContext> _activeActions = new();
        private readonly ConcurrentDictionary<string, string> _taskIdToActionIdMap = new();

        public NodeCoordinator(
            ILogger<NodeCoordinator> logger,
            IAgentConnectionManagerService agentConnectionManager,
            IJournalService journalService,
            INodeHealthMonitorService nodeHealthMonitorService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _agentConnectionManager = agentConnectionManager ?? throw new ArgumentNullException(nameof(agentConnectionManager));
            _journalService = journalService ?? throw new ArgumentNullException(nameof(journalService));
            _nodeHealthMonitorService = nodeHealthMonitorService ?? throw new ArgumentNullException(nameof(nodeHealthMonitorService));
        }

        public async Task<NodeActionResult> ExecuteAsync(
            NodeAction actionToExecuteParam,
            MasterActionContext masterActionContext,
            IProgress<StageProgress> progress,
            CancellationToken cancellationToken)
        {
            var currentAction = actionToExecuteParam;

            if (currentAction == null || !currentAction.NodeTasks.Any())
            {
                masterActionContext.LogInfo("NodeCoordinator received no tasks to execute. Stage succeeded immediately.");
                var fallbackAction = currentAction ?? new NodeAction("empty-action", OperationType.NoOp, masterActionContext.MasterActionId);
                return new NodeActionResult { IsSuccess = true, FinalActionState = fallbackAction };
            }

            var activeCtx = new ActiveActionContext(currentAction, progress, masterActionContext);

            if (!_activeActions.TryAdd(currentAction.Id, activeCtx))
            {
                _logger.LogError("Failed to add action {ActionId} to active actions; an action with this ID may already be running.", currentAction.Id);
                throw new InvalidOperationException($"An action with the internal ID {currentAction.Id} is already running or failed to be added to tracking.");
            }

            foreach (var task in currentAction.NodeTasks)
            {
                _taskIdToActionIdMap[task.TaskId] = currentAction.Id;
            }

            _logger.LogInformation("Starting node action: {ActionType} (ActionId: {ActionId}) for MasterAction {MasterActionId}",
                currentAction.Type, currentAction.Id, masterActionContext.MasterActionId);

            using (MappedDiagnosticsLogicalContext.SetScoped("SK-ActionId", currentAction.Id))
            {
                var logConsumerTask = ConsumeLogChannelAsync(activeCtx);

                var healthMonitoringCts = new CancellationTokenSource();
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, healthMonitoringCts.Token);
                _ = MonitorNodeHealthForActionAsync(activeCtx, linkedCts.Token);

                using (cancellationToken.Register(() =>
                       {
                           _logger.LogWarning("Parent MasterAction cancellation requested for node action {ActionId}.", currentAction.Id);
                           activeCtx.ActionCompletionSource.TrySetCanceled();
                       }
                ))
                {
                    try
                    {
                        await StartReadinessCheckAndDispatchSequenceAsync(currentAction);
                        var nodeActionResult = await activeCtx.ActionCompletionSource.Task;

                        _logger.LogInformation("Node action {ActionId} reached terminal state: {Status}. Preparing to flush logs.", currentAction.Id, nodeActionResult.FinalActionState.OverallStatus);
                        await FlushAllNodeLogsAsync(nodeActionResult, masterActionContext);

                        _logger.LogInformation("Log flushing complete for {ActionId}. Returning final result.", currentAction.Id);
                        return nodeActionResult;
                    }
                    catch (TaskCanceledException)
                    {
                         _logger.LogWarning("Node action {ActionId} was cancelled. Handling cancellation logic...", currentAction.Id);
                        currentAction.OverallStatus = OperationOverallStatus.Cancelling;

                        var tasksToCancel = currentAction.NodeTasks.Where(t => !t.Status.IsTerminal()).ToList();
                        _logger.LogInformation("Identified {Count} non-terminal tasks to process for cancellation for ActionId {ActionId}.", tasksToCancel.Count, currentAction.Id);

                        foreach (var task in tasksToCancel)
                        {
                            var nodeState = await _nodeHealthMonitorService.GetNodeCachedStateAsync(task.NodeName);
                            if (nodeState?.ConnectivityStatus is AgentConnectivityStatus.Offline or AgentConnectivityStatus.Unreachable)
                            {
                                _logger.LogInformation("Task {TaskId} on offline/unreachable node {NodeName} for ActionId {ActionId} marked as Cancelled.", task.TaskId, task.NodeName, currentAction.Id);
                                task.Status = NodeTaskStatus.Cancelled;
                                task.StatusMessage = "Task cancelled; target node was offline or unreachable.";
                                task.EndTime = DateTime.UtcNow;
                            }
                            else
                            {
                                _logger.LogInformation("Requesting cancellation for task {TaskId} on online node {NodeName} for ActionId {ActionId}.", task.TaskId, task.NodeName, currentAction.Id);
                                task.Status = NodeTaskStatus.Cancelling;
                                _ = _agentConnectionManager.SendCancelTaskAsync(task.NodeName, new CancelTaskOnAgentRequest { ActionId = currentAction.Id, TaskId = task.TaskId, Reason = "Action cancelled by master." });
                            }
                        }

                        await MonitorCancellationCompletionAsync(activeCtx, TimeSpan.FromSeconds(15));

                        foreach (var task in currentAction.NodeTasks.Where(t => t.Status == NodeTaskStatus.Cancelling))
                        {
                            _logger.LogInformation("Finalizing status for task {TaskId} (ActionId {ActionId}) from 'Cancelling' to 'Cancelled' after monitor.", task.TaskId, currentAction.Id);
                            task.Status = NodeTaskStatus.Cancelled;
                            task.StatusMessage = task.StatusMessage ?? "Task cancellation finalized by master after monitoring period.";
                            task.EndTime = DateTime.UtcNow;
                        }

                        currentAction.OverallStatus = OperationOverallStatus.Cancelled;
                        currentAction.EndTime = DateTime.UtcNow;
                        masterActionContext.LogWarning($"Node action {currentAction.Id} for MasterAction {masterActionContext.MasterActionId} was cancelled.");

                        var cancelledResult = new NodeActionResult { IsSuccess = false, FinalActionState = currentAction };

                        await FlushAllNodeLogsAsync(cancelledResult, masterActionContext);
                        activeCtx.ActionCompletionSource.TrySetResult(cancelledResult);
                        return cancelledResult;
                    }
                    catch(Exception ex)
                    {
                        _logger.LogError(ex, "Unhandled exception during ExecuteAsync for node action {ActionId}.", currentAction.Id);
                        currentAction.OverallStatus = OperationOverallStatus.Failed;
                        currentAction.StatusMessage = $"Node coordinator failed: {ex.Message}";
                        currentAction.EndTime = DateTime.UtcNow;
                        var failedResult = new NodeActionResult { IsSuccess = false, FinalActionState = currentAction };
                        await FlushAllNodeLogsAsync(failedResult, masterActionContext);
                        activeCtx.ActionCompletionSource.TrySetResult(failedResult);
                        throw;
                    }
                    finally
                    {
                        _logger.LogDebug("Initiating cleanup for node action {ActionId} in ExecuteAsync.finally.", currentAction.Id);
                        healthMonitoringCts.Cancel();
                        _activeActions.TryRemove(currentAction.Id, out _);

                        foreach (var task in currentAction.NodeTasks)
                        {
                            _taskIdToActionIdMap.TryRemove(task.TaskId, out _);
                        }

                        activeCtx.LogChannel.Writer.TryComplete();
                        await logConsumerTask;

                        _logger.LogInformation("Node action {ActionId} (MasterAction {MasterActionId}) finished execution and cleanup.",
                            currentAction.Id, masterActionContext.MasterActionId);
                    }
                }
            }
        }

        private async Task MonitorNodeHealthForActionAsync(ActiveActionContext activeCtx, CancellationToken cancellationToken)
        {
            var currentAction = activeCtx.ExecutingAction;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);

                    var activeTasks = currentAction.NodeTasks.Where(t => !t.Status.IsTerminal()).ToList();
                    if (!activeTasks.Any())
                    {
                        _logger.LogDebug("No active tasks remaining for health monitoring in ActionId {ActionId}. Exiting monitor loop.", currentAction.Id);
                        return;
                    }

                    foreach (var task in activeTasks)
                    {
                        var nodeState = await _nodeHealthMonitorService.GetNodeCachedStateAsync(task.NodeName);
                        if (nodeState?.ConnectivityStatus is AgentConnectivityStatus.Offline or AgentConnectivityStatus.Unreachable)
                        {
                            _logger.LogError("Node '{NodeName}' disconnected or became unreachable during active task {TaskId} for ActionId {ActionId}. Failing the task.",
                                task.NodeName, task.TaskId, currentAction.Id);
                            task.Status = NodeTaskStatus.NodeOfflineDuringTask;
                            task.StatusMessage = $"Node went offline or became unreachable during task execution. Last known connectivity: {nodeState.ConnectivityStatus}.";
                            task.EndTime = DateTime.UtcNow;
                            await RecalculateActionStatusAsync(activeCtx);
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("Node health monitoring loop for ActionId {ActionId} was cancelled.", currentAction.Id);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in node health monitoring loop for action {ActionId}.", currentAction.Id);
                }
            }
             _logger.LogInformation("Exited node health monitoring loop for ActionId {ActionId} due to cancellation request.", currentAction.Id);
        }

        private async Task MonitorCancellationCompletionAsync(ActiveActionContext activeCtx, TimeSpan timeout)
        {
            var currentAction = activeCtx.ExecutingAction;
            var timeoutCts = new CancellationTokenSource(timeout);
            _logger.LogInformation("Monitoring for cancellation confirmation from nodes for ActionId: {ActionId}", currentAction.Id);

            try
            {
                while (!timeoutCts.IsCancellationRequested)
                {
                    var tasksStillCancelling = currentAction.NodeTasks.Where(t => t.Status == NodeTaskStatus.Cancelling).ToList();

                    if (!tasksStillCancelling.Any())
                    {
                        _logger.LogInformation("All tasks have moved past 'Cancelling' state for ActionId {ActionId}.", currentAction.Id);
                        return;
                    }

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
                        _logger.LogInformation("All tasks still in 'Cancelling' state for ActionId {ActionId} are on offline/unreachable nodes. Proceeding with cancellation finalization.", currentAction.Id);
                        return;
                    }
                    await Task.Delay(500, timeoutCts.Token);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Timeout reached while waiting for cancellation confirmation for ActionId {ActionId}.", currentAction.Id);
            }
            finally
            {
                timeoutCts.Dispose();
            }

            var tasksLeftCancelling = currentAction.NodeTasks.Where(t => t.Status == NodeTaskStatus.Cancelling).ToList();
            if (tasksLeftCancelling.Any())
            {
                _logger.LogWarning("{Count} tasks still in 'Cancelling' state after monitoring period for ActionId {ActionId}. Forcibly marking them based on node status.",
                    tasksLeftCancelling.Count, currentAction.Id);
                foreach (var task in tasksLeftCancelling)
                {
                    var nodeState = await _nodeHealthMonitorService.GetNodeCachedStateAsync(task.NodeName);
                    if (nodeState?.ConnectivityStatus is AgentConnectivityStatus.Offline or AgentConnectivityStatus.Unreachable)
                    {
                        task.Status = NodeTaskStatus.Cancelled;
                        task.StatusMessage = "Task cancellation confirmed as node became unresponsive.";
                    }
                    else
                    {
                        task.Status = NodeTaskStatus.Cancelled;
                        task.StatusMessage = "Node did not confirm cancellation within timeout; master marked as cancelled.";
                    }
                    task.EndTime = DateTime.UtcNow;
                }
            }
        }

        private async Task StartReadinessCheckAndDispatchSequenceAsync(NodeAction actionToProcess)
        {
            actionToProcess.StartTime = actionToProcess.StartTime ?? DateTime.UtcNow;
            actionToProcess.OverallStatus = OperationOverallStatus.AwaitingNodeReadiness;
            actionToProcess.StatusMessage = "Awaiting readiness reports from nodes.";

            if(_activeActions.TryGetValue(actionToProcess.Id, out var activeCtx))
            {
                activeCtx.ProgressReporter.Report(new StageProgress { ProgressPercent = 5, StatusMessage = "Initiating readiness checks..." });
            }

            _logger.LogInformation("ActionId {ActionId}: Starting readiness checks for {TaskCount} tasks.", actionToProcess.Id, actionToProcess.NodeTasks.Count);
            foreach (var task in actionToProcess.NodeTasks)
            {
                task.Status = NodeTaskStatus.AwaitingReadiness;
                string? prepParamsJson = null;
                if (task.TaskType == SlaveTaskType.TestOrchestration && task.TaskPayload != null)
                {
                    try
                    {
                        prepParamsJson = JsonSerializer.Serialize(task.TaskPayload, _jsonOptions);
                    }
                    catch(Exception ex)
                    {
                        _logger.LogError(ex, "Failed to serialize TaskPayload for TestOrchestration task {TaskId} on node {NodeName} for ActionId {ActionId}.", task.TaskId, task.NodeName, actionToProcess.Id);
                    }
                }

                var prepareInstruction = new PrepareForTaskInstruction
                {
                    ActionId = task.ActionId,
                    TaskId = task.TaskId,
                    ExpectedTaskType = task.TaskType,
                    PreparationParametersJson = prepParamsJson,
                    TargetResource = task.TargetResource
                };

                task.Status = NodeTaskStatus.ReadinessCheckSent;
                task.StatusMessage = "Prepare instruction sent, awaiting readiness report.";
                task.LastUpdateTime = DateTime.UtcNow;
                await _agentConnectionManager.SendPrepareForTaskInstructionAsync(task.NodeName, prepareInstruction);
                _logger.LogDebug("ActionId {ActionId}: Sent PrepareForTaskInstruction for TaskId {TaskId} to Node {NodeName}.", actionToProcess.Id, task.TaskId, task.NodeName);
            }
            _ = MonitorReadinessTimeoutAsync(actionToProcess.Id, TimeSpan.FromSeconds(_activeActions.TryGetValue(actionToProcess.Id, out var ctx) ? ctx.ExecutingAction.DefaultTaskTimeoutSeconds : 30));
        }

        public async Task ProcessSlaveTaskReadinessAsync(SlaveTaskReadinessReport readinessReport)
        {
            if (!_taskIdToActionIdMap.TryGetValue(readinessReport.TaskId, out var internalActionId) ||
                !_activeActions.TryGetValue(internalActionId, out var activeCtx))
            {
                _logger.LogWarning("Received readiness report for an unknown or completed task/action: TaskId {TaskId}, MappedActionId {InternalActionId}",
                    readinessReport.TaskId, internalActionId ?? "N/A");
                return;
            }

            var task = activeCtx.ExecutingAction.NodeTasks.FirstOrDefault(t => t.TaskId == readinessReport.TaskId);
            if (task == null)
            {
                _logger.LogWarning("Readiness report for unknown TaskId {TaskId} in active ActionId {ActionId}", readinessReport.TaskId, internalActionId);
                return;
            }

            if (task.Status.IsTerminal())
            {
                 _logger.LogInformation("Received readiness report for already terminal task {TaskId} with status {Status}. Ignoring.", task.TaskId, task.Status);
                return;
            }

            task.LastUpdateTime = DateTime.UtcNow;
            if (readinessReport.IsReady)
            {
                task.Status = NodeTaskStatus.ReadyToExecute;
                task.StatusMessage = "Agent reported ready for task execution.";
                _logger.LogInformation("Node {NodeName} is READY for task {TaskId} (ActionId {ActionId}). Dispatching task.",
                    task.NodeName, task.TaskId, internalActionId);

                var slaveTaskInstruction = new SlaveTaskInstruction
                {
                    ActionId = task.ActionId,
                    TaskId = task.TaskId,
                    TaskType = task.TaskType,
                    ParametersJson = task.TaskPayload != null ? JsonSerializer.Serialize(task.TaskPayload, _jsonOptions) : null,
                    TimeoutSeconds = task.TimeoutSeconds > 0 ? task.TimeoutSeconds : activeCtx.ExecutingAction.DefaultTaskTimeoutSeconds
                };

                await _agentConnectionManager.SendSlaveTaskAsync(task.NodeName, slaveTaskInstruction);
                task.Status = NodeTaskStatus.TaskDispatched;
                task.StatusMessage = "Task instruction dispatched to agent.";
                task.StartTime = DateTime.UtcNow;

                var executionTimeout = TimeSpan.FromSeconds(slaveTaskInstruction.TimeoutSeconds ?? activeCtx.ExecutingAction.DefaultTaskTimeoutSeconds);
                _ = MonitorExecutionTimeoutAsync(activeCtx, task.TaskId, executionTimeout);
            }
            else
            {
                task.Status = NodeTaskStatus.NotReadyForTask;
                task.StatusMessage = $"Agent reported NOT READY: {readinessReport.ReasonIfNotReady}";
                task.EndTime = DateTime.UtcNow;
                _logger.LogWarning("Node {NodeName} is NOT READY for task {TaskId} (ActionId {ActionId}). Reason: {Reason}",
                    task.NodeName, task.TaskId, internalActionId, readinessReport.ReasonIfNotReady);
            }
            await RecalculateActionStatusAsync(activeCtx);
        }

        public async Task ProcessTaskStatusUpdateAsync(SlaveTaskProgressUpdate statusUpdate)
        {
            if (!_taskIdToActionIdMap.TryGetValue(statusUpdate.TaskId, out var internalActionId) ||
                !_activeActions.TryGetValue(internalActionId, out var activeCtx))
            {
                _logger.LogWarning("Received task status update for an unknown or completed task/action: TaskId {TaskId}, MappedActionId {InternalActionId}",
                    statusUpdate.TaskId, internalActionId ?? "N/A");
                return;
            }

            var nodeTask = activeCtx.ExecutingAction.NodeTasks.FirstOrDefault(t => t.TaskId == statusUpdate.TaskId);
            if (nodeTask == null)
            {
                 _logger.LogWarning("Status update for unknown TaskId {TaskId} in active ActionId {ActionId}", statusUpdate.TaskId, internalActionId);
                return;
            }

            if (nodeTask.Status.IsTerminal() && statusUpdate.Status != nodeTask.Status.ToString())
            {
                 _logger.LogInformation("Received status update '{SlaveStatus}' for already terminal task {TaskId} (MasterStatus: {MasterStatus}). Ignoring update.",
                    statusUpdate.Status, nodeTask.TaskId, nodeTask.Status);
                return;
            }

            nodeTask.LastUpdateTime = statusUpdate.TimestampUtc;
            if (Enum.TryParse<NodeTaskStatus>(statusUpdate.Status, true, out var parsedStatus))
            {
                nodeTask.Status = parsedStatus;
            }
            else
            {
                _logger.LogWarning("Failed to parse NodeTaskStatus '{StatusString}' for TaskId {TaskId} in ActionId {ActionId}", statusUpdate.Status, nodeTask.TaskId, internalActionId);
            }
            nodeTask.ProgressPercent = statusUpdate.ProgressPercent ?? nodeTask.ProgressPercent;
            nodeTask.StatusMessage = statusUpdate.Message ?? nodeTask.StatusMessage;

            if (nodeTask.Status.IsTerminal())
            {
                nodeTask.EndTime = statusUpdate.TimestampUtc;
                if (!string.IsNullOrEmpty(statusUpdate.ResultJson))
                {
                    try
                    {
                        nodeTask.ResultPayload ??= new Dictionary<string, object>();
                        var deserializedResult = JsonSerializer.Deserialize<Dictionary<string, object>>(statusUpdate.ResultJson, _jsonOptions);
                        if (deserializedResult != null)
                        {
                            foreach(var kvp in deserializedResult) nodeTask.ResultPayload[kvp.Key] = kvp.Value;
                        }
                        _logger.LogInformation("Task {TaskId} (ActionId {ActionId}) completed with ResultPayload.", nodeTask.TaskId, internalActionId);
                    }
                    catch(Exception ex)
                    {
                         _logger.LogError(ex, "Failed to deserialize ResultJson for task {TaskId} (ActionId {ActionId}). JSON: {Json}", nodeTask.TaskId, internalActionId, statusUpdate.ResultJson);
                         nodeTask.ResultPayload ??= new Dictionary<string, object>();
                         nodeTask.ResultPayload["DeserializationError"] = $"Failed to parse ResultJson: {ex.Message}";
                    }
                }
                await _journalService.RecordNodeTaskResultAsync(activeCtx.ParentMasterActionContext, nodeTask);
            }
            await RecalculateActionStatusAsync(activeCtx);
        }

        private async Task RecalculateActionStatusAsync(ActiveActionContext activeCtx)
        {
            var currentAction = activeCtx.ExecutingAction;
            var tasks = currentAction.NodeTasks;

            if (!tasks.Any())
            {
                currentAction.OverallStatus = OperationOverallStatus.Succeeded;
                currentAction.ProgressPercent = 100;
                currentAction.StatusMessage = "NodeAction has no tasks to execute.";
                 _logger.LogWarning("RecalculateActionStatusAsync called for ActionId {ActionId} with no tasks.", currentAction.Id);
            }
            else
            {
                int totalTasks = tasks.Count;
                int succeededTasks = tasks.Count(t => t.Status == NodeTaskStatus.Succeeded || t.Status == NodeTaskStatus.SucceededWithIssues);
                int failedTasks = tasks.Count(t => t.Status == NodeTaskStatus.Failed || t.Status == NodeTaskStatus.TaskDispatchFailed_Execute || t.Status == NodeTaskStatus.DispatchFailed_Prepare || t.Status == NodeTaskStatus.NodeOfflineDuringTask || t.Status == NodeTaskStatus.TimedOut || t.Status == NodeTaskStatus.ReadinessCheckTimedOut || t.Status == NodeTaskStatus.NotReadyForTask);
                int cancelledTasks = tasks.Count(t => t.Status == NodeTaskStatus.Cancelled || t.Status == NodeTaskStatus.CancellationFailed);
                int terminalTasks = tasks.Count(t => t.Status.IsTerminal());
                int nonTerminalTasks = totalTasks - terminalTasks;

                currentAction.ProgressPercent = nonTerminalTasks > 0
                    ? (int)tasks.Where(t => !t.Status.IsTerminal()).Average(t => t.ProgressPercent)
                    : 100;

                currentAction.StatusMessage = $"Tasks: Total={totalTasks}, Succeeded={succeededTasks}, Failed={failedTasks}, Cancelled={cancelledTasks}, InProgress/Pending={nonTerminalTasks}.";

                if (terminalTasks == totalTasks)
                {
                    if (tasks.All(t => t.Status == NodeTaskStatus.Succeeded || t.Status == NodeTaskStatus.SucceededWithIssues))
                        currentAction.OverallStatus = OperationOverallStatus.Succeeded;
                    else if (tasks.Any(t => t.Status == NodeTaskStatus.Cancelled || t.Status == NodeTaskStatus.Cancelling))
                        currentAction.OverallStatus = OperationOverallStatus.Cancelled;
                    else if (tasks.Any(t => t.Status == NodeTaskStatus.SucceededWithIssues) && !tasks.Any(t=> t.Status == NodeTaskStatus.Failed))
                        currentAction.OverallStatus = OperationOverallStatus.SucceededWithErrors;
                    else
                        currentAction.OverallStatus = OperationOverallStatus.Failed;

                    currentAction.EndTime = DateTime.UtcNow;
                    _logger.LogInformation("ActionId {ActionId}: All tasks ({TotalTasks}) are terminal. OverallStatus: {OverallStatus}",
                        currentAction.Id, totalTasks, currentAction.OverallStatus);
                }
                else
                {
                    if (currentAction.OverallStatus != OperationOverallStatus.Cancelling)
                    {
                         currentAction.OverallStatus = OperationOverallStatus.InProgress;
                    }
                }
            }

            activeCtx.ProgressReporter.Report(new StageProgress
            {
                ProgressPercent = currentAction.ProgressPercent,
                StatusMessage = currentAction.StatusMessage
            });

            if (currentAction.OverallStatus.IsCompleted())
            {
                var result = new NodeActionResult
                {
                    IsSuccess = (currentAction.OverallStatus == OperationOverallStatus.Succeeded || currentAction.OverallStatus == OperationOverallStatus.SucceededWithErrors),
                    FinalActionState = currentAction
                };
                bool resultSet = activeCtx.ActionCompletionSource.TrySetResult(result);
                if (resultSet)
                {
                    _logger.LogInformation("ActionId {ActionId}: ActionCompletionSource set to {OverallStatus}.", currentAction.Id, currentAction.OverallStatus);
                }
                else
                {
                    _logger.LogDebug("ActionId {ActionId}: Failed to set ActionCompletionSource; it might have been set already. Current TCS Status: {TcsStatus}",
                        currentAction.Id, activeCtx.ActionCompletionSource.Task.Status);
                }
            }
        }

        private async Task MonitorReadinessTimeoutAsync(string actionId, TimeSpan timeout)
        {
            await Task.Delay(timeout);

            if (_activeActions.TryGetValue(actionId, out var activeCtx) &&
                (activeCtx.ExecutingAction.OverallStatus == OperationOverallStatus.AwaitingNodeReadiness ||
                 activeCtx.ExecutingAction.NodeTasks.Any(t=> t.Status == NodeTaskStatus.ReadinessCheckSent) ))
            {
                _logger.LogWarning("Readiness check timed out for action {ActionId} after {TotalSeconds}s.", actionId, timeout.TotalSeconds);
                bool changed = false;
                foreach (var task in activeCtx.ExecutingAction.NodeTasks.Where(t => t.Status == NodeTaskStatus.ReadinessCheckSent))
                {
                    task.Status = NodeTaskStatus.ReadinessCheckTimedOut;
                    task.StatusMessage = $"Agent did not respond to readiness check within the {timeout.TotalSeconds}s timeout period.";
                    task.EndTime = DateTime.UtcNow;
                    task.LastUpdateTime = DateTime.UtcNow;
                    changed = true;
                    _logger.LogWarning("Task {TaskId} on Node {NodeName} for ActionId {ActionId} marked as ReadinessCheckTimedOut.", task.TaskId, task.NodeName, actionId);
                }
                if(changed) await RecalculateActionStatusAsync(activeCtx);
            }
        }

        private async Task ConsumeLogChannelAsync(ActiveActionContext activeCtx)
        {
            var currentActionId = activeCtx.ExecutingAction.Id;
            _logger.LogDebug("Log consumer started for ActionId: {ActionId}", currentActionId);
            try
            {
                await foreach (var logEntry in activeCtx.LogChannel.Reader.ReadAllAsync(activeCtx.ParentMasterActionContext.CancellationToken))
                {
                    try
                    {
                        await _journalService.AppendToStageLogAsync(activeCtx.ParentMasterActionContext.MasterActionId, logEntry);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error writing slave log from channel to journal for ActionId {ActionId}, TaskId {TaskId}.",
                            logEntry.ActionId, logEntry.TaskId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                 _logger.LogInformation("Log channel consumer for ActionId {ActionId} cancelled.", currentActionId);
            }
            catch (ChannelClosedException)
            {
                 _logger.LogInformation("Log channel consumer for ActionId {ActionId} finished as channel was closed.", currentActionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Log channel consumer for ActionId {ActionId} failed unexpectedly.", currentActionId);
            }
            _logger.LogDebug("Log consumer finished for ActionId: {ActionId}", currentActionId);
        }

        private async Task FlushAllNodeLogsAsync(NodeActionResult actionResult, MasterActionContext masterActionContext)
        {
            var currentAction = actionResult.FinalActionState;
            var allParticipatingNodes = currentAction.NodeTasks.Select(t => t.NodeName).Distinct().ToList();

            if (!allParticipatingNodes.Any())
            {
                masterActionContext.LogInfo("No participating nodes in action {ActionId} to flush logs from.", currentAction.Id);
                if (_activeActions.TryGetValue(currentAction.Id, out var activeCtxForEmpty) && activeCtxForEmpty != null)
                {
                    activeCtxForEmpty.LogChannel.Writer.TryComplete();
                    await activeCtxForEmpty.LogChannel.Reader.Completion;
                }
                return;
            }

            if (!_activeActions.TryGetValue(currentAction.Id, out var activeCtx) || activeCtx == null)
            {
                masterActionContext.LogError(null, $"Cannot flush logs for action {currentAction.Id}: ActiveActionContext not found.");
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
                     masterActionContext.LogInfo($"Node '{nodeName}' is not online (Status: {nodeState?.ConnectivityStatus}). Skipping log flush request for this node for ActionId {currentAction.Id}.");
                }
            }

            if (onlineNodesToFlush.Any())
            {
                masterActionContext.LogInfo($"Action {currentAction.Id} stage complete. Requesting log flush from {onlineNodesToFlush.Count} online node(s): {string.Join(", ", onlineNodesToFlush)}...");
                foreach (var nodeName in onlineNodesToFlush)
                {
                    await _agentConnectionManager.RequestLogFlushForTask(nodeName, currentAction.Id);
                }

                var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try
                {
                    while (activeCtx.ConfirmedLogFlushNodes.Count < onlineNodesToFlush.Count && !timeoutCts.IsCancellationRequested)
                    {
                        await Task.Delay(250, timeoutCts.Token);
                    }

                    if (timeoutCts.IsCancellationRequested)
                    {
                         masterActionContext.LogWarning($"Timed out waiting for all {onlineNodesToFlush.Count} online nodes to confirm log flush for action {currentAction.Id}. Received: {activeCtx.ConfirmedLogFlushNodes.Count}. Missing: {string.Join(", ", onlineNodesToFlush.Except(activeCtx.ConfirmedLogFlushNodes))}");
                    }
                    else
                    {
                        masterActionContext.LogInfo($"All {onlineNodesToFlush.Count} online nodes have confirmed their logs have been sent for action {currentAction.Id}.");
                    }
                }
                catch (TaskCanceledException)
                {
                     masterActionContext.LogWarning($"Log flush confirmation wait was cancelled (likely timed out) for action {currentAction.Id}. Received: {activeCtx.ConfirmedLogFlushNodes.Count} of {onlineNodesToFlush.Count}.");
                }
                finally
                {
                    timeoutCts.Dispose();
                }
            }
            else
            {
                masterActionContext.LogInfo($"No online nodes to flush logs from for action {currentAction.Id}. Skipping wait for confirmations.");
            }

            masterActionContext.LogInfo($"Completing log channel for action {currentAction.Id}. This will allow the log consumer to finish.");
            activeCtx.LogChannel.Writer.TryComplete();

            await activeCtx.LogChannel.Reader.Completion;
            masterActionContext.LogInfo($"All received logs for action {currentAction.Id} have been processed by the journaler.");
        }

        public Task JournalSlaveLogAsync(SlaveTaskLogEntry logEntry)
        {
            string? internalActionId = null;
            if (!string.IsNullOrEmpty(logEntry.TaskId) && _taskIdToActionIdMap.TryGetValue(logEntry.TaskId, out var mappedActionId))
            {
                internalActionId = mappedActionId;
            }
            else if (!string.IsNullOrEmpty(logEntry.ActionId))
            {
                if (_activeActions.ContainsKey(logEntry.ActionId))
                {
                    internalActionId = logEntry.ActionId;
                }
            }

            if (internalActionId != null && _activeActions.TryGetValue(internalActionId, out var activeCtx))
            {
                _logger.LogTrace("ActionId {ActionId}, TaskId {TaskId}: Queuing slave log entry to internal channel.", internalActionId, logEntry.TaskId ?? "N/A");
                if (!activeCtx.LogChannel.Writer.TryWrite(logEntry))
                {
                    _logger.LogWarning("Could not write log to channel for ActionId {ActionId}, TaskId {TaskId}; channel may be closed or full.", internalActionId, logEntry.TaskId ?? "N/A");
                }
            }
            else
            {
                 _logger.LogWarning("Could not journal slave log: No active action context found for ActionId '{ActionId}' / TaskId '{TaskId}'. Log from Node '{NodeName}': {Message}",
                    logEntry.ActionId, logEntry.TaskId ?? "N/A", logEntry.NodeName, logEntry.LogMessage);
            }
            return Task.CompletedTask;
        }

        public void ConfirmLogFlush(string actionId, string nodeName)
        {
            if (_activeActions.TryGetValue(actionId, out var activeCtx))
            {
                activeCtx.ConfirmedLogFlushNodes.Add(nodeName);
                _logger.LogDebug("Received log flush confirmation from node {NodeName} for action {ActionId}.", nodeName, actionId);

                var onlineNodesInAction = activeCtx.ExecutingAction.NodeTasks
                    .Select(t => t.NodeName)
                    .Distinct()
                    .Where(nn => _nodeHealthMonitorService.GetNodeCachedStateAsync(nn).Result?.ConnectivityStatus == AgentConnectivityStatus.Online)
                    .ToList();

                if (activeCtx.ConfirmedLogFlushNodes.Count >= onlineNodesInAction.Count)
                {
                    _logger.LogInformation("All expected online nodes ({Count}) have confirmed log flush for action {ActionId}.", onlineNodesInAction.Count, actionId);
                    activeCtx.LogFlushCompletionSource.TrySetResult();
                }
            }
            else
            {
                _logger.LogWarning("Received log flush confirmation for an unknown or already completed action: {ActionId} from node {NodeName}", actionId, nodeName);
            }
        }

        private async Task MonitorExecutionTimeoutAsync(ActiveActionContext activeCtx, string taskId, TimeSpan timeout)
        {
            var currentAction = activeCtx.ExecutingAction;
            await Task.Delay(timeout);

            if (_activeActions.TryGetValue(currentAction.Id, out var currentActiveCtx))
            {
                var task = currentActiveCtx.ExecutingAction.NodeTasks.FirstOrDefault(t => t.TaskId == taskId);

                if (task != null && !task.Status.IsTerminal())
                {
                    _logger.LogWarning("Execution timed out for task {TaskId} in action {ActionId} after {Seconds} seconds.", taskId, currentAction.Id, timeout.TotalSeconds);
                    task.Status = NodeTaskStatus.TimedOut;
                    task.StatusMessage = $"Task did not complete within the {timeout.TotalSeconds}-second timeout period and was marked as timed out by the master.";
                    task.EndTime = DateTime.UtcNow;
                    task.LastUpdateTime = DateTime.UtcNow;
                    await RecalculateActionStatusAsync(currentActiveCtx);
                }
            }
        }
    }
}
