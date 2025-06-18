using SiteKeeper.Shared.DTOs.API.Diagnostics;
using SiteKeeper.Shared.DTOs.API.Operations;
using System.Threading.Tasks;
using System.Security.Claims;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Service interface for handling diagnostic operations, including listing available checks,
    /// running diagnostics, and collecting diagnostic data such as application logs.
    /// </summary>
    /// <remarks>
    /// This service is typically consumed by API controllers that expose diagnostic-related endpoints.
    /// For operations that involve actions on slave nodes (e.g., running checks, collecting logs),
    /// this service may interact with the <see cref="IMasterActionCoordinatorService"/> to orchestrate those actions.
    /// </remarks>
    public interface IDiagnosticsService
    {
        /// <summary>
        /// Lists all available health checks that can be run in the system.
        /// Typically called by API controllers serving endpoints like GET /api/diagnostics/health-checks.
        /// </summary>
        /// <returns>A <see cref="HealthCheckListResponse"/> containing the available health checks.</returns>
        Task<HealthCheckListResponse> ListAvailableHealthChecksAsync();

        /// <summary>
        /// Initiates a diagnostic run based on the provided request.
        /// This method likely triggers an operation via the <see cref="IMasterActionCoordinatorService"/>.
        /// </summary>
        /// <param name="request">The <see cref="RunHealthChecksRequest"/> detailing which checks to run and on which nodes.</param>
        /// <param name="user">The <see cref="ClaimsPrincipal"/> of the user initiating the diagnostic run, for auditing and authorization purposes.</param>
        /// <returns>An <see cref="OperationInitiationResponse"/> for tracking the asynchronous diagnostic operation.</returns>
        Task<OperationInitiationResponse> RunDiagnosticsAsync(RunHealthChecksRequest request, ClaimsPrincipal user);

        /// <summary>
        /// Retrieves a list of applications that are discoverable for diagnostic purposes.
        /// </summary>
        /// <returns>An <see cref="AppListResponse"/> containing a list of <see cref="AppInfo"/> objects.</returns>
        /// <remarks>
        /// This method corresponds to the GET /api/diagnostics/apps endpoint.
        /// The actual list of apps might be sourced from configuration, a discovery service, or a predefined list.
        /// </remarks>
        Task<AppListResponse> ListDiagnosticAppsAsync();

        /// <summary>
        /// Retrieves a list of data package types available for a specific application (e.g., types of logs or dumps that can be collected).
        /// </summary>
        /// <param name="appId">The unique identifier of the application.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
        /// The task result contains an <see cref="AppDataPackageTypesResponse"/> if the application is found and has data package types,
        /// or <c>null</c> if the application is not found or has no defined package types.
        /// </returns>
        /// <remarks>
        /// This method corresponds to the GET /api/diagnostics/apps/{appId}/data-package-types endpoint.
        /// </remarks>
        Task<AppDataPackageTypesResponse?> GetAppDataPackageTypesAsync(string appId);

        /// <summary>
        /// Initiates an operation to collect logs or other data packages for a specific application from target nodes.
        /// This method likely triggers an operation via the <see cref="IMasterActionCoordinatorService"/>.
        /// </summary>
        /// <param name="request">The <see cref="CollectLogsRequest"/> detailing the application, data package type, and target nodes.</param>
        /// <param name="user">The <see cref="ClaimsPrincipal"/> of the user initiating the log collection, for auditing and authorization purposes.</param>
        /// <returns>An <see cref="OperationInitiationResponse"/> for tracking the asynchronous log collection operation.</returns>
        /// <remarks>This method corresponds to the POST /api/operations/diagnostics/collect-logs endpoint.</remarks>
        Task<OperationInitiationResponse> CollectAppLogsAsync(CollectLogsRequest request, ClaimsPrincipal user);

        // Other methods from swagger like:
        // Task<OnlineUpdateCheckResponse> CheckForOnlineUpdatesAsync(); 
        // These will be added as we implement the corresponding API endpoints if they fall under this service.
    }
} 