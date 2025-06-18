using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.DTOs.API.Diagnostics; // For CollectLogsRequest
using SiteKeeper.Shared.DTOs.API.Operations; // For OperationInitiationResponse
using SiteKeeper.Shared.DTOs.API.PackageManagement; // Added for PackageChangeVersionRequest
using SiteKeeper.Shared.DTOs.Common; // Added for ErrorResponse
using SiteKeeper.Shared.Security; // Corrected and now relevant namespace for SiteKeeperRoles
using SiteKeeper.Shared.Enums;
using System.Security.Claims;
using System.Collections.Generic;
using System.Linq;
using SiteKeeper.Master.Web.Apis;
using System; // Required for Exception
using System.Threading.Tasks;
using SiteKeeper.Master;

namespace SiteKeeper.Master.Web.Apis
{
    /// <summary>
    /// Defines API endpoints related to initiating, monitoring, and managing various system operations.
    /// This is a partial class, and these endpoints are registered via the main <see cref="ApiEndpoints.MapAll(WebApplication)"/> method.
    /// </summary>
    /// <remarks>
    /// This class groups various operation-related endpoints, such as initiating environment updates,
    /// backups, restores, diagnostic runs, package management tasks, and other coordinated workflows.
    /// It utilizes a generic helper method <see cref="MapPostOperation{TRequest}"/> for many standard operation initiations
    /// and also defines custom mappings for operations requiring more specific handling (e.g., test operations, status polling, cancellation).
    /// All endpoints are grouped under <c>/api/v1/operations</c> and require authorization, with specific role requirements per operation.
    /// Operations are typically long-running and are coordinated by the <see cref="IMasterActionCoordinatorService"/>.
    /// </remarks>
    public static partial class ApiEndpointsOperations
    {
        private const string ApiTag = "Operations"; // Main tag for most operations
        private const string DiagnosticsApiTag = "Diagnostics"; // Specific tag for diagnostic operations

        /// <summary>
        /// Maps all API endpoints related to system operations.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to which the endpoints will be mapped (typically the <see cref="WebApplication"/> instance).
        /// The parameter name <paramref name="app"/> is used here instead of <c>builder</c> for consistency with other <c>Map...Api</c> methods in the project.</param>
        /// <param name="guiHostConstraint">A hostname constraint string to apply to these endpoints, ensuring they are only accessible via specific hostnames configured for GUI/API access.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with the newly mapped operation endpoints.</returns>
        /// <remarks>
        /// This method creates a route group for <c>/api/v1/operations</c> which requires general authorization.
        /// It then uses a combination of a generic helper <see cref="MapPostOperation{TRequest}"/> for standardized
        /// operation initiation endpoints and custom lambda definitions for more specialized operational controls like
        /// status retrieval, cancellation, and specific diagnostic actions.
        /// Each operation initiation typically results in a <see cref="MasterAction"/> being created and returns an
        /// <see cref="OperationInitiationResponse"/> with a 202 Accepted status.
        /// </remarks>
        public static IEndpointRouteBuilder MapOperationsApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            var operationsGroup = app.MapGroup("/api/v1/operations")
                .WithTags(ApiTag)
                 // General authorization for the group; specific endpoints also verify roles via MapPostOperation or inline checks.
                .RequireAuthorization()
                .RequireHost(guiHostConstraint);

            //
            // Endpoints that FIT the standard operation pattern are refactored below using MapPostOperation helper
            //
            operationsGroup.MapPostOperation<EnvUpdateRequest>("/env-update-online", OperationType.EnvUpdateOnline, SiteKeeperRoles.BasicAdmin,
                "Initiate Online Environment Update", "Starts an online environment update to the specified target version defined in the request body.");

            operationsGroup.MapPostOperation<OfflineScanSourcesRequest>("/offline-update/scan-sources", OperationType.OfflineScanSources, SiteKeeperRoles.BasicAdmin,
                "Scan Sources for Offline Bundles", "Initiates a scan of selected offline sources (e.g., USB drives, network shares) to find available offline update bundles.");

            operationsGroup.MapPostOperation<OfflineUpdateInitiateRequest>("/offline-update/initiate", OperationType.EnvUpdateOffline, SiteKeeperRoles.BasicAdmin,
                "Initiate Offline Environment Update", "Initiates an offline environment update using a specific update bundle selected from a scanned offline source.");

            operationsGroup.MapPostOperation<EmptyRequest>("/env-backup", OperationType.EnvBackup, SiteKeeperRoles.BasicAdmin,
                "Initiate Environment Backup", "Starts a predefined environment backup operation. Request body is empty.");

            operationsGroup.MapPostOperation<EnvRestoreRequest>("/env-restore", OperationType.EnvRestore, SiteKeeperRoles.BasicAdmin,
                "Initiate Environment Restore", "Initiates an environment restore from a specific backup, identified by a journal record ID in the request body.");

            operationsGroup.MapPostOperation<EnvRevertRequest>("/env-revert", OperationType.EnvRevert, SiteKeeperRoles.BasicAdmin,
                "Initiate Environment Revert", "Initiates an environment revert to a specific previously known 'pure' state, identified by a journal record ID in the request body.");

            operationsGroup.MapPostOperation<EmptyRequest>("/env-verify", OperationType.EnvVerify, SiteKeeperRoles.BasicAdmin,
                "Initiate Environment Verification", "Starts an environment verification process against its manifest or baseline. Request body is empty.");

            operationsGroup.MapPostOperation<EmptyRequest>("/env-sync", OperationType.EnvSync, SiteKeeperRoles.BasicAdmin,
                "Initiate Environment Sync", "Starts a general environment synchronization process. Request body is empty.");

            operationsGroup.MapPostOperation<PackageChangeVersionRequest>("/packages/change-version", OperationType.PackageChangeVersion, SiteKeeperRoles.AdvancedAdmin,
                "Change Package Version", "Initiates an operation to change the version of a specified package on designated target nodes, as detailed in the request body.");

            operationsGroup.MapPostOperation<PackageNameRequest>("/packages/revert-deviations", OperationType.PackageRevertDeviations, SiteKeeperRoles.AdvancedAdmin,
                "Revert Package Deviations", "Initiates an operation to revert a specific package on target nodes to its version and configuration as defined in the current environment manifest. Package name is provided in the request body.");

            operationsGroup.MapPostOperation<PackageRefreshRequest>("/packages/refresh", OperationType.PackageRefresh, SiteKeeperRoles.AdvancedAdmin,
                "Refresh Package(s)", "Initiates an operation to refresh selected package(s) or all refreshable packages on target nodes, as specified in the request body.");

            operationsGroup.MapPostOperation<EmptyRequest>("/system-software/start", OperationType.SystemSoftwareStart, SiteKeeperRoles.Operator,
                "Start All System Software", "Initiates an operation to start all managed system software components across the environment. Request body is empty.");

            operationsGroup.MapPostOperation<EmptyRequest>("/system-software/stop", OperationType.SystemSoftwareStop, SiteKeeperRoles.Operator,
                "Stop All System Software", "Initiates an operation to stop all managed system software components across the environment. Request body is empty.");

            operationsGroup.MapPostOperation<EmptyRequest>("/system-software/restart", OperationType.SystemSoftwareRestart, SiteKeeperRoles.Operator,
                "Restart All System Software", "Initiates an operation to restart all managed system software components across the environment. Request body is empty.");

            //
            // Endpoints with CUSTOM logic remain as they were, with XML documentation added to their lambda handlers
            //
            // Defines POST /api/v1/operations/test-op
            // Initiates a special test operation with simulated behaviors.
            // Requires AdvancedAdmin role. Calls IMasterActionCoordinatorService and IAuditLogService.
            // Request body: TestOpRequest. Response: 202 Accepted with OperationInitiationResponse.
            operationsGroup.MapPost("/test-op",
                /// <summary>
                /// Initiates a special test operation designed to simulate various orchestration scenarios,
                /// including master-side and slave-side failures or specific behaviors.
                /// Requires AdvancedAdmin privileges.
                /// </summary>
                /// <param name="request">The <see cref="TestOpRequest"/> DTO containing parameters to control master and slave simulation behavior.</param>
                /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user (must be AdvancedAdmin).</param>
                /// <param name="masterActionService">The <see cref="IMasterActionCoordinatorService"/> to initiate the test operation.</param>
                /// <param name="auditLog">The <see cref="IAuditLogService"/> for recording this test operation initiation.</param>
                /// <param name="httpContext">The <see cref="HttpContext"/> for accessing request details like client IP.</param>
                /// <returns>
                /// An <see cref="IResult"/> that is <see cref="Results.Accepted(string, object)"/> with an <see cref="OperationInitiationResponse"/> on successful initiation.
                /// Returns <see cref="Results.Forbid()"/> if the user is not an AdvancedAdmin.
                /// </returns>
                async (
                    [FromBody] TestOpRequest request,
                    ClaimsPrincipal user,
                    [FromServices] IMasterActionCoordinatorService masterActionService,
                    [FromServices] IAuditLogService auditLog,
                    HttpContext httpContext) =>
                {
                    // Authorization: Requires AdvancedAdmin.
                    if (!user.IsAdvancedAdmin())
                    {
                        return Results.Forbid();
                    }

                    var username = user.GetUsername() ?? "unknown_admin";
                    var operationDescription = $"Orchestration Test (Master: {request.MasterFailure}, Slave: {request.SlaveBehavior}) initiated by {username}";

                    // Use the DtoExtensions.ToDictionary() helper to serialize the entire request object
                    // into the parameters dictionary. This makes all test settings available to the handler.
                    var parameters = request.ToDictionary(); // Assumes DtoExtensions.ToDictionary() exists for TestOpRequest

                    var initiateRequest = new OperationInitiateRequest
                    {
                        OperationType = OperationType.OrchestrationTest,
                        Description = operationDescription,
                        Parameters = parameters
                    };
                    
                    var masterAction = await masterActionService.InitiateMasterActionAsync(initiateRequest, user);
                    
                    await auditLog.LogActionAsync(
                        username: username,
                        action: "InitiateTestOperation",
                        targetResource: "OrchestrationFramework", // Target is the framework itself
                        parameters: parameters,
                        outcome: AuditLogOutcome.Success.ToString(), // Audits the initiation success
                        details: $"Test operation '{masterAction.Id}' (MasterFailure: {request.MasterFailure}, SlaveBehavior: {request.SlaveBehavior}) initiated by user '{username}'.",
                        clientIpAddress: httpContext.GetClientIpAddress());

                    var location = $"/api/v1/operations/{masterAction.Id}"; // Location header for the newly created resource (operation status)
                    var response = new OperationInitiationResponse { OperationId = masterAction.Id, Message = "Test operation initiated successfully." };

                    return Results.Accepted(location, response);
                })
                .WithName("InitiateTestOperation")
                .WithSummary("Initiate a Test Orchestration Operation")
                .WithDescription("A test-only endpoint to trigger an operation with simulated master and slave behavior (success, failure, timeout, cancellation). Requires Advanced Admin role. *Audit Logged*")
                .Produces<OperationInitiationResponse>(StatusCodes.Status202Accepted)
                .Produces<ErrorResponse>(StatusCodes.Status400BadRequest) // If request DTO is invalid
                .Produces(StatusCodes.Status403Forbidden)
                .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsAdvancedAdmin()));

            // Defines GET /api/v1/operations/{operationId}
            // Retrieves the detailed status of a specific Master Action by its ID.
            // Requires Observer role. Calls IMasterActionCoordinatorService.GetStatusAsync.
            operationsGroup.MapGet("/{operationId}",
                /// <summary>
                /// Retrieves the detailed current status, progress, logs, and results for a specific asynchronous Master Action,
                /// identified by its <paramref name="operationId"/>.
                /// This endpoint is typically polled by clients to track an operation's lifecycle.
                /// Requires Observer or higher privileges.
                /// </summary>
                /// <param name="operationId">The unique identifier of the Master Action (operation) to query.</param>
                /// <param name="masterActionService">The <see cref="IMasterActionCoordinatorService"/> for retrieving operation status.</param>
                /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
                /// <returns>
                /// An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with an <see cref="OperationStatusResponse"/>
                /// containing the detailed status if the operation is found.
                /// Returns <see cref="Results.NotFound(object?)"/> with an <see cref="ErrorResponse"/> if no operation with the specified ID is found.
                /// Returns <see cref="Results.Forbid()"/> if unauthorized.
                /// </returns>
                async (string operationId, [FromServices] IMasterActionCoordinatorService masterActionService, ClaimsPrincipal user) =>
                {
                    // Authorization: Requires Observer or higher.
                    if (!user.IsObserverOrHigher())
                    {
                        return Results.Forbid();
                    }
                    var statusResponse = await masterActionService.GetStatusAsync(operationId);
                    return statusResponse is not null
                        ? Results.Ok(statusResponse)
                        : Results.NotFound(new ErrorResponse { Error = "NotFound", Message = $"Master Action with ID '{operationId}' not found." });
                })
                .WithName("GetOperationStatus")
                .WithSummary("Get Detailed Operation Status")
                .WithDescription("Retrieves the detailed status and results of a specific asynchronous operation by its ID. This endpoint can be polled to track the progress and final outcome of an operation. Requires Observer role or higher.")
                .Produces<OperationStatusResponse>(StatusCodes.Status200OK)
                .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status401Unauthorized)
                .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsObserverOrHigher()));

            // Defines POST /api/v1/operations/diagnostics/collect-logs
            // Initiates an operation to collect logs or other data packages for a specific application.
            // Requires Operator role. Calls IDiagnosticsService.CollectAppLogsAsync, which in turn initiates a MasterAction.
            // Request body: CollectLogsRequest. Response: 202 Accepted with OperationInitiationResponse.
            operationsGroup.MapPost("/diagnostics/collect-logs",
                /// <summary>
                /// Initiates an operation to collect specified data packages (e.g., logs, dumps) for an application
                /// from target nodes. Requires Operator or higher privileges.
                /// </summary>
                /// <param name="request">The <see cref="CollectLogsRequest"/> DTO detailing the application, data package type, and target nodes.</param>
                /// <param name="diagnosticsService">The <see cref="IDiagnosticsService"/> to handle the log collection request (likely by initiating a MasterAction).</param>
                /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
                /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> for creating a logger instance.</param>
                /// <returns>
                /// An <see cref="IResult"/> that is <see cref="Results.Accepted(string, object)"/> with an <see cref="OperationInitiationResponse"/> on successful initiation.
                /// Returns <see cref="Results.BadRequest(object)"/> with an <see cref="ErrorResponse"/> if the request is invalid or initiation fails.
                /// Returns <see cref="Results.Forbid()"/> if unauthorized (handled by group policy).
                /// </returns>
                async (
                    [FromBody] CollectLogsRequest request,
                    [FromServices] IDiagnosticsService diagnosticsService,
                    ClaimsPrincipal user,
                    [FromServices] ILoggerFactory loggerFactory) =>
                {
                    var logger = loggerFactory.CreateLogger("SiteKeeper.Master.Web.Apis.Operations"); // Specific logger for this endpoint handler
                    var username = user.GetUsername();
                    logger.LogInformation("API: Collect logs request received from user '{Username}' for AppId: {AppId}, PackageType: {PackageTypeId}.",
                        username, request.AppId, request.DataPackageTypeId);

                    // Basic validation, more can be added in the service layer or model validation
                    if (string.IsNullOrWhiteSpace(request.AppId) || string.IsNullOrWhiteSpace(request.DataPackageTypeId))
                    {
                        return Results.BadRequest(new ErrorResponse
                        {
                            Error = "BadRequest",
                            Message = "AppId and DataPackageTypeId are required for log collection."
                        });
                    }
                    if ((!request.AllNodes.HasValue || !request.AllNodes.Value) && (request.NodeNames == null || !request.NodeNames.Any()))
                    {
                        logger.LogWarning("API: Invalid collect logs request from {Username}: No nodes specified and AllNodes is false.", username);
                        return Results.BadRequest(new ErrorResponse
                        {
                            Error = "BadRequest",
                            Message = "Log collection requires at least one target node or 'allNodes' to be true."
                        });
                    }

                    // IDiagnosticsService.CollectAppLogsAsync is expected to initiate a MasterAction and return its ID.
                    // Audit logging for this action is now expected to be handled within CollectAppLogsAsync or the MasterActionHandler it triggers.
                    var response = await diagnosticsService.CollectAppLogsAsync(request, user);

                    if (string.IsNullOrEmpty(response.OperationId)) // Check if operation initiation failed (e.g., as indicated by the service)
                    {
                         logger.LogError("Log collection initiation failed for AppId {AppId} by user {Username}. Reason: {FailureMessage}", request.AppId, username, response.Message);
                         return Results.BadRequest(new ErrorResponse { Error = "OperationInitiationFailed", Message = response.Message });
                    }

                    logger.LogInformation("Log collection operation initiated with ID {OperationId} for AppId {AppId} by user {Username}.", response.OperationId, request.AppId, username);
                    return Results.Accepted($"/api/v1/operations/{response.OperationId}", response);
                })
                .WithName("CollectAppLogs")
                .WithTags(DiagnosticsApiTag) // Also tagged as Diagnostics as it pertains to app diagnostics data
                .WithSummary("Collect Logs/Dumps for an App")
                .WithDescription("Collects logs and memory dumps for a specific application from target nodes. Requires Operator role or higher. The initiation of this operation is audit logged.")
                .Produces<OperationInitiationResponse>(StatusCodes.Status202Accepted)
                .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsOperatorOrHigher()));

            // Note: The /packages/optional/* endpoints have been removed as they are duplicates of the logic in the new helper.
            // They can be added back by calling the helper if desired:
            // operationsGroup.MapPostOperation<PackageNameRequest>("/packages/optional/install", OperationType.PackageOptionalInstall, SiteKeeperRoles.BasicAdmin, "Install Optional Package", "Initiates the installation of an optional package.");
            // operationsGroup.MapPostOperation<PackageNameRequest>("/packages/optional/uninstall", OperationType.PackageOptionalUninstall, SiteKeeperRoles.BasicAdmin, "Uninstall Optional Package", "Initiates the uninstallation of an optional package.");

            // Defines POST /api/v1/operations/{operationId}/cancel
            // Requests cancellation of an ongoing Master Action.
            // Requires Operator role. Calls IMasterActionCoordinatorService.RequestCancellationAsync and logs to audit.
            operationsGroup.MapPost("/{operationId}/cancel",
            /// <summary>
            /// Requests cancellation of an ongoing Master Action identified by its <paramref name="operationId"/>.
            /// Requires Operator or higher privileges.
            /// </summary>
            /// <param name="operationId">The unique identifier of the Master Action (operation) to be cancelled.</param>
            /// <param name="masterActionService">The <see cref="IMasterActionCoordinatorService"/> to handle the cancellation request.</param>
            /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user making the request.</param>
            /// <param name="auditLog">The <see cref="IAuditLogService"/> for recording the cancellation attempt.</param>
            /// <param name="httpContext">The <see cref="HttpContext"/> for accessing request details.</param>
            /// <param name="logger">A logger specifically for this endpoint, obtained via dependency injection using <see cref="ILogger{TCategoryName}"/> with <see cref="MasterConfig"/> as category for example, or a more specific category.</param>
            /// <returns>
            /// An <see cref="IResult"/> representing the outcome:
            /// <list type="bullet">
            ///   <item><description><see cref="Results.Ok(object?)"/> with <see cref="OperationCancelResponse"/> if cancellation is pending.</description></item>
            ///   <item><description><see cref="Results.NotFound(object?)"/> with <see cref="ErrorResponse"/> if the operation ID is not found.</description></item>
            ///   <item><description><see cref="Results.Conflict(object?)"/> with <see cref="ErrorResponse"/> if the operation has already completed or does not support cancellation.</description></item>
            ///   <item><description><see cref="Results.Forbid()"/> if unauthorized.</description></item>
            /// </list>
            /// </returns>
            async (string operationId, [FromServices] IMasterActionCoordinatorService masterActionService, ClaimsPrincipal user, [FromServices] IAuditLogService auditLog, HttpContext httpContext, [FromServices] ILogger<ApiEndpointsOperations> logger) => // Using ApiEndpointsOperations as logger category
            {
                // Authorization: Requires Operator or higher.
                if (!user.IsOperatorOrHigher())
                {
                    logger.LogWarning("User {User} forbidden to cancel operation {OperationId}", user.GetUsername() ?? "unknown", operationId);
                    return Results.Forbid();
                }
                var username = user.GetUsername() ?? "unknown_canceller";

                logger.LogInformation("User {User} requesting cancellation for operation {OperationId}", username, operationId);

                OperationCancelResponse cancelResponse = await masterActionService.RequestCancellationAsync(operationId, username);
    
                bool isRequestConsideredSuccessAudit = cancelResponse.Status == OperationCancellationRequestStatus.CancellationPending || cancelResponse.Status == OperationCancellationRequestStatus.AlreadyCompleted;
                await auditLog.LogActionAsync(
                    username: username,
                    action: "RequestOperationCancellation", // Changed from RequestMasterActionCancellation to be more generic for API
                    targetResource: $"Operation:{operationId}", // Changed from MasterAction
                    parameters: null,
                    outcome: isRequestConsideredSuccessAudit ? AuditLogOutcome.Success.ToString() : AuditLogOutcome.Failure.ToString(),
                    details: $"User '{username}' requested cancellation for operation '{operationId}'. Service response: {cancelResponse.Message} (Status: {cancelResponse.Status})",
                    clientIpAddress: httpContext.GetClientIpAddress());

                switch (cancelResponse.Status)
                {
                    case OperationCancellationRequestStatus.NotFound:
                        return Results.NotFound(new ErrorResponse("NotFound", cancelResponse.Message));

                    case OperationCancellationRequestStatus.AlreadyCompleted:
                    case OperationCancellationRequestStatus.CancellationNotSupported:
                        return Results.Conflict(new ErrorResponse("Conflict", cancelResponse.Message)); // 409 Conflict

                    case OperationCancellationRequestStatus.CancellationPending:
                    default: // Assuming CancellationPending is the primary "successful request" outcome
                        return Results.Ok(cancelResponse); // 200 OK with details
                }
            })
            .WithSummary("Cancel Ongoing Operation").Produces<OperationCancelResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest) // Though not explicitly returned, good to keep for general errors
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status403Forbidden); // Added Forbid for completeness

            // Defines POST /api/v1/operations/packages/optional/install
            // Initiates installation of an optional package.
            // Requires BasicAdmin role. Calls IMasterActionCoordinatorService and IAuditLogService.
            // Request body: PackageNameRequest. Response: 202 Accepted with OperationInitiationResponse.
            operationsGroup.MapPost("/packages/optional/install",
            /// <summary>
            /// Initiates an operation to install an optional package specified by its name.
            /// Requires BasicAdmin or higher privileges.
            /// </summary>
            /// <param name="request">The <see cref="PackageNameRequest"/> DTO containing the name of the package to install.</param>
            /// <param name="masterActionService">The <see cref="IMasterActionCoordinatorService"/> to initiate the package installation operation.</param>
            /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
            /// <param name="auditLog">The <see cref="IAuditLogService"/> for recording the action.</param>
            /// <param name="httpContext">The <see cref="HttpContext"/> for accessing request details.</param>
            /// <returns>
            /// An <see cref="IResult"/> that is <see cref="Results.Accepted(string, object)"/> with an <see cref="OperationInitiationResponse"/> on successful initiation.
            /// Returns <see cref="Results.BadRequest(object)"/> with an <see cref="ErrorResponse"/> if initiation fails.
            /// Returns <see cref="Results.Forbid()"/> if unauthorized.
            /// </returns>
            async (PackageNameRequest request, [FromServices] IMasterActionCoordinatorService masterActionService, ClaimsPrincipal user, [FromServices] IAuditLogService auditLog, HttpContext httpContext) =>
            {
                if (!user.IsBasicAdminOrHigher()) return Results.Forbid(); // Authorization check
                var username = user.GetUsername() ?? "unknown_admin";

                var initiateRequest = new OperationInitiateRequest
                {
                    OperationType = OperationType.PackageOptionalInstall,
                    Parameters = request.ToDictionary(), // Assumes DtoExtensions.ToDictionary() exists for PackageNameRequest
                    Description = $"Install optional package: {request.PackageName}"
                };

                try
                {
                    var masterAction = await masterActionService.InitiateMasterActionAsync(initiateRequest, user);
                    var auditParameters = new Dictionary<string, object> { { "packageName", request.PackageName }, { "action", "Install" }, { "operationId", masterAction.Id } };
                    await auditLog.LogActionAsync(
                        username: username,
                        action: "RequestInstallOptionalPackage",
                        targetResource: $"Package:{request.PackageName}",
                        parameters: auditParameters,
                        outcome: AuditLogOutcome.Success.ToString(), // Audit success of initiation
                        details: $"Optional package install for '{request.PackageName}' initiated. MasterActionId: {masterAction.Id}",
                        clientIpAddress: httpContext.GetClientIpAddress());

                    var response = new OperationInitiationResponse { OperationId = masterAction.Id, Message = "Optional package install initiated." };
                    return Results.Accepted($"/api/v1/operations/{masterAction.Id}", response);
                }
                catch (Exception ex)
                {
                    var auditParameters = new Dictionary<string, object> { { "packageName", request.PackageName }, { "action", "Install" } };
                    await auditLog.LogActionAsync(
                        username: username,
                        action: "RequestInstallOptionalPackage",
                        targetResource: $"Package:{request.PackageName}",
                        parameters: auditParameters,
                        outcome: AuditLogOutcome.Failure.ToString(),
                        details: $"Failed to initiate install for '{request.PackageName}'. Reason: {ex.Message}",
                        clientIpAddress: httpContext.GetClientIpAddress());
                    return Results.BadRequest(new ErrorResponse(error: "PACKAGE_INSTALL_FAILED", message: ex.Message));
                }
            }).WithSummary("Install Optional Package")
              .Produces<OperationInitiationResponse>(StatusCodes.Status202Accepted)
              .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
              .Produces(StatusCodes.Status403Forbidden); // Added Forbid

            // Defines POST /api/v1/operations/packages/optional/uninstall
            // Initiates uninstallation of an optional package.
            // Requires BasicAdmin role. Calls IMasterActionCoordinatorService and IAuditLogService.
            // Request body: PackageNameRequest. Response: 202 Accepted with OperationInitiationResponse.
            operationsGroup.MapPost("/packages/optional/uninstall",
            /// <summary>
            /// Initiates an operation to uninstall an optional package specified by its name.
            /// Requires BasicAdmin or higher privileges.
            /// </summary>
            /// <param name="request">The <see cref="PackageNameRequest"/> DTO containing the name of the package to uninstall.</param>
            /// <param name="masterActionService">The <see cref="IMasterActionCoordinatorService"/> to initiate the package uninstallation operation.</param>
            /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
            /// <param name="auditLog">The <see cref="IAuditLogService"/> for recording the action.</param>
            /// <param name="httpContext">The <see cref="HttpContext"/> for accessing request details.</param>
            /// <returns>
            /// An <see cref="IResult"/> that is <see cref="Results.Accepted(string, object)"/> with an <see cref="OperationInitiationResponse"/> on successful initiation.
            /// Returns <see cref="Results.BadRequest(object)"/> with an <see cref="ErrorResponse"/> if initiation fails.
            /// Returns <see cref="Results.Forbid()"/> if unauthorized.
            /// </returns>
            async (PackageNameRequest request, [FromServices] IMasterActionCoordinatorService masterActionService, ClaimsPrincipal user, [FromServices] IAuditLogService auditLog, HttpContext httpContext) =>
            {
                if (!user.IsBasicAdminOrHigher()) return Results.Forbid(); // Authorization check
                var username = user.GetUsername() ?? "unknown_admin";

                var initiateRequest = new OperationInitiateRequest
                {
                    OperationType = OperationType.PackageOptionalUninstall,
                    Parameters = request.ToDictionary(), // Assumes DtoExtensions.ToDictionary() exists
                    Description = $"Uninstall optional package: {request.PackageName}"
                };

                try
                {
                    var masterAction = await masterActionService.InitiateMasterActionAsync(initiateRequest, user);
                    var auditParameters = new Dictionary<string, object> { { "packageName", request.PackageName }, { "action", "Uninstall" }, { "operationId", masterAction.Id } };
                    await auditLog.LogActionAsync(
                        username: username,
                        action: "RequestUninstallOptionalPackage",
                        targetResource: $"Package:{request.PackageName}",
                        parameters: auditParameters,
                        outcome: AuditLogOutcome.Success.ToString(), // Audit success of initiation
                        details: $"Optional package uninstall for '{request.PackageName}' initiated. MasterActionId: {masterAction.Id}",
                        clientIpAddress: httpContext.GetClientIpAddress());

                    var response = new OperationInitiationResponse { OperationId = masterAction.Id, Message = "Optional package uninstall initiated." };
                    return Results.Accepted($"/api/v1/operations/{masterAction.Id}", response);
                }
                catch (Exception ex)
                {
                    var auditParameters = new Dictionary<string, object> { { "packageName", request.PackageName }, { "action", "Uninstall" } };
                    await auditLog.LogActionAsync(
                        username: username,
                        action: "RequestUninstallOptionalPackage",
                        targetResource: $"Package:{request.PackageName}",
                        parameters: auditParameters,
                        outcome: AuditLogOutcome.Failure.ToString(),
                        details: $"Failed to initiate uninstall for '{request.PackageName}'. Reason: {ex.Message}",
                        clientIpAddress: httpContext.GetClientIpAddress());
                    return Results.BadRequest(new ErrorResponse(error: "PACKAGE_UNINSTALL_FAILED", message: ex.Message));
                }
            }).WithSummary("Uninstall Optional Package")
              .Produces<OperationInitiationResponse>(StatusCodes.Status202Accepted)
              .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
              .Produces(StatusCodes.Status403Forbidden); // Added Forbid

            // POST /api/v1/operations/diagnostics/standard
            // Initiates a standard diagnostic run or selected health checks.
            // Requires Operator role. Calls IMasterActionCoordinatorService and IAuditLogService.
            // Request body: RunHealthChecksRequest. Response: 202 Accepted with OperationInitiationResponse.
            operationsGroup.MapPost("/diagnostics/standard",
            /// <summary>
            /// Initiates a standard diagnostic run or executes selected health checks as specified in the <see cref="RunHealthChecksRequest"/>.
            /// Requires Operator or higher privileges.
            /// </summary>
            /// <param name="request">The <see cref="RunHealthChecksRequest"/> DTO detailing which checks to run and on which nodes.</param>
            /// <param name="masterActionService">The <see cref="IMasterActionCoordinatorService"/> to initiate the diagnostic operation.</param>
            /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
            /// <param name="auditLog">The <see cref="IAuditLogService"/> for recording the action.</param>
            /// <param name="httpContext">The <see cref="HttpContext"/> for accessing request details.</param>
            /// <returns>
            /// An <see cref="IResult"/> that is <see cref="Results.Accepted(string, object)"/> with an <see cref="OperationInitiationResponse"/> on successful initiation.
            /// Returns <see cref="Results.BadRequest(object)"/> with an <see cref="ErrorResponse"/> if initiation fails.
            /// Returns <see cref="Results.Forbid()"/> if unauthorized.
            /// </returns>
            async (RunHealthChecksRequest request, [FromServices] IMasterActionCoordinatorService masterActionService, ClaimsPrincipal user, [FromServices] IAuditLogService auditLog, HttpContext httpContext) =>
            {
                if (!user.IsOperatorOrHigher()) return Results.Forbid(); // Authorization check

                var username = user.GetUsername() ?? "unknown_operator";
                var description = "Standard Diagnostics Run";
                if (request.CheckIds?.Any() == true)
                    description = $"Run specific health checks: {string.Join(", ", request.CheckIds)}";
                if (request.NodeNames?.Any() == true)
                    description += $" on nodes: {string.Join(", ", request.NodeNames)}";
                else if (request.AllNodes == true)
                    description += " on all nodes";

                description += $" (Initiated by {username})";


                try
                {
                    var initiateRequest = new OperationInitiateRequest
                    {
                        OperationType = OperationType.RunStandardDiagnostics,
                        Parameters = request.ToDictionary(), // Assumes DtoExtensions.ToDictionary() exists
                        Description = description
                    };
                    var masterAction = await masterActionService.InitiateMasterActionAsync(initiateRequest, user);

                    // Parameters for audit log should be prepared before potential modifications by ToDictionary or if request is complex
                    var auditParameters = new Dictionary<string, object>
                    {
                        { "checkIds", request.CheckIds ?? new List<string>() },
                        { "nodeNames", request.NodeNames ?? new List<string>() },
                        { "allNodes", request.AllNodes ?? false },
                        { "operationId", masterAction.Id }
                    };

                    await auditLog.LogActionAsync(
                        username: username,
                        action: "RunStandardDiagnostics",
                        targetResource: "SystemDiagnostics", // General target
                        parameters: auditParameters,
                        outcome: AuditLogOutcome.Success.ToString(), // Audit success of initiation
                        details: $"Diagnostic run initiated successfully. MasterActionId: {masterAction.Id}. Description: {description}",
                        clientIpAddress: httpContext.GetClientIpAddress());

                    var response = new OperationInitiationResponse
                    {
                        OperationId = masterAction.Id,
                        Message = "Standard diagnostics operation initiated." // Clarified message
                    };

                    return Results.Accepted($"/api/v1/operations/{masterAction.Id}", response);
                }
                catch (Exception ex)
                {
                    var auditParameters = new Dictionary<string, object>
                    {
                        { "checkIds", request.CheckIds ?? new List<string>() },
                        { "nodeNames", request.NodeNames ?? new List<string>() },
                        { "allNodes", request.AllNodes ?? false }
                    };
                    await auditLog.LogActionAsync(
                       username: username,
                       action: "RunStandardDiagnostics",
                       targetResource: "SystemDiagnostics",
                       parameters: auditParameters,
                       outcome: AuditLogOutcome.Failure.ToString(),
                       details: $"Failed to initiate diagnostic run. Error: {ex.Message}. Description: {description}",
                       clientIpAddress: httpContext.GetClientIpAddress());
                    return Results.BadRequest(new ErrorResponse(error: "DIAGNOSTICS_INIT_FAILED", message: ex.Message));
                }
            }).WithSummary("Run Standard Diagnostics or Selected Health Checks").Produces<OperationInitiationResponse>(StatusCodes.Status202Accepted).Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

            return app; // Corrected from builder to app to match parameter name
        }

        /// <summary>
        /// A generic helper method to map standardized POST endpoints for initiating various types of operations.
        /// </summary>
        /// <typeparam name="TRequest">The DTO type for the request body. Use <see cref="EmptyRequest"/> for operations that do not require a request body.</typeparam>
        /// <param name="group">The <see cref="RouteGroupBuilder"/> to which this endpoint will be added.</param>
        /// <param name="pattern">The URL route pattern for this specific operation endpoint (e.g., "/env-backup").</param>
        /// <param name="operationType">The <see cref="OperationType"/> enum value that this endpoint will initiate.</param>
        /// <param name="requiredRole">A string representing the minimum <see cref="SiteKeeperRoles"/> required to access this endpoint (e.g., <see cref="SiteKeeperRoles.Operator"/>, <see cref="SiteKeeperRoles.BasicAdmin"/>).</param>
        /// <param name="summary">A brief summary for Swagger/OpenAPI documentation, describing the endpoint's purpose.</param>
        /// <param name="description">A more detailed description for Swagger/OpenAPI documentation, including role requirements and auditing notes.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> (specifically, the <paramref name="group"/>) with the new endpoint mapped, allowing for further chaining if needed.</returns>
        /// <remarks>
        /// This helper method standardizes the creation of operation-initiating POST endpoints. It handles:
        /// <list type="bullet">
        ///   <item><description>Role-based authorization using the <see cref="CheckUserRole"/> helper.</description></item>
        ///   <item><description>Logging the initiation request.</description></item>
        ///   <item><description>Constructing an <see cref="OperationInitiateRequest"/>, converting the typed <paramref name="request"/> DTO into a parameter dictionary.</description></item>
        ///   <item><description>Calling <see cref="IMasterActionCoordinatorService.InitiateMasterActionAsync"/> to start the operation.</description></item>
        ///   <item><description>Performing audit logging of the initiation attempt (success or failure) via <see cref="IAuditLogService"/>.</description></item>
        ///   <item><description>Returning a 202 Accepted response with an <see cref="OperationInitiationResponse"/> containing the MasterActionId and a confirmation message.</description></item>
        ///   <item><description>Handling exceptions during initiation and returning a 400 Bad Request with an <see cref="ErrorResponse"/>.</description></item>
        /// </list>
        /// The `Produces` and `WithTags` attributes for Swagger are also applied consistently.
        /// </remarks>
        public static IEndpointRouteBuilder MapPostOperation<TRequest>(
            this IEndpointRouteBuilder group, // Should be RouteGroupBuilder, but IEndpointRouteBuilder works for MapPost extension
            string pattern,
            OperationType operationType,
            string requiredRole,
            string summary,
            string description) where TRequest : class
        {
            group.MapPost(pattern, async (
                [FromBody] TRequest request,
                ClaimsPrincipal user,
                [FromServices] IMasterActionCoordinatorService masterActionService,
                [FromServices] IAuditLogService auditLog,
                [FromServices] ILoggerFactory loggerFactory,
                HttpContext httpContext) =>
            {
                var logger = loggerFactory.CreateLogger("SiteKeeper.Api.OperationEndpoints");
                if (!CheckUserRole(user, requiredRole))
                {
                    logger.LogWarning("Forbidden access to endpoint {Pattern} by user {User}. Required role: {Role}", pattern, user.GetUsername(), requiredRole);
                    return Results.Forbid();
                }

                var username = user.GetUsername();
                logger.LogInformation("API: User '{Username}' initiating operation '{OperationType}' via endpoint '{Pattern}'.", username, operationType, pattern);

                try
                {
                    var initiateRequest = new OperationInitiateRequest
                    {
                        OperationType = operationType,
                        Parameters = request.ToDictionary()
                    };

                    var masterAction = await masterActionService.InitiateMasterActionAsync(initiateRequest, user);

                    var auditParameters = new Dictionary<string, object> { { "operationId", masterAction.Id } };
                    if (request is not EmptyRequest)
                    {
                        foreach (var param in initiateRequest.Parameters)
                        {
                            auditParameters[param.Key] = param.Value;
                        }
                    }

                    await auditLog.LogActionAsync(
                        username: username,
                        action: $"Initiate{operationType}",
                        targetResource: operationType.ToString(),
                        parameters: auditParameters,
                        outcome: AuditLogOutcome.Success.ToString(),
                        details: $"{operationType} operation '{masterAction.Id}' initiated by user '{username}'.",
                        clientIpAddress: httpContext.GetClientIpAddress()
                    );

                    var location = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/api/v1/operations/{masterAction.Id}";
                    var response = new OperationInitiationResponse { OperationId = masterAction.Id, Message = $"{summary} initiated." };
                    return Results.Accepted(location, response);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to initiate operation {OperationType} for user '{Username}'.", operationType, username);
                    await auditLog.LogActionAsync(username, $"Initiate{operationType}", operationType.ToString(), request.ToDictionary(), AuditLogOutcome.Failure.ToString(), details: ex.Message);
                    return Results.BadRequest(new ErrorResponse(error: $"{operationType.ToString().ToUpper()}_FAILED", message: ex.Message));
                }
            })
            .WithSummary(summary)
            .WithDescription($"{description}. Requires {requiredRole} role or higher. *Audit Logged*.")
            .Produces<OperationInitiationResponse>(StatusCodes.Status202Accepted)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

            return group;
        }

        private static bool CheckUserRole(ClaimsPrincipal user, string role)
        {
            return role switch
            {
                SiteKeeperRoles.Observer => user.IsObserverOrHigher(),
                SiteKeeperRoles.Operator => user.IsOperatorOrHigher(),
                SiteKeeperRoles.BasicAdmin => user.IsBasicAdminOrHigher(),
                SiteKeeperRoles.AdvancedAdmin => user.IsAdvancedAdmin(),
                _ => false,
            };
        }
    }
} 