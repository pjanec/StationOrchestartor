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

namespace SiteKeeper.Master.Services
{
    /// <summary>
    /// Orchestrates the execution of high-level, multi-stage Master Actions within the SiteKeeper Master Agent.
    /// </summary>
    /// <remarks>
    /// This service acts as the central "General Contractor" for all workflows initiated via API requests.
    /// It implements <see cref="IMasterActionCoordinatorService"/> and ensures key operational constraints, such as:
    /// <list type="bullet">
    ///   <item><description>Singleton Execution: Only one <see cref="MasterAction"/> can run at a time, enforced by a <see cref="SemaphoreSlim"/>.</description></item>
    ///   <item><description>Handler Resolution: It uses <see cref="IServiceProvider"/> to dynamically resolve the correct <see cref="IMasterActionHandler"/> for a given <see cref="OperationType"/> from the dependency injection container.</description></item>
    ///   <item><description>Context Management: It creates and manages the <see cref="MasterActionContext"/> and <see cref="CancellationTokenSource"/> for each workflow.</description></item>
    ///   <item><description>Lifecycle Journaling: It collaborates with <see cref="IJournalService"/> to record the initiation and completion of each Master Action.</description></item>
    ///   <item><description>Background Execution: Workflows are executed on background threads (<see cref="Task.Run(Func{Task})"/>) to allow API calls to return quickly.</description></item>
    ///   <item><description>Contextual Logging: It utilizes <see cref="MappedDiagnosticsLogicalContext"/> to ensure logs generated within a workflow are tagged with the <see cref="MasterAction.Id"/>.</description></item>
    ///   <item><description>Log Flushing: It leverages a log flush provider (obtained from <see cref="MasterNLogSetupService"/>) to ensure logs are persisted before critical state changes.</description></item>
    /// </list>
    /// This service is the sole entry point for the API layer to initiate, monitor, and request cancellation of Master Actions.
    /// </remarks>
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

        /// <summary>
        /// Initializes a new instance of the <see cref="MasterActionCoordinatorService"/> class.
        /// </summary>
        /// <param name="logger">The logger for recording service activities and errors.</param>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to resolve <see cref="IMasterActionHandler"/> instances.</param>
        /// <param name="journalService">The <see cref="IJournalService"/> for recording the lifecycle of Master Actions.</param>
        /// <param name="nlogSetupService">The <see cref="MasterNLogSetupService"/> which provides access to the UI logging target for flushing logs.</param>
        public MasterActionCoordinatorService(
            ILogger<MasterActionCoordinatorService> logger,
            IServiceProvider serviceProvider,
            IJournalService journalService,
            MasterNLogSetupService nlogSetupService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _journalService = journalService;
            _logFlushProvider = nlogSetupService.GetUiLoggingTarget().FlushAsync;
        }

        /// <summary>
        /// Initiates a new Master Action workflow based on an API request from an authenticated user.
        /// It ensures only one master action runs at a time, resolves the appropriate handler for the requested operation type,
        /// sets up the execution context, journals the initiation, and starts the workflow in a background task.
        /// </summary>
        /// <param name="request">The <see cref="OperationInitiateRequest"/> DTO containing the operation type and its specific parameters.</param>
        /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user who initiated the action. Used for auditing.</param>
        /// <returns>A task that represents the asynchronous initiation. The task result contains the initial state of the newly created <see cref="MasterAction"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if another Master Action is already in progress, as this service enforces singleton execution.</exception>
        /// <exception cref="NotSupportedException">Thrown if no <see cref="IMasterActionHandler"/> is registered in the dependency injection container for the requested <see cref="OperationType"/>.</exception>
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
                                // Ensure all logs generated by the handler have been processed by NLog targets
                                // before we mark the action as completed in the journal service.
                                try
                                {
                                    await context.FlushLogsAsync();
                                    _logger.LogInformation("Final log stream for Master Action {MasterActionId} flushed.", masterActionId);
                                }
                                catch (Exception flushEx)
                                {
                                    _logger.LogError(flushEx, "Failed to flush final logs for Master Action {MasterActionId}.", masterActionId);
                                }

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
        /// Retrieves the current status of a specific Master Action, formatted for API responses.
        /// </summary>
        /// <remarks>
        /// This method first checks if the requested action is the currently running one.
        /// If not, it attempts to retrieve the action's state from the journal via <see cref="IJournalService.GetArchivedMasterActionAsync"/>.
        /// The internal <see cref="MasterAction"/> state is then mapped to an <see cref="OperationStatusResponse"/> DTO.
        /// This includes mapping node task details or the final result payload for master-only operations.
        /// </remarks>
        /// <param name="masterActionId">The unique ID of the Master Action to query.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result is an <see cref="OperationStatusResponse"/>
        /// DTO containing the aggregated status, progress, logs, and results of the action.
        /// Returns null if no action (neither currently running nor archived) with the specified ID is found.
        /// </returns>
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
                    _logger.LogWarning("Status requested for MasterActionId '{MasterActionId}', but it was not found as current or archived.", masterActionId);
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
                Parameters = new Dictionary<string, object>(action.Parameters), // Ensure parameters are copied
                RecentLogs = action.GetRecentLogs() // Get a copy of recent logs
            };

			// Always prioritize showing the actual node tasks from the completed stage if they exist.
			if( action.CurrentStageOperation?.NodeTasks.Any() == true )
			{
				response.NodeTasks = action.CurrentStageOperation.NodeTasks.Select( nt => new OperationNodeTaskStatus
				{
					NodeName = nt.NodeName,
					TaskStatus = nt.Status.ToString(), // Assuming NodeTask.Status is an enum
					Message = nt.StatusMessage,
					ResultPayload = nt.ResultPayload, // This should be a Dictionary<string, object> or null
                    TaskStartTime = nt.StartTime,
                    TaskEndTime = nt.EndTime
				} ).ToList();
				
                var jsonizedRespose = System.Text.Json.JsonSerializer.Serialize( response, new System.Text.Json.JsonSerializerOptions { WriteIndented = false } );
				_logger.LogDebug( "Mapping node tasks from the completed stage for Master Action {MasterActionId}. response: {Response}", action.Id, jsonizedRespose );
			}
			// As a FALLBACK, if the operation is complete but has no node tasks (e.g., a master-only operation),
			// then represent its outcome using the virtual master node.
			else if( action.IsComplete && action.FinalResultPayload != null )
			{
                // Attempt to ensure FinalResultPayload is presented as Dictionary<string, object> if possible
                Dictionary<string, object>? payloadDict = action.FinalResultPayload as Dictionary<string, object>;
                if (payloadDict == null && action.FinalResultPayload != null)
                {
                    try
                    {
                        // If it's a JsonElement, try to deserialize it into the dictionary.
                        if (action.FinalResultPayload is JsonElement jsonElement)
                        {
                             payloadDict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
                        }
                        else // Fallback: wrap it if it's not already a dictionary.
                        {
                            payloadDict = new Dictionary<string, object> { { "result", action.FinalResultPayload } };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not convert FinalResultPayload to Dictionary<string, object> for MasterActionId {MasterActionId}. Payload Type: {PayloadType}", action.Id, action.FinalResultPayload.GetType().FullName);
                        payloadDict = new Dictionary<string, object> { { "error", "Failed to serialize result payload" }, { "originalType", action.FinalResultPayload.GetType().FullName } };
                    }
                }


				response.NodeTasks = new List<OperationNodeTaskStatus>
						   {
							   new()
							   {
								   NodeName = "_master", // Virtual node name for master-only results
								   TaskStatus = OperationOverallStatus.Succeeded.ToString(), // Assuming success if there's a final payload
								   Message = "Master action completed with a final result.",
								   ResultPayload = payloadDict,
                                   TaskStartTime = action.StartTime, // Approximate task time with action time
                                   TaskEndTime = action.EndTime
							   }
						   };
                var jsonizedRespose = System.Text.Json.JsonSerializer.Serialize( response, new System.Text.Json.JsonSerializerOptions { WriteIndented = false } );
				_logger.LogDebug( "No node tasks found. Using fallback virtual master node for Master Action {MasterActionId}. response: {Response}", action.Id, jsonizedRespose );
			}
			else
			{
				_logger.LogDebug( "No node tasks or final result payload available to map for Master Action {MasterActionId}.", action.Id );
			}

            return response;
        }

        /// <summary>
        /// Requests cancellation of the currently running Master Action.
        /// </summary>
        /// <remarks>
        /// This method checks if the specified action is currently running and, if so, signals its <see cref="CancellationTokenSource"/>.
        /// If the action has already completed (either actively or found in the journal), it returns an appropriate status.
        /// If the action ID is not found, it indicates that the action does not exist or was never initiated.
        /// </remarks>
        /// <param name="masterActionId">The unique ID of the Master Action to cancel.</param>
        /// <param name="cancelledBy">A string identifying the user or system entity that requested the cancellation, for auditing purposes.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result is an <see cref="OperationCancelResponse"/>
        /// indicating the outcome of the cancellation request (e.g., pending, already completed, not found).
        /// </returns>
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
                action.OverallStatus = OperationOverallStatus.Cancelling;
        
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
