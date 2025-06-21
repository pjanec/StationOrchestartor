using Microsoft.Extensions.Logging;
using NLog;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Abstractions.Workflow;
using SiteKeeper.Master.Model.InternalData;
using SiteKeeper.Master.Services.NLog2;
using SiteKeeper.Shared.DTOs.API.Operations;
using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SiteKeeper.Master.Services
{
    /// <summary>
    /// Orchestrates the execution of high-level, multi-stage Master Actions.
    /// This service acts as the central "General Contractor" for all workflows. It ensures only one
    /// Master Action runs at a time, finds the appropriate handler for a given request, and provides
    /// status and cancellation capabilities. It is the sole entry point for the API layer to initiate workflows.
    /// </summary>
    public class MasterActionCoordinator : IMasterActionCoordinator
    {
        private readonly ILogger<MasterActionCoordinator> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IJournal _journalService;
        private readonly Func<Task> _logFlushProvider;
        private readonly IActionIdTranslator _actionIdTranslator;

        // The state of the currently executing Master Action. Null if no action is running.
        private MasterAction? _currentMasterAction;
        private CancellationTokenSource? _cancellationTokenSource;
        
        // A SemaphoreSlim with a count of 1 is used to ensure only one thread can
        // enter the critical section for initiating a new Master Action.
        private static readonly SemaphoreSlim _singletonLock = new(1, 1);

        // This lock synchronizes all read/write access to the _currentMasterAction object.
        private readonly object _actionStateLock = new object();

        // serializer options to handle enums as strings, matching the Journal service.
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public MasterActionCoordinator(
            ILogger<MasterActionCoordinator> logger,
            IServiceProvider serviceProvider,
            IJournal journalService,
            // We inject the MasterNLogSetupService to get a reference to the UILoggingTarget instance,
            // which allows us to access its FlushAsync method.
            MasterNLogSetup nlogSetupService,
            IActionIdTranslator actionIdTranslator)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _journalService = journalService;
            _logFlushProvider = nlogSetupService.GetUiLoggingTarget().FlushAsync;
            _actionIdTranslator = actionIdTranslator;
        }

        /// <summary>
        /// Initiates a new Master Action workflow based on an API request.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if another Master Action is already in progress.</exception>
        /// <exception cref="NotSupportedException">Thrown if no IMasterActionHandler is registered for the requested OperationType.</exception>
        public async Task<MasterAction> InitiateMasterActionAsync(OperationInitiateRequest request, ClaimsPrincipal user)
        {
            if (!await _singletonLock.WaitAsync(TimeSpan.Zero))
            {
                throw new InvalidOperationException("Another master action is already in progress. Please wait for it to complete.");
            }

            try
            {
                var masterActionId = $"ma-{Guid.NewGuid():N}";
                _cancellationTokenSource = new CancellationTokenSource();
                var parameters = request.Parameters ?? new Dictionary<string, object>();
                _currentMasterAction = new MasterAction(masterActionId, request.OperationType, request.Description, user.Identity?.Name, parameters);

                await _journalService.RecordMasterActionInitiatedAsync(_currentMasterAction);
                var actionToReturn = _currentMasterAction;

                _ = Task.Run(async () =>
                {
                    // Create a dedicated DI scope for this workflow's lifetime.
                    using var scope = _serviceProvider.CreateScope();
                    try
                    {
                        // Resolve the scoped logger and handler from this action's scope.
                        var workflowLogger = scope.ServiceProvider.GetRequiredService<IWorkflowLogger>();
                        workflowLogger.SetContext(masterActionId); // Initialize its context.

                        var handler = scope.ServiceProvider.GetRequiredService<IEnumerable<IMasterActionHandler>>()
                            .First(h => h.Handles == request.OperationType);

                        // Create the context, now passing the scoped workflow logger.
                        var context = new MasterActionContext(
                            actionToReturn,
                            scope.ServiceProvider.GetRequiredService<IJournal>(),
                            scope.ServiceProvider,
                            workflowLogger, // Use the scoped logger
                            _cancellationTokenSource.Token,
                            _logFlushProvider,
                            _actionStateLock );
                        
                        await handler.ExecuteAsync(context);
                    }
                    catch (TaskCanceledException)
                    {
                        _logger.LogWarning("Master Action {MasterActionId} was canceled.", masterActionId);
                        // Re-create a minimal context to log the final status
                        var finalContext = new MasterActionContext(actionToReturn, _journalService, scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<IWorkflowLogger>(), _cancellationTokenSource.Token, _logFlushProvider, _actionStateLock );
                        finalContext.SetCancelled("Master action was canceled by user request.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Master Action {MasterActionId} failed with an unhandled exception.", masterActionId);
                        var finalContext = new MasterActionContext(actionToReturn, _journalService, scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<IWorkflowLogger>(), _cancellationTokenSource.Token, _logFlushProvider, _actionStateLock );
                        finalContext.SetFailed($"Workflow failed with unhandled exception: {ex.Message}");
                    }
                    finally
                    {
                        // Wait for all queued logs for this action to be processed by the NLog target.
                        // This ensures that logs from right before an exception are written to disk
                        // before we finalize and "close" the journal for this action.
                        await _logFlushProvider();

                        if (actionToReturn != null)
                        {
                            await _journalService.RecordMasterActionCompletedAsync(actionToReturn);
                        }
                        
                        _currentMasterAction = null;
                        _cancellationTokenSource?.Dispose();
                        _cancellationTokenSource = null;
                        _actionIdTranslator.UnregisterAllForMasterAction(masterActionId);
                        _journalService.ClearJournalMappings(masterActionId);
                        _singletonLock.Release();
                        _logger.LogInformation("Master Action {MasterActionId} execution finished and singleton lock released.", masterActionId);
                    }
                });

                return actionToReturn;
            }
            catch
            {
                _singletonLock.Release();
                throw;
            }
        }
 
        /// <summary>
        /// Retrieves the current status of a Master Action, formatted for the UI.
        /// </summary>
        public async Task<OperationStatusResponse?> GetStatusAsync(string masterActionId)
        {
            MasterAction? action = null;
            bool isLive = false;
    
            // Check for the live action inside the lock
            lock (_actionStateLock)
            {
                if (_currentMasterAction != null && _currentMasterAction.Id == masterActionId)
                {
                    action = _currentMasterAction;
                    isLive = true;
                }
            }

            if (action == null)
            {
                action = await _journalService.GetArchivedMasterActionAsync(masterActionId);
                isLive = false;
            }

            if (action == null)
            {
                return null;
            }

        OperationStatusResponse response;

        // Acquire the lock to ensure we read a consistent snapshot of the entire object,
        // including the ExecutionHistory and CurrentStageNodeActions collections.
        lock (_actionStateLock)
        {
                // This is the facade logic. We map the internal MasterAction state to the DTO the UI expects.
                response = new OperationStatusResponse
                {
                    Id = action.Id,
                    Name = action.Name ?? action.Type.ToString(),
                    OperationType = action.Type,
                    Status = action.OverallStatus.ToString(),
                    ProgressPercent = action.OverallProgressPercent,
                    StartTime = action.StartTime,
                    EndTime = action.EndTime,
                    Parameters = new Dictionary<string, object>(action.Parameters),
                    RecentLogs = action.GetRecentLogs()
                };

			    // helper function to create a NodeTaskStatus from a NodeTask and its parent NodeAction
			    var createOperationNodeTaskStatus = ( NodeTask nt, NodeAction nodeAction ) => new OperationNodeTaskStatus
                {
	                NodeName = nt.NodeName,
	                ActionId = nodeAction.Id,
	                ActionName = nodeAction.Name,
	                TaskId = nt.TaskId,
	                TaskType = nt.TaskType.ToString(),
	                TaskStatus = nt.Status.ToString(),
	                Message = nt.StatusMessage,
	                ResultPayload = nt.ResultPayload
                };

                // 1. Always populate the completed stages from the persistent history first.
                if (action.ExecutionHistory.Any())
                {
                    response.Stages = (
                        from stageRecord in action.ExecutionHistory
                        orderby stageRecord.StageIndex
                        select new StageStatusInfo
                        {
                            StageIndex = stageRecord.StageIndex,
                            StageName = stageRecord.StageName,
                            IsSuccess = stageRecord.IsSuccess,
                            NodeTasks = (
                                from nodeAction in stageRecord.FinalNodeActions
                                from nt in nodeAction.NodeTasks
                                select createOperationNodeTaskStatus(nt, nodeAction)
                            ).ToList()
                        }
                    ).ToList();
                }

                // 2. If the action is still live, append the currently running stage to the list.
                if (isLive && action.CurrentStageNodeActions.Any())
                {
                    var liveTasks = (
                        from nodeAction in action.CurrentStageNodeActions
                        from nt in nodeAction.NodeTasks
                        select createOperationNodeTaskStatus(nt, nodeAction)
                    ).ToList();

                    response.Stages.Add(new StageStatusInfo
                    {
                        StageIndex = action.CurrentStageIndex,
                        StageName = action.CurrentStageName ?? "Currently Running Stage",
                        IsSuccess = false, // Not yet determined
                        NodeTasks = liveTasks
                    });
                }
            }

            return response;
        }

        /// <summary>
        /// Requests cancellation of the currently running Master Action and provides immediate feedback.
        /// </summary>
        /// <returns>An OperationCancelResponse indicating the outcome of the cancellation request.</returns>
        public async Task<OperationCancelResponse> RequestCancellationAsync(string masterActionId, string cancelledBy)
        {
            var action = _currentMasterAction;

            // --- Step 1: Check if the action is currently running ---
            if (action != null && action.Id == masterActionId)
            {
                if (action.IsComplete)
                {
                    _logger.LogInformation("Received cancellation request for already completed Master Action {MasterActionId}", masterActionId);
                    return new OperationCancelResponse
                    {
                        OperationId = masterActionId,
                        Status = OperationCancellationRequestStatus.AlreadyCompleted,
                        Message = $"Action already completed with status: {action.OverallStatus}"
                    };
                }

                if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
                {
                    _logger.LogInformation("Received redundant cancellation request for Master Action {MasterActionId}", masterActionId);
                     return new OperationCancelResponse
                    {
                        OperationId = masterActionId,
                        Status = OperationCancellationRequestStatus.CancellationPending,
                        Message = "Action cancellation is already pending."
                    };
                }

                _logger.LogInformation("Cancellation requested for Master Action {MasterActionId} by {CancelledBy}", masterActionId, cancelledBy);
                _cancellationTokenSource.Cancel();
                action.OverallStatus = MasterActionStatus.Cancelling;
        
                return new OperationCancelResponse
                {
                    OperationId = masterActionId,
                    Status = OperationCancellationRequestStatus.CancellationPending,
                    Message = "Cancellation request accepted and is being processed."
                };
            }

            // --- Step 2: If not running, check the journal for a completed action ---
            var archivedAction = await _journalService.GetArchivedMasterActionAsync(masterActionId);
            if (archivedAction != null)
            {
                _logger.LogInformation("Received cancellation request for already completed (archived) Master Action {MasterActionId}", masterActionId);
                // The action existed but is finished.
                return new OperationCancelResponse
                {
                    OperationId = masterActionId,
                    Status = OperationCancellationRequestStatus.AlreadyCompleted,
                    Message = $"Action already completed with status: {archivedAction.OverallStatus}"
                };
            }

            // --- Step 3: If it's not in memory AND not in the journal, it is truly not found ---
            _logger.LogWarning("Received cancellation request for non-existent Master Action ID: {MasterActionId}", masterActionId);
            return new OperationCancelResponse
            {
                OperationId = masterActionId,
                Status = OperationCancellationRequestStatus.NotFound,
                Message = "The specified action was not found."
            };
        }
    }
}
