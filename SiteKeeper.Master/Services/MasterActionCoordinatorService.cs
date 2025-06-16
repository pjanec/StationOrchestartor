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

namespace SiteKeeper.Master.Services
{
    /// <summary>
    /// Orchestrates the execution of high-level, multi-stage Master Actions.
    /// This service acts as the central "General Contractor" for all workflows. It ensures only one
    /// Master Action runs at a time, finds the appropriate handler for a given request, and provides
    /// status and cancellation capabilities. It is the sole entry point for the API layer to initiate workflows.
    /// </summary>
    public class MasterActionCoordinatorService : IMasterActionCoordinatorService
    {
        private readonly ILogger<MasterActionCoordinatorService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IJournalService _journalService;
        private readonly Func<Task> _logFlushProvider;

        // The state of the currently executing Master Action. Null if no action is running.
        private MasterAction? _currentMasterAction;
        private CancellationTokenSource? _cancellationTokenSource;
        
        // A SemaphoreSlim with a count of 1 is used to ensure only one thread can
        // enter the critical section for initiating a new Master Action.
        private static readonly SemaphoreSlim _singletonLock = new(1, 1);

        public MasterActionCoordinatorService(
            ILogger<MasterActionCoordinatorService> logger,
            IServiceProvider serviceProvider,
            IJournalService journalService,
            // We inject the MasterNLogSetupService to get a reference to the UILoggingTarget instance,
            // which allows us to access its FlushAsync method.
            MasterNLogSetupService nlogSetupService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _journalService = journalService;
            _logFlushProvider = nlogSetupService.GetUiLoggingTarget().FlushAsync;
        }

        /// <summary>
        /// Initiates a new Master Action workflow based on an API request.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if another Master Action is already in progress.</exception>
        /// <exception cref="NotSupportedException">Thrown if no IMasterActionHandler is registered for the requested OperationType.</exception>
        public async Task<MasterAction> InitiateMasterActionAsync(OperationInitiateRequest request, ClaimsPrincipal user)
        {
            // Asynchronously wait for the lock, but with a timeout of 0. If the lock is not
            // immediately available, it means another action is running, and we throw an exception.
            if (!await _singletonLock.WaitAsync(TimeSpan.Zero))
            {
                throw new InvalidOperationException("Another master action is already in progress. Please wait for it to complete.");
            }

            try
            {
                // Create a new dependency injection scope for this specific workflow run.
                using (var scope = _serviceProvider.CreateScope())
                {
                    // Resolve the handlers from within this temporary scope.
                    var handlers = scope.ServiceProvider.GetRequiredService<IEnumerable<IMasterActionHandler>>();
                    var handler = handlers.FirstOrDefault(h => h.Handles == request.OperationType);

                    if (handler == null)
                    {
                        // No need to release the lock here, the 'finally' in the outer try/catch will handle it.
                        throw new NotSupportedException($"Master action for operation type '{request.OperationType}' is not supported.");
                    }

                    var masterActionId = $"ma-{Guid.NewGuid():N}";
                    _cancellationTokenSource = new CancellationTokenSource();
                    var parameters = request.Parameters ?? new Dictionary<string, object>();
                    
                    // First, create the MasterAction object that holds the state for the entire workflow.
                    _currentMasterAction = new MasterAction(masterActionId, request.OperationType, request.Description, user.Identity?.Name, parameters);

                    await _journalService.RecordMasterActionInitiatedAsync(_currentMasterAction);

                    // Then, create the context object, passing the MasterAction instance to it.
                    var context = new MasterActionContext(
                        _currentMasterAction,
                        _journalService,
                        _logger,
                        _cancellationTokenSource.Token,
                        _logFlushProvider);
                    
                    // Run the actual workflow on a background thread. This allows the API call to return
                    // immediately with the new MasterActionId, while the work continues asynchronously.
                    _ = Task.Run(async () =>
                    {
                        // This 'using' block is critical. It sets the "MasterActionId" property in the ambient
                        // NLog context. Any logger called within this block (even static ones) will have access
                        // to this ID, allowing our custom UILoggingTarget to identify and forward the log.
                        // The context is automatically cleared when the block is exited.
                        using (MappedDiagnosticsLogicalContext.SetScoped("MasterActionId", masterActionId))
                        using (MappedDiagnosticsLogicalContext.SetScoped("MasterActionContext", context)) // Add this line
                        {
                            try
                            {
                                await handler.ExecuteAsync(context);
                            }
                            catch (TaskCanceledException)
                            {
                                _logger.LogWarning("Master Action {MasterActionId} was canceled.", masterActionId);
                                context.SetCancelled("Master action was canceled by user request.");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Master Action {MasterActionId} failed with an unhandled exception.", masterActionId);
                                context.SetFailed($"Workflow failed with unhandled exception: {ex.Message}");
                            }
                            finally
                            {
                                if (_currentMasterAction != null)
                                {
                                    await _journalService.RecordMasterActionCompletedAsync(_currentMasterAction);
                                }
                                // This block executes when the workflow is finished (success, failure, or cancellation).
                                _currentMasterAction = null;
                                _cancellationTokenSource?.Dispose();
                                _cancellationTokenSource = null;
                                _singletonLock.Release(); // Release the lock so a new action can be started.
                                _logger.LogInformation("Master Action {MasterActionId} execution finished and singleton lock released.", masterActionId);
                            }
                        }
                    });

                    return _currentMasterAction;
                }
            }
            catch
            {
                // Ensure the lock is released if the initiation process itself throws an exception.
                _singletonLock.Release();
                throw;
            }
        }

        /// <summary>
        /// Retrieves the current status of a Master Action, formatted for the UI.
        /// </summary>
        public async Task<OperationStatusResponse?> GetStatusAsync(string masterActionId)
        {
            var action = _currentMasterAction;
            // If the action isn't the one currently running, try to get it from the journal.
            if (action == null || action.Id != masterActionId)
            {
                action = await _journalService.GetArchivedMasterActionAsync(masterActionId);
                if(action == null)
                {
                    // If it's not in memory and not in the journal, it doesn't exist.
                    return null;
                }
            }

            // This is the facade logic. We map the internal MasterAction state to the DTO the UI expects.
            var response = new OperationStatusResponse
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

            // Handle the final result payload using the "virtual node" concept.
            if (action.IsComplete && action.FinalResultPayload != null)
            {
                response.NodeTasks = new List<OperationNodeTaskStatus>
                {
                    new()
                    {
                        NodeName = "_master",
                        TaskStatus = "Succeeded",
                        Message = "Master action completed with a final result.",
                        ResultPayload = action.FinalResultPayload as Dictionary<string, object>
                    }
                };
            }
            else if (action.CurrentStageOperation != null)
            {
                // If the action is running, show the node tasks from the currently active stage.
                response.NodeTasks = action.CurrentStageOperation.NodeTasks.Select(nt => new OperationNodeTaskStatus
                {
                    NodeName = nt.NodeName,
                    TaskStatus = nt.Status.ToString(),
                    Message = nt.StatusMessage,
                    ResultPayload = nt.ResultPayload
                    // ... map other task properties
                }).ToList();
            }

            return response;
        }

        /// <summary>
        /// Requests cancellation of the currently running Master Action and provides immediate feedback.
        /// </summary>
        /// <returns>An OperationCancelResponse indicating the outcome of the cancellation request.</returns>
        public Task<OperationCancelResponse> RequestCancellationAsync(string masterActionId, string cancelledBy)
        {
            var action = _currentMasterAction;

            // Case 1: No action is running or the ID does not match.
            if (action == null || action.Id != masterActionId)
            {
                _logger.LogWarning("Received cancellation request for non-existent or wrong Master Action ID: {MasterActionId}", masterActionId);
                return Task.FromResult(new OperationCancelResponse
                {
                    OperationId = masterActionId,
                    Status = OperationCancellationRequestStatus.NotFound,
                    Message = "The specified action was not found or is not currently running."
                });
            }

            // Case 2: The action has already completed.
            if (action.IsComplete)
            {
                _logger.LogInformation("Received cancellation request for already completed Master Action {MasterActionId}", masterActionId);
                return Task.FromResult(new OperationCancelResponse
                {
                    OperationId = masterActionId,
                    Status = OperationCancellationRequestStatus.AlreadyCompleted,
                    Message = $"Action already completed with status: {action.OverallStatus}"
                });
            }

            // Case 3: Cancellation is already in progress.
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                _logger.LogInformation("Received redundant cancellation request for Master Action {MasterActionId}", masterActionId);
                 return Task.FromResult(new OperationCancelResponse
                {
                    OperationId = masterActionId,
                    Status = OperationCancellationRequestStatus.CancellationPending,
                    Message = "Action cancellation is already pending."
                });
                 
            }

            // Case 4: Successfully initiate cancellation.
            _logger.LogInformation("Cancellation requested for Master Action {MasterActionId} by {CancelledBy}", masterActionId, cancelledBy);
            _cancellationTokenSource.Cancel();
            action.OverallStatus = OperationOverallStatus.Cancelling; // Update the state
            
            return Task.FromResult(new OperationCancelResponse
            {
                OperationId = masterActionId,
                Status = OperationCancellationRequestStatus.CancellationPending,
                Message = "Cancellation request accepted and is being processed."
            });
        }

    }
}
