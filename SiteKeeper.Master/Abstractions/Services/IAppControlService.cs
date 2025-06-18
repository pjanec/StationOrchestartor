using SiteKeeper.Shared.DTOs.API.SoftwareControl; // For AppStatusInfo
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Defines the contract for a service that handles control and status of individual applications (apps)
    /// running on managed nodes.
    /// </summary>
    /// <remarks>
    /// This service is responsible for querying the current status of applications and initiating control actions
    /// such as start, stop, and restart. It may interact with the <see cref="IMasterActionCoordinatorService"/>
    /// to orchestrate these actions across one or more slave agents, and with <see cref="IAgentConnectionManagerService"/>
    /// for direct communication if needed for status polling or simple commands.
    /// The application definitions and their management are typically based on the active environment manifest.
    /// </remarks>
    public interface IAppControlService
    {
        /// <summary>
        /// Retrieves a list of all manageable applications within the environment, along with their current operational statuses.
        /// This method is typically called by API controllers serving endpoints like GET /api/apps.
        /// </summary>
        /// <param name="filterText">Optional text to filter applications by (e.g., name, description, node).</param>
        /// <param name="sortBy">Optional field name to sort the results by (e.g., "appName", "status", "nodeName").</param>
        /// <param name="sortOrder">Optional sort order ("asc" or "desc").</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of <see cref="AppStatusInfo"/> DTOs.</returns>
        Task<List<AppStatusInfo>> ListAppsAsync(string? filterText, string? sortBy, string? sortOrder);

        // Other methods from swagger like:
        // Task<AppStatusInfo> StartAppAsync(string appId); // Typically triggers an operation via IMasterActionCoordinatorService
        // Task<AppStatusInfo> StopAppAsync(string appId);  // Typically triggers an operation via IMasterActionCoordinatorService
        // Task<AppStatusInfo> RestartAppAsync(string appId); // Typically triggers an operation via IMasterActionCoordinatorService
        // These will be added as we implement the corresponding API endpoints if they fall under this service.
    }
} 