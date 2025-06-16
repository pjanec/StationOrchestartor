using SiteKeeper.Shared.DTOs.API.SoftwareControl; // For AppStatusInfo
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Defines the contract for a service that handles control and status of individual applications (apps).
    /// </summary>
    public interface IAppControlService
    {
        /// <summary>
        /// Lists all manageable applications and their current statuses.
        /// </summary>
        Task<List<AppStatusInfo>> ListAppsAsync(string? filterText, string? sortBy, string? sortOrder);

        // Other methods from swagger like:
        // Task<AppStatusInfo> StartAppAsync(string appId);
        // Task<AppStatusInfo> StopAppAsync(string appId);
        // Task<AppStatusInfo> RestartAppAsync(string appId);
        // These will be added as we implement the corresponding API endpoints if they fall under this service.
    }
} 