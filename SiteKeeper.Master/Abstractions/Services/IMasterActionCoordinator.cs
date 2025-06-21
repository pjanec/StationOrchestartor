using SiteKeeper.Master.Model.InternalData;
using SiteKeeper.Shared.DTOs.API.Operations;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Defines the contract for the service that orchestrates the execution of high-level, multi-stage Master Actions.
    /// This service acts as the primary entry point for the API layer to initiate, monitor, and manage workflows.
    /// </summary>
    public interface IMasterActionCoordinator
    {
        /// <summary>
        /// Initiates a new Master Action workflow based on an API request.
        /// This method finds the appropriate handler for the requested operation type,
        /// creates the execution context, and starts the workflow in the background.
        /// It enforces the singleton execution model, ensuring only one Master Action runs at a time.
        /// </summary>
        /// <param name="request">The API request DTO containing the operation type and parameters.</param>
        /// <param name="user">The user principal who initiated the action.</param>
        /// <returns>A task that represents the asynchronous initiation operation. The task result contains the initial state of the <see cref="MasterAction"/>.</returns>
        /// <exception cref="System.InvalidOperationException">Thrown if another Master Action is already in progress.</exception>
        /// <exception cref="System.NotSupportedException">Thrown if no handler is registered for the requested <see cref="OperationType"/>.</exception>
        Task<MasterAction> InitiateMasterActionAsync(OperationInitiateRequest request, ClaimsPrincipal user);

        /// <summary>
        /// Retrieves the current status of a Master Action, formatted for the API response.
        /// This method is used for polling the state of an ongoing or completed action.
        /// </summary>
        /// <param name="masterActionId">The unique ID of the Master Action to query.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result is an <see cref="OperationStatusResponse"/>
        /// DTO containing the aggregated status, progress, logs, and results of the action.
        /// Returns null if no action with the specified ID is found (or has been archived).
        /// </returns>
        Task<OperationStatusResponse?> GetStatusAsync(string masterActionId);

        /// <summary>
        /// Requests the cancellation of the currently running Master Action.
        /// This will signal a cancellation token in the MasterActionContext,
        /// allowing the running workflow to terminate gracefully.
        /// </summary>
        /// <param name="masterActionId">The ID of the Master Action to cancel.</param>
        /// <param name="cancelledBy">A string identifying the user or system that requested the cancellation.</param>
        /// <returns>A task that represents the asynchronous cancellation request.</returns>
        Task<OperationCancelResponse> RequestCancellationAsync(string masterActionId, string cancelledBy);
    }
} 