using SiteKeeper.Shared.DTOs.API.SoftwareControl; // For PlanListResponse
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Defines the contract for a service that handles control and status of application plans.
    /// </summary>
    public interface IPlanControlService
    {
        /// <summary>
        /// Lists all defined application plans and their current aggregated statuses.
        /// </summary>
        Task<PlanListResponse> ListPlansAsync(string? filterText, string? sortBy, string? sortOrder);

        // Other methods from swagger like:
        // Task<OperationInitiationResponse> StartPlanAsync(string planId);
        // Task<OperationInitiationResponse> StopPlanAsync(string planId);
        // Task<OperationInitiationResponse> RestartPlanAsync(string planId);
        // These will be added as we implement the corresponding API endpoints if they fall under this service.
    }
} 