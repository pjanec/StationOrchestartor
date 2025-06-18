using SiteKeeper.Shared.DTOs.API.SoftwareControl; // For PlanListResponse
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Defines the contract for a service that handles the control and status reporting of application plans.
    /// An application plan is a logical grouping of multiple applications that are managed as a single unit.
    /// </summary>
    /// <remarks>
    /// This service is responsible for retrieving the status of defined application plans and for initiating
    /// collective actions (e.g., start, stop, restart) on all applications within a plan.
    /// Such actions are typically orchestrated via the <see cref="IMasterActionCoordinatorService"/>.
    /// This service is primarily consumed by API controllers serving plan-related endpoints (e.g., /api/plans).
    /// </remarks>
    public interface IPlanControlService
    {
        /// <summary>
        /// Retrieves a list of all defined application plans, along with their current aggregated operational statuses.
        /// This method is typically called by API controllers serving an endpoint like GET /api/plans.
        /// </summary>
        /// <param name="filterText">Optional text to filter plans by (e.g., name, description).</param>
        /// <param name="sortBy">Optional field name to sort the results by (e.g., "name", "status").</param>
        /// <param name="sortOrder">Optional sort order ("asc" or "desc").</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a <see cref="PlanListResponse"/> DTO,
        /// which includes a list of <see cref="PlanInfo"/> objects.
        /// </returns>
        Task<PlanListResponse> ListPlansAsync(string? filterText, string? sortBy, string? sortOrder);

        // Other methods from swagger like:
        // Task<OperationInitiationResponse> StartPlanAsync(string planId); // Typically triggers an operation via IMasterActionCoordinatorService
        // Task<OperationInitiationResponse> StopPlanAsync(string planId);  // Typically triggers an operation via IMasterActionCoordinatorService
        // Task<OperationInitiationResponse> RestartPlanAsync(string planId); // Typically triggers an operation via IMasterActionCoordinatorService
        // These will be added as we implement the corresponding API endpoints if they fall under this service.
    }
} 