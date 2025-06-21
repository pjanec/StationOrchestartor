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

namespace SiteKeeper.Master.Workflow
{
    /// <summary>
    /// A specialized, stateful stage handler that executes and manages actions distributed across multiple slave nodes.
    /// This class is registered as a singleton and is responsible for the entire lifecycle of a multi-node action
    /// within a master workflow, including readiness checks, task dispatch, progress tracking, cancellation, and health monitoring.
    /// </summary>
    public class NodeActionDispatcher : INodeActionDispatcher
    {
        #region Private State Management Class
        /// <summary>
        /// A private nested class to hold the complete state for each active multi-node action
        /// being managed by this handler. This allows the singleton handler to manage multiple actions concurrently if needed in the future,
        /// although the current MasterActionCoordinator enforces a single workflow at a time.
        /// </summary>
        private class ActionContext
        {
            /// <summary>The core action object containing all tasks and parameters.</summary>
            public NodeAction Action { get; }
            /// <summary>A reference to the parent MasterAction's context for logging and journaling.</summary>
            public MasterActionContext ParentMasterActionContext { get; }
            /// <summary>The TaskCompletionSource that the parent workflow awaits. It is completed only when the action finishes.</summary>
            public TaskCompletionSource<NodeActionResult> ActionCompletionSource { get; }
            /// <summary>A thread-safe collection to track which nodes have confirmed their logs are flushed.</summary>
            public ConcurrentBag<string> ConfirmedLogFlushNodes { get; }
            /// <summary>The TaskCompletionSource for the log flush phase. It is completed when all nodes have confirmed.</summary>
            public TaskCompletionSource LogFlushCompletionSource { get; }
            /// <summary>The progress reporter for this specific stage.</summary>
            public IProgress<StageProgress> ProgressReporter { get; }
			
            // for hanling logs arriving after the action is completed
			public Channel<SlaveTaskLogEntry> LogChannel { get; }

            public ActionContext(NodeAction action, IProgress<StageProgress> progress, MasterActionContext parentMasterActionContext)
            {
                Action = action;
                ProgressReporter = progress;
                ParentMasterActionContext = parentMasterActionContext;
                ActionCompletionSource = new TaskCompletionSource<NodeActionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                ConfirmedLogFlushNodes = new ConcurrentBag<string>();
                LogFlushCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                // Initialize an unbounded channel for this action's logs.
                LogChannel = Channel.CreateUnbounded<SlaveTaskLogEntry>(new UnboundedChannelOptions { SingleReader = true });
            }
        }
        #endregion

        private readonly ILogger<NodeActionDispatcher> _logger;
        private readonly IAgentConnectionManager _agentConnectionManager;
        private readonly IJournal _journalService;
        private readonly INodeHealthMonitor _nodeHealthMonitorService;
        private readonly ConcurrentDictionary<string, ActionContext> _activeActions = new();
        private readonly ConcurrentDictionary<string, string> _taskIdToActionIdMap = new();

        public NodeActionDispatcher(
            ILogger<NodeActionDispatcher> logger,
            IAgentConnectionManager agentConnectionManager,
            IJournal journalService,
            INodeHealthMonitor nodeHealthMonitorService)
        {
            _logger = logger;
            _agentConnectionManager = agentConnectionManager;
            _journalService = journalService;
            _nodeHealthMonitorService = nodeHealthMonitorService;
        }

        /// <summary>
        /// Executes a multi-node action as a single stage within a Master Action. This is the main entry point for the handler.
        /// </summary>
        /// <param name="action">The DTO containing the pre-built Action object to execute.</param>
        /// <param name="masterActionContext">The context of the parent Master Action.</param>
        /// <param name="progress">The progress reporter for this stage.</param>
        /// <param name="cancellationToken">The cancellation token for the parent Master Action.</param>
        /// <returns>A NodeActionResult containing the final state of the action.</returns>
        public async Task<NodeActionResult> ExecuteAsync(
            NodeAction action,
            MasterActionContext masterActionContext,
            IProgress<StageProgress> progress,
            CancellationToken cancellationToken)
        {
            if (action == null || !action.NodeTasks.Any())
            {
                masterActionContext.LogInfo("NodeActionDispatcher received no tasks to execute => action FAILED.");
                return new NodeActionResult { IsSuccess = false, FinalState = action ?? new NodeAction("empty") };
            }

            var opContext = new ActionContext(action, progress, masterActionContext);

            if (!_activeActions.TryAdd(action.Id, opContext))
            {
                throw new InvalidOperationException($"An action with ID {action.Id} is already running.");
            }
        
            foreach (var task in action.NodeTasks)
            {
                _taskIdToActionIdMap[task.TaskId] = action.Id;
            }
              
            masterActionContext.LogInfo($"Starting multi-node action {action.Name} {action.Id} for MasterAction {masterActionContext.MasterActionId}");
            
            var logConsumerTask = ConsumeLogChannelAsync(opContext);

            var healthMonitoringCts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, healthMonitoringCts.Token);
            _ = MonitorNodeHealthForAction(opContext, linkedCts.Token);

            using (cancellationToken.Register(() => opContext.ActionCompletionSource.TrySetCanceled()))
            {
                try
                {
                    await StartReadinessCheckAndDispatchSequenceAsync(action);
                    var actionResult = await opContext.ActionCompletionSource.Task;
                    await FlushAllNodeLogsAsync(actionResult, masterActionContext);
                    return actionResult;
                }
                catch (TaskCanceledException)
                {
                    masterActionContext.LogWarning($"Multi-node action stage {action.Id} was cancelled by master request. Handling cancellation...");
                    action.OverallStatus = NodeActionOverallStatus.Cancelling;

                    var tasksToCancel = action.NodeTasks.Where(t => !t.Status.IsTerminal()).ToList();

                    foreach (var task in tasksToCancel)
                    {
                        var nodeState = await _nodeHealthMonitorService.GetNodeCachedStateAsync(task.NodeName);
    
                        if (nodeState?.ConnectivityStatus is AgentConnectivityStatus.Offline or AgentConnectivityStatus.Unreachable)
                        {
                            masterActionContext.LogInfo($"Task {task.TaskId} on offline node {task.NodeName} is being marked as Cancelled immediately.");
                            task.Status = NodeTaskStatus.Cancelled;
                            task.StatusMessage = "Task cancelled; the target node was offline.";
                            task.EndTime = DateTime.UtcNow;
                        }
                        else
                        {
                            masterActionContext.LogInfo($"Requesting cancellation for task {task.TaskId} on online node {task.NodeName}.");
                            task.Status = NodeTaskStatus.Cancelling;
                            await _agentConnectionManager.SendCancelTaskAsync(task.NodeName, new CancelTaskOnAgentRequest { ActionId = action.Id, TaskId = task.TaskId, Reason = "Action cancelled by master." });
                        }
                    }

                    await MonitorCancellationCompletion(opContext, TimeSpan.FromSeconds(15));

                    foreach (var task in action.NodeTasks.Where(t => t.Status == NodeTaskStatus.Cancelling))
                    {
                        masterActionContext.LogInfo($"Finalizing status for task {task.TaskId} from 'Cancelling' to 'Cancelled' after monitor completion.");
                        task.Status = NodeTaskStatus.Cancelled;
                        task.StatusMessage = "Task cancellation confirmed as node is offline or did not respond.";
                        task.EndTime = DateTime.UtcNow;
                    }

                    action.OverallStatus = NodeActionOverallStatus.Cancelled;
                    action.EndTime = DateTime.UtcNow;

                    var cancelledResult = new NodeActionResult { IsSuccess = false, FinalState = action };
                    await FlushAllNodeLogsAsync(cancelledResult, masterActionContext);

                    opContext.ActionCompletionSource.TrySetResult(cancelledResult);

                    return cancelledResult;
                }
                finally
                {
                    healthMonitoringCts.Cancel();
                    _activeActions.TryRemove(action.Id, out _);

                    foreach (var task in action.NodeTasks)
                    {
                        _taskIdToActionIdMap.TryRemove(task.TaskId, out _);
                    }

                    await logConsumerTask;

                    masterActionContext.LogInfo($"Multi-node action stage {action.Id} finished and cleaned up.");
                }
            }
        }
        
        private async Task MonitorNodeHealthForAction(ActionContext opContext, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);

                    var activeTasks = opContext.Action.NodeTasks.Where(t => !t.Status.IsTerminal()).ToList();
                    if (!activeTasks.Any()) return;

                    foreach (var task in activeTasks)
                    {
                        var nodeState = await _nodeHealthMonitorService.GetNodeCachedStateAsync(task.NodeName);
                        if (nodeState?.ConnectivityStatus is AgentConnectivityStatus.Offline or AgentConnectivityStatus.Unreachable)
                        {
                            opContext.ParentMasterActionContext.LogError(null, $"Node '{task.NodeName}' disconnected during active task {task.TaskId}. Failing the task.");
                            task.Status = NodeTaskStatus.NodeOfflineDuringTask;
                            task.StatusMessage = $"Node went offline or became unreachable during task execution. Last known status: {nodeState.ConnectivityStatus}.";
                            task.EndTime = DateTime.UtcNow;
                            await RecalculateActionStatusAsync(opContext);
                        }
                    }
                }
                catch (TaskCanceledException) { return; }
                catch (Exception ex)
                {
                    opContext.ParentMasterActionContext.LogError(ex, $"Error in node health monitoring loop for action {opContext.Action.Id}.");
                }
            }
        }

        private async Task MonitorCancellationCompletion(ActionContext opContext, TimeSpan timeout)
        {
            var timeoutCts = new CancellationTokenSource(timeout);
            opContext.ParentMasterActionContext.LogInfo($"Monitoring for cancellation confirmation from nodes for OpId: {opContext.Action.Id}");

            try
            {
                while (!timeoutCts.IsCancellationRequested)
                {
                    var tasksStillCancelling = opContext.Action.NodeTasks.Where(t => t.Status == NodeTaskStatus.Cancelling).ToList();
                    
                    if (!tasksStillCancelling.Any())
                    {
                        _logger.LogInformation("All nodes have confirmed terminal status for OpId {ActionId}.", opContext.Action.Id);
                        return; // All tasks have moved past the "Cancelling" state
                    }

                    bool allAccountedFor = true;
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
                        opContext.ParentMasterActionContext.LogInfo($"All reachable nodes have confirmed terminal status after cancellation request for OpId: {opContext.Action.Id}");
                        return;
                    }
                    await Task.Delay(500, timeoutCts.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // This means the timeout was reached
            }

            opContext.ParentMasterActionContext.LogWarning($"Timed out waiting for all nodes to confirm cancellation for OpId: {opContext.Action.Id}. Forcibly marking remaining tasks as Cancelled.");
            foreach (var task in opContext.Action.NodeTasks.Where(t => !t.Status.IsTerminal()))
            {
                task.Status = NodeTaskStatus.Cancelled;
                task.StatusMessage = "Node did not confirm cancellation within timeout; master marked as cancelled.";
                task.EndTime = DateTime.UtcNow;
            }
        }
        
        /// <summary>
        /// Sends a "Prepare for Task" instruction to all nodes involved in the action.
        /// </summary>
        private async Task StartReadinessCheckAndDispatchSequenceAsync(NodeAction action)
        {
            action.StartTime = DateTime.UtcNow;
            action.OverallStatus = NodeActionOverallStatus.AwaitingNodeReadiness;
            
            foreach (var task in action.NodeTasks)
            {
                string? prepParamsJson = null;
                if (task.TaskType == SlaveTaskType.TestOrchestration)
                {
                    prepParamsJson = JsonSerializer.Serialize(task.TaskPayload);
                }

                var prepareInstruction = new PrepareForTaskInstruction
                {
                    ActionId = task.ActionId,
                    TaskId = task.TaskId,
                    ExpectedTaskType = task.TaskType,
                    PreparationParametersJson = prepParamsJson
                };
                await _agentConnectionManager.SendPrepareForTaskInstructionAsync(task.NodeName, prepareInstruction);
                task.Status = NodeTaskStatus.ReadinessCheckSent;
            }

            _ = MonitorReadinessTimeout(action.Id, TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// Processes a readiness report from a slave, received via the AgentHub.
        /// </summary>
        public async Task ProcessSlaveTaskReadinessAsync(SlaveTaskReadinessReport readinessReport)
        {
            if (!_taskIdToActionIdMap.TryGetValue(readinessReport.TaskId, out var internalOpId) ||
                !_activeActions.TryGetValue(internalOpId, out var opContext))
            {
                _logger.LogWarning("Received readiness report for an unknown or completed task: TaskId {TaskId}", readinessReport.TaskId);
                return;
            }

            var task = opContext.Action.NodeTasks.FirstOrDefault(t => t.TaskId == readinessReport.TaskId);
            if (task == null) return;

            var parentContext = opContext.ParentMasterActionContext;

            if (readinessReport.IsReady)
            {
                task.Status = NodeTaskStatus.ReadyToExecute;
                task.StatusMessage = "Agent reported ready for task execution.";
                parentContext.LogInfo($"Node {task.NodeName} is ready for task {task.TaskId}.");

                var slaveTaskInstruction = new SlaveTaskInstruction
                {
                    ActionId = task.ActionId,
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
                parentContext.LogWarning($"Node {task.NodeName} is not ready for task {task.TaskId}. Reason: {readinessReport.ReasonIfNotReady}");
            }
            await RecalculateActionStatusAsync(opContext);
        }

        public async Task ProcessTaskStatusUpdateAsync(SlaveTaskProgressUpdate statusUpdate)
        {
            if (!_activeActions.TryGetValue(statusUpdate.ActionId, out var opContext))
            {
                _logger.LogWarning("Received task status update for an unknown or completed action: {ActionId}", statusUpdate.ActionId);
                return;
            }

            var nodeTask = opContext.Action.NodeTasks.FirstOrDefault(t => t.TaskId == statusUpdate.TaskId);
            if (nodeTask == null)
            {
                opContext.ParentMasterActionContext.LogWarning($"Received status update for an unknown task: {statusUpdate.TaskId} in action {statusUpdate.ActionId}");
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
                         opContext.ParentMasterActionContext.LogError(ex, $"Failed to deserialize ResultJson for task {nodeTask.TaskId}.");
                    }
                }
                await _journalService.RecordNodeTaskResultAsync(nodeTask);
            }
            await RecalculateActionStatusAsync(opContext);
        }
        
        /// <summary>
        /// Recalculates the overall status of an action based on the current statuses of its individual node tasks.
        /// </summary>
        private async Task RecalculateActionStatusAsync(ActionContext opContext)
        {
            var action = opContext.Action;
            var tasks = action.NodeTasks;

            if (!tasks.Any()) {
                action.OverallStatus = NodeActionOverallStatus.Succeeded;
                action.ProgressPercent = 100;
            } else {
                int succeeded = tasks.Count(t => t.Status == NodeTaskStatus.Succeeded);
                int failedOrCancelled = tasks.Count(t => t.Status.IsTerminal() && t.Status != NodeTaskStatus.Succeeded);
                
                action.ProgressPercent = (int)tasks.Average(t => t.ProgressPercent);
                action.StatusMessage = $"In Progress: {tasks.Count(t => !t.Status.IsTerminal())}, Succeeded: {succeeded}, Failed/Cancelled: {failedOrCancelled}";

                if (tasks.All(t => t.Status.IsTerminal()))
                {
                    if (tasks.Any(t => t.Status == NodeTaskStatus.Cancelled || t.Status == NodeTaskStatus.Cancelling))
                        action.OverallStatus = NodeActionOverallStatus.Cancelled;
                    else if (tasks.Any(t => t.Status != NodeTaskStatus.Succeeded))
                        action.OverallStatus = NodeActionOverallStatus.Failed;
                    else
                        action.OverallStatus = NodeActionOverallStatus.Succeeded;
                    
                    action.EndTime = DateTime.UtcNow;
                }
            }
            
            opContext.ProgressReporter.Report(new StageProgress {
                ProgressPercent = action.ProgressPercent,
                StatusMessage = action.StatusMessage
            });
            
            if (action.OverallStatus.IsCompleted())
            {
                var result = new NodeActionResult {
                    IsSuccess = action.OverallStatus == NodeActionOverallStatus.Succeeded,
                    FinalState = action
                };
                opContext.ActionCompletionSource.TrySetResult(result);
            }
        }
        
        /// <summary>
        /// Monitors for readiness check timeouts.
        /// </summary>
        private async Task MonitorReadinessTimeout(string actionId, TimeSpan timeout)
        {
            await Task.Delay(timeout);

            if (_activeActions.TryGetValue(actionId, out var opContext) && opContext.Action.OverallStatus == NodeActionOverallStatus.AwaitingNodeReadiness)
            {
                opContext.ParentMasterActionContext.LogWarning($"Readiness check timed out for action {actionId}");
                bool changed = false;
                foreach (var task in opContext.Action.NodeTasks.Where(t => t.Status == NodeTaskStatus.ReadinessCheckSent))
                {
                    task.Status = NodeTaskStatus.ReadinessCheckTimedOut;
                    task.StatusMessage = "Agent did not respond to readiness check within the timeout period.";
                    task.EndTime = DateTime.UtcNow;
                    changed = true;
                }
                if(changed) await RecalculateActionStatusAsync(opContext);
            }
        }
        
        private async Task ConsumeLogChannelAsync(ActionContext opContext)
        {
            try
            {
                await foreach (var logEntry in opContext.LogChannel.Reader.ReadAllAsync())
                {
                    try
                    {
                        // The logEntry.ActionId is the internal ID for the stage's action,
                        // but the JournalService tracks journals by the parent MasterActionId.
                        await _journalService.AppendSlaveLogToStageAsync(opContext.ParentMasterActionContext.MasterActionId, logEntry);
                    }
                    catch (Exception ex)
                    {
                        opContext.ParentMasterActionContext.LogError(ex, $"Error writing slave log from channel to journal for OpId {logEntry.ActionId}");
                    }
                }
            }
            catch (Exception ex)
            {
                opContext.ParentMasterActionContext.LogError(ex, $"Log channel consumer for OpId {opContext.Action.Id} failed unexpectedly.");
            }
        }

        private async Task FlushAllNodeLogsAsync(NodeActionResult actionResult, MasterActionContext masterActionContext)
        {
            var allParticipatingNodes = actionResult.FinalState.NodeTasks.Select(t => t.NodeName).Distinct().ToList();
            if (!allParticipatingNodes.Any()) return;
            if (!_activeActions.TryGetValue(actionResult.FinalState.Id, out var opContext)) return;

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
                masterActionContext.LogInfo($"Action stage complete. Requesting log flush from {onlineNodes.Count} online node(s)...");

                foreach (var nodeName in onlineNodes)
                {
                    await _agentConnectionManager.RequestLogFlushForTask(nodeName, actionResult.FinalState.Id);
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
                         masterActionContext.LogWarning("Timed out waiting for all online nodes to confirm log flush.");
                    }
                    else
                    {
                        masterActionContext.LogInfo("All online nodes have confirmed their logs have been sent.");
                    }
                }
                catch (TaskCanceledException)
                {
                     masterActionContext.LogWarning("Timed out waiting for all online nodes to confirm log flush.");
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
            // This method receives a log entry that may not have a TaskId if it's a general action log.
            // However, for logs from a slave task, it SHOULD have a TaskId.
            // The lookup must be resilient.
            string? actionId = null;
            if (!string.IsNullOrEmpty(logEntry.TaskId))
            {
                _taskIdToActionIdMap.TryGetValue(logEntry.TaskId, out actionId);
            }

            if (actionId == null || !_activeActions.TryGetValue(actionId, out var opContext))
            {
                 _logger.LogWarning("Could not journal slave log: No active context found for ActionId {ActionId} / TaskId {TaskId}", 
                    logEntry.ActionId, logEntry.TaskId);
                return Task.CompletedTask;
            }
 
            // Instead of writing directly, just enqueue the log entry. This is a fast, non-blocking operation.
            if (!opContext.LogChannel.Writer.TryWrite(logEntry))
            {
                _logger.LogWarning("Could not write log to channel for OpId {opId}; channel may be closed.", logEntry.ActionId);
            }

            return Task.CompletedTask;
        }

        public void ConfirmLogFlush(string actionId, string nodeName)
        {
            if (_activeActions.TryGetValue(actionId, out var opContext))
            {
                opContext.ConfirmedLogFlushNodes.Add(nodeName);
                
                // Use the parent context's logger
                opContext.ParentMasterActionContext.LogInfo($"Received log flush confirmation from node {nodeName} for action {actionId}.");

                var totalNodes = opContext.Action.NodeTasks.Select(t => t.NodeName).Distinct().Count();
                if (opContext.ConfirmedLogFlushNodes.Count >= totalNodes)
                {
                    opContext.ParentMasterActionContext.LogInfo($"All nodes have confirmed log flush for action {actionId}.");
                    // Signal that the slave confirmation phase is complete.
                    opContext.LogFlushCompletionSource.TrySetResult();
                }
            }
            else
            {
                _logger.LogWarning("Received log flush confirmation for an unknown or already completed action: {ActionId}", actionId);
            }
        }

        private async Task MonitorExecutionTimeoutAsync(ActionContext opContext, string taskId, TimeSpan timeout)
        {
            await Task.Delay(timeout);

            if (_activeActions.TryGetValue(opContext.Action.Id, out var currentOpContext))
            {
                var task = currentOpContext.Action.NodeTasks.FirstOrDefault(t => t.TaskId == taskId);

                if (task != null && !task.Status.IsTerminal())
                {
                    currentOpContext.ParentMasterActionContext.LogWarning($"Execution timed out for task {taskId} in action {opContext.Action.Id} after {timeout.TotalSeconds} seconds.");
                    task.Status = NodeTaskStatus.TimedOut;
                    task.StatusMessage = $"Task did not complete within the {timeout.TotalSeconds}-second timeout period and was marked as timed out by the master.";
                    task.EndTime = DateTime.UtcNow;
                    await RecalculateActionStatusAsync(currentOpContext);
                }
            }
        }
    }
}
