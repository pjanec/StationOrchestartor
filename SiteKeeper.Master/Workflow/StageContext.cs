using Microsoft.Extensions.DependencyInjection;  
using SiteKeeper.Master.Abstractions.Services;  
using SiteKeeper.Master.Abstractions.Workflow;  
using SiteKeeper.Master.Model.InternalData;  
using SiteKeeper.Master.Workflow.DTOs;  
using SiteKeeper.Shared.Enums;  
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;  
using System.Linq;  
using System.Threading;  
using System.Threading.Tasks;

namespace SiteKeeper.Master.Workflow  
{  
    public class StageContext : IStageContext  
    {  
        private readonly MasterActionContext _parentContext;  
        private readonly IServiceProvider _serviceProvider;  
        private readonly IProgress<StageProgress> _stageProgressReporter;  
        private readonly int _totalSubActions;  
        private int _currentSubActionNumber = 0;

        // Resolved services  
        private readonly IJournal _journalService;  
        private readonly INodeActionDispatcher _nodeActionDispatcher;  
        private readonly IAgentConnectionManager _agentConnectionManager;  
        private readonly IActionIdTranslator _actionIdTranslator;  
          
        public string StageName { get; }

        private readonly int _stageIndex;
        private DateTime _stageStartTime = DateTime.UtcNow;
        private readonly object _actionStateLock;

		// Final custom result object for this stage, to be saved to journal upon disposal.
		// This is for stages not using NodeActions, or for custom results that are not tied to any NodeAction.
        // Note: node action results are saved separately and automatically and do not affect this.
        private object? _customStageResult;

        // automatically accumulates results from the Execute methods.
        private readonly List<NodeActionResult> _nodeActionResults = new List<NodeActionResult>();


        internal StageContext(  
            MasterActionContext parentContext,  
            IServiceProvider serviceProvider,  
            string stageName,  
            int stageIndex,
            int subActionCount,  
            IProgress<StageProgress> stageProgressReporter,
            object actionStateLock
            )  
        {  
            _parentContext = parentContext;  
            _serviceProvider = serviceProvider;  
            StageName = stageName;  
            _stageIndex = stageIndex;
            _totalSubActions = Math.Max(1, subActionCount);  
            _stageProgressReporter = stageProgressReporter;

            _journalService = _serviceProvider.GetRequiredService<IJournal>();  
            _nodeActionDispatcher = _serviceProvider.GetRequiredService<INodeActionDispatcher>();  
            _agentConnectionManager = _serviceProvider.GetRequiredService<IAgentConnectionManager>();  
            _actionIdTranslator = _serviceProvider.GetRequiredService<IActionIdTranslator>();  
            _actionStateLock = actionStateLock;

        }

		/// <inheritdoc />
		public void SetCustomResult(object? result)
        {
            _customStageResult = result;
        }

        private void AdvanceSubAction()  
        {  
            _currentSubActionNumber++;  
            LogInfo($"Beginning sub-action {_currentSubActionNumber}/{_totalSubActions} in stage '{StageName}'.");  
        }

		/// <inheritdoc />   
        public async Task<NodeActionResult> CreateAndExecuteNodeActionAsync(  
            string actionName,  
            SlaveTaskType slaveTaskType,  
            Dictionary<string, object>? auditContext = null,  
            Dictionary<string, Dictionary<string, object>>? nodeSpecificPayloads = null,
            List<string>? targetNodeNames = null
            )
        {  
            AdvanceSubAction();

            // 1. Create the NodeAction object
            var sanitizedActionName = System.Text.RegularExpressions.Regex.Replace(actionName, @"[^a-zA-Z0-9\-_ ]", "").Trim();
            var actionId = $"op-{sanitizedActionName.ToLower()}-{Guid.NewGuid():N}";
            var action = new NodeAction(actionId, actionName, auditContext, _parentContext.MasterAction.InitiatedBy);

            // Register the action with the journal and ID translation services.
            await _journalService.MapNodeActionToStageAsync(_parentContext.MasterActionId, _stageIndex, StageName, action.Id);
            _actionIdTranslator.RegisterMapping(action.Id, _parentContext.MasterActionId);

            // --- 2. Filter target nodes based on the new parameter ---
            var allAgents = await _agentConnectionManager.GetAllConnectedAgentsAsync();
            var targetAgents = allAgents; // Default to all

            if (targetNodeNames != null && targetNodeNames.Any())
            {
                var requestedNodes = new HashSet<string>(targetNodeNames);
                targetAgents = allAgents.Where(agent => requestedNodes.Contains(agent.NodeName)).ToList();
        
                // Log if some requested nodes were not found/online
                var missingNodes = requestedNodes.Except(targetAgents.Select(a => a.NodeName));
                if (missingNodes.Any())
                {
                    LogWarning($"For action '{action.Name}', the following target nodes are not online: {string.Join(", ", missingNodes)}");
                }
            }

            // Create tasks for the filtered list of target agents.
            foreach (var agent in targetAgents)
            {
                var payload = nodeSpecificPayloads?.GetValueOrDefault(agent.NodeName) ?? new Dictionary<string, object>();
                action.NodeTasks.Add(new NodeTask($"{actionId}-{agent.NodeName}", action.Id, agent.NodeName, slaveTaskType, payload));
            }
            LogInfo($"Created NodeAction '{action.Name}' ({action.Id}) targeting {action.NodeTasks.Count} nodes.");

            // --- 3. Execute with robust cancellation handling ---
            try
            {
                // 3. Execute the action and report progress  
                var subActionProgress = new Progress<StageProgress>(progress =>  
                {  
                    double completedSubActionsProgress = ((double)_currentSubActionNumber - 1) / _totalSubActions * 100;  
                    double currentSubActionContribution = (double)progress.ProgressPercent / _totalSubActions;  
                    int overallStageProgress = (int)(completedSubActionsProgress + currentSubActionContribution);  
                    _stageProgressReporter.Report(new StageProgress { ProgressPercent = overallStageProgress, StatusMessage = progress.StatusMessage });  
                });  
              
                // Add the action to the master context's concurrent bag for real-time UI reporting.
                _parentContext.MasterAction.CurrentStageNodeActions.Add(action);

                var result = await _nodeActionDispatcher.ExecuteAsync(action, _parentContext, subActionProgress, _parentContext.CancellationToken);  

                _nodeActionResults.Add(result);

                return result;  
            }
            catch (OperationCanceledException)
            {
                // This block executes if the dispatcher's task is cancelled.
                LogWarning($"Node action '{action.Name}' was cancelled. The operation will be marked as cancelled.");
        
                // The dispatcher's internal logic will have already updated the 'action' object's status.
                // We just need to ensure the final state is captured.
                var finalState = _parentContext.MasterAction.CurrentStageNodeActions
                    .FirstOrDefault(a => a.Id == action.Id) ?? action;
            
                return new NodeActionResult { IsSuccess = false, FinalState = finalState };
            }
            finally
            {
                // Ensure the action is removed from the live tracking bag upon completion or failure.
                _parentContext.MasterAction.CurrentStageNodeActions.Clear();
            }
        }

		/// <inheritdoc />   
		public async Task<List<NodeActionResult>> CreateAndExecuteNodeActionsInParallelAsync(
            IEnumerable<NodeActionInput> actionInputs)
        {
            var actionInputList = actionInputs.ToList();
            AdvanceSubAction(); // Advance the stage's sub-action counter once for the entire parallel block.

            var parallelTasks = new List<Task<NodeActionResult>>();
            var progressDict = new ConcurrentDictionary<string, int>();
            var allAgents = await _agentConnectionManager.GetAllConnectedAgentsAsync();

            // --- 1. Launch all tasks in parallel ---
            foreach (var input in actionInputList)
            {
                var actionId = $"op-{input.ActionName.ToLower().Replace(" ", "-")}-{Guid.NewGuid():N}";
                var action = new NodeAction(actionId, input.ActionName, input.AuditContext, _parentContext.MasterAction.InitiatedBy);

                // Register the action with the journal and ID translation services.
                await _journalService.MapNodeActionToStageAsync(_parentContext.MasterActionId, _stageIndex, StageName, action.Id);
                _actionIdTranslator.RegisterMapping(action.Id, _parentContext.MasterActionId);

                // --- Filter target nodes ---
                var targetAgents = allAgents;
                if (input.TargetNodeNames != null && input.TargetNodeNames.Any())
                {
                    var requestedNodes = new HashSet<string>(input.TargetNodeNames);
                    targetAgents = allAgents.Where(agent => requestedNodes.Contains(agent.NodeName)).ToList();
                }

                // Create tasks only for the designated target nodes.
                foreach (var agent in targetAgents)
                {
                    var payload = input.NodeSpecificPayloads?.GetValueOrDefault(agent.NodeName) ?? new Dictionary<string, object>();
                    action.NodeTasks.Add(new NodeTask($"{action.Id}-{agent.NodeName}", action.Id, agent.NodeName, input.SlaveTaskType, payload));
                }
                LogInfo($"Created parallel NodeAction '{action.Name}' ({action.Id}) targeting {action.NodeTasks.Count} of {allAgents.Count} connected nodes.");

                // Add the action to the master context's concurrent bag for real-time UI reporting.
                _parentContext.MasterAction.CurrentStageNodeActions.Add(action);

                // --- Define a progress reporter for this specific action ---
                var singleActionProgress = new Progress<StageProgress>(progress =>
                {
                    progressDict[action.Id] = progress.ProgressPercent;
                    int averageProgress = (int)progressDict.Values.DefaultIfEmpty(0).Average();
                    _stageProgressReporter.Report(new StageProgress
                    {
                        ProgressPercent = averageProgress,
                        StatusMessage = $"Parallel execution in progress... ({progressDict.Count}/{actionInputList.Count} actions reporting)"
                    });
                });

                // Launch the dispatcher task and add it to the list to be awaited.
                parallelTasks.Add(_nodeActionDispatcher.ExecuteAsync(action, _parentContext, singleActionProgress, _parentContext.CancellationToken));
            }

            // --- 2. Await completion and handle cancellation ---
            try
            {
                var results = await Task.WhenAll(parallelTasks);

                _nodeActionResults.AddRange(results);

                return results.ToList();
            }
            catch (OperationCanceledException)
            {
                // This block is entered when the main CancellationToken is triggered.
                LogWarning("Parallel stage was cancelled. Waiting for all tasks to confirm cancellation...");

                // Wait for all tasks to reach a terminal state (Succeeded, Failed, or Cancelled).
                // Task.WhenAll has already completed (by throwing), so the underlying tasks are complete.
                // We just need to gather their final states.
                var finalResults = new List<NodeActionResult>();
                foreach (var task in parallelTasks)
                {
                    // Inspect the task's status. It will not block here.
                    if (task.IsCanceled || task.IsFaulted)
                    {
                        // The NodeActionDispatcher's own finally block ensures it returns a result
                        // object even on cancellation, containing the final state. We can get this
                        // result without re-throwing the exception.
                        var resultProperty = typeof(Task<NodeActionResult>).GetProperty("Result");
                        if (resultProperty != null)
                        {
                             finalResults.Add((NodeActionResult)resultProperty.GetValue(task)!);
                        }
                    }
                    else if (task.IsCompletedSuccessfully)
                    {
                        finalResults.Add(task.Result);
                    }
                }

                _nodeActionResults.AddRange(finalResults);

                return finalResults;
            }
            finally
            {
                // Final cleanup is always performed.
                _parentContext.MasterAction.CurrentStageNodeActions.Clear();
            }
        }

        public void ReportProgress(int subStepProgressPercent, string statusMessage)  
        {  
            if (_currentSubActionNumber == 0) AdvanceSubAction();  
            _stageProgressReporter.Report(new StageProgress { ProgressPercent = subStepProgressPercent, StatusMessage = statusMessage });  
        }

        #region Logging  
        public void LogInfo(string message) => _parentContext.LogInfo(message);  
        public void LogWarning(string message) => _parentContext.LogWarning(message);  
        public void LogError(Exception? ex, string message) => _parentContext.LogError(ex, message);  
        #endregion

        /// <summary>
        /// Finalizes the stage by writing its completion record to the journal upon disposal.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            // First, write the detailed stage_result.json file. We can store a composite object here.
            var combinedResultForFile = new
            {
                NodeActionResults = _nodeActionResults,
                CustomResult = _customStageResult
            };
            await _journalService.RecordStageCompletedAsync(_parentContext.MasterActionId, _stageIndex, StageName, combinedResultForFile);

            // Now, populate the permanent record for the main master_action_info.json file.
            var finalActions = new List<NodeAction>();
            
            // 1. Assume the stage is successful if it completes without throwing an exception.
            //    This is the correct default for stages that might not run any node actions.
            bool stageSuccess = true; 

            // 2. If node actions were run, their outcome overrides the stage's success status.
            if (_nodeActionResults.Any())
            {
                finalActions.AddRange(_nodeActionResults.Select(r => r.FinalState));
                stageSuccess = _nodeActionResults.All(r => r.IsSuccess);
            }

            // 3. Add the permanent record to the MasterAction's history within a lock to ensure thread safety.
            lock (_actionStateLock)
            {
                _parentContext.MasterAction.ExecutionHistory.Add(new StageRecord
                {
                    StageIndex = _stageIndex,
                    StageName = StageName,
                    StartTime = _stageStartTime,
                    EndTime = DateTime.UtcNow,
                    IsSuccess = stageSuccess,
                    FinalNodeActions = finalActions, // Store the explicit list of node actions
                    CustomResult = _customStageResult // Store the independent custom result
                });
            }
        }
    }  
} 