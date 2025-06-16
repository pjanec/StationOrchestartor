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
    /// Defines API endpoints related to operations management.
    /// </summary>
    /// <remarks>
    /// This class groups various operation-related endpoints, such as initiating environment updates,
    /// backups, restores, and other coordinated tasks.
    /// </remarks>
    public static partial class ApiEndpointsOperations
    {
        private const string ApiTag = "Operations";
        private const string DiagnosticsApiTag = "Diagnostics";

        /// <summary>
        /// Maps operation-related endpoints.
        /// </summary>
        /// <param name="builder">The endpoint route builder.</param>
        /// <returns>The endpoint route builder for chaining.</returns>
        public static IEndpointRouteBuilder MapOperationsApi(this IEndpointRouteBuilder builder, string guiHostConstraint)
        {
            var operationsGroup = builder.MapGroup("/api/v1/operations")
                .WithTags(ApiTag)
                .RequireAuthorization()
                .RequireHost(guiHostConstraint);

            //
            // Endpoints that FIT the standard operation pattern are refactored below
            //
            operationsGroup.MapPostOperation<EnvUpdateRequest>("/env-update-online", OperationType.EnvUpdateOnline, SiteKeeperRoles.BasicAdmin,
                "Initiate Online Environment Update", "Starts an online environment update to the specified target version.");

            operationsGroup.MapPostOperation<OfflineScanSourcesRequest>("/offline-update/scan-sources", OperationType.OfflineScanSources, SiteKeeperRoles.BasicAdmin,
                "Scan Sources for Offline Bundles", "Initiates a scan of selected offline sources to find offline update bundles.");

            operationsGroup.MapPostOperation<OfflineUpdateInitiateRequest>("/offline-update/initiate", OperationType.EnvUpdateOffline, SiteKeeperRoles.BasicAdmin,
                "Initiate Offline Environment Update", "Initiates an offline environment update using a selected bundle.");

            operationsGroup.MapPostOperation<EmptyRequest>("/env-backup", OperationType.EnvBackup, SiteKeeperRoles.BasicAdmin,
                "Initiate Environment Backup", "Starts an environment backup.");

            operationsGroup.MapPostOperation<EnvRestoreRequest>("/env-restore", OperationType.EnvRestore, SiteKeeperRoles.BasicAdmin,
                "Initiate Environment Restore", "Initiates an environment restore from a specific backup recorded in the journal.");

            operationsGroup.MapPostOperation<EnvRevertRequest>("/env-revert", OperationType.EnvRevert, SiteKeeperRoles.BasicAdmin,
                "Initiate Environment Revert", "Initiates an environment revert to a specific 'pure' state recorded in the journal.");

            operationsGroup.MapPostOperation<EmptyRequest>("/env-verify", OperationType.EnvVerify, SiteKeeperRoles.BasicAdmin,
                "Initiate Environment Verification", "Starts environment verification.");

            operationsGroup.MapPostOperation<EmptyRequest>("/env-sync", OperationType.EnvSync, SiteKeeperRoles.BasicAdmin,
                "Initiate Environment Sync", "Starts an environment synchronization process.");

            operationsGroup.MapPostOperation<PackageChangeVersionRequest>("/packages/change-version", OperationType.PackageChangeVersion, SiteKeeperRoles.AdvancedAdmin,
                "Change Package Version", "Changes package version on specified nodes.");

            operationsGroup.MapPostOperation<PackageNameRequest>("/packages/revert-deviations", OperationType.PackageRevertDeviations, SiteKeeperRoles.AdvancedAdmin,
                "Revert Package Deviations", "Reverts package deviations for a specific package to match the version and configuration defined in the current environment manifest.");

            operationsGroup.MapPostOperation<PackageRefreshRequest>("/packages/refresh", OperationType.PackageRefresh, SiteKeeperRoles.AdvancedAdmin,
                "Refresh Package(s)", "Refreshes selected package(s) or all refreshable packages.");

            operationsGroup.MapPostOperation<EmptyRequest>("/system-software/start", OperationType.SystemSoftwareStart, SiteKeeperRoles.Operator,
                "Start All System Software", "Starts all system software.");

            operationsGroup.MapPostOperation<EmptyRequest>("/system-software/stop", OperationType.SystemSoftwareStop, SiteKeeperRoles.Operator,
                "Stop All System Software", "Stops all system software.");

            operationsGroup.MapPostOperation<EmptyRequest>("/system-software/restart", OperationType.SystemSoftwareRestart, SiteKeeperRoles.Operator,
                "Restart All System Software", "Restarts all system software.");

            //
            // Endpoints with CUSTOM logic remain as they were
            //
            operationsGroup.MapPost("/test-op",
                async (
                    [FromBody] TestOpRequest request,
                    ClaimsPrincipal user,
                    [FromServices] IMasterActionCoordinatorService masterActionService,
                    [FromServices] IAuditLogService auditLog,
                    HttpContext httpContext) =>
                {
                    // This endpoint is for testing and requires the highest level of privilege.
                    if (!user.IsAdvancedAdmin())
                    {
                        return Results.Forbid();
                    }

                    var username = user.GetUsername() ?? "unknown";
                    var operationDescription = $"Orchestration Test (Master: {request.MasterFailure}, Slave: {request.SlaveBehavior})";

                    // Use the DtoExtensions.ToDictionary() helper to serialize the entire request object
                    // into the parameters dictionary. This makes all test settings available to the handler.
                    var parameters = request.ToDictionary();

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
                        targetResource: "Orchestration",
                        parameters: parameters, // Log all parameters for traceability
                        outcome: "Success",
                        details: $"Test operation '{masterAction.Id}' initiated.",
                        clientIpAddress: httpContext.GetClientIpAddress());

                    var location = $"/api/v1/operations/{masterAction.Id}";
                    var response = new OperationInitiationResponse { OperationId = masterAction.Id, Message = "Test operation initiated successfully." };

                    return Results.Accepted(location, response);
                })
                .WithName("InitiateTestOperation")
                .WithSummary("Initiate a Test Orchestration Operation")
                .WithDescription("A test-only endpoint to trigger an operation with simulated master and slave behavior (success, failure, timeout, cancellation). Requires Advanced Admin role. *Audit Logged*")
                .Produces<OperationInitiationResponse>(StatusCodes.Status202Accepted)
                .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status403Forbidden)
                .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsAdvancedAdmin()));


            operationsGroup.MapGet("/{operationId}",
                async (string operationId, [FromServices] IMasterActionCoordinatorService masterActionService, ClaimsPrincipal user) =>
                {
                    if (!user.IsObserverOrHigher())
                    {
                        return Results.Forbid();
                    }
                    var statusResponse = await masterActionService.GetStatusAsync(operationId);
                    return statusResponse is not null
                        ? Results.Ok(statusResponse)
                        : Results.NotFound(new ErrorResponse { Error = "NotFound", Message = $"Action with ID '{operationId}' not found." });
                })
                .WithName("GetOperationStatus")
                .WithSummary("Get Detailed Operation Status")
                .WithDescription("Retrieves the detailed status and results of a specific asynchronous operation by its ID. This endpoint can be polled to track the progress and final outcome of an operation. Requires Observer role or higher.")
                .Produces<OperationStatusResponse>(StatusCodes.Status200OK)
                .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status401Unauthorized)
                .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsObserverOrHigher()));

            operationsGroup.MapPost("/diagnostics/collect-logs",
                async (
                    [FromBody] CollectLogsRequest request,
                    [FromServices] IDiagnosticsService diagnosticsService,
                    ClaimsPrincipal user,
                    [FromServices] ILoggerFactory loggerFactory) =>
                {
                    var logger = loggerFactory.CreateLogger("SiteKeeper.Master.Web.Apis.Operations");
                    var username = user.GetUsername();
                    logger.LogInformation("API: Collect logs request received from user '{Username}' for AppId: {AppId}, PackageType: {PackageTypeId}.",
                        username, request.AppId, request.DataPackageTypeId);

                    if (string.IsNullOrWhiteSpace(request.AppId) || string.IsNullOrWhiteSpace(request.DataPackageTypeId))
                    {
                        return Results.BadRequest(new ErrorResponse
                        {
                            Error = "BadRequest",
                            Message = "AppId and DataPackageTypeId are required."
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

                    var response = await diagnosticsService.CollectAppLogsAsync(request, user);

                    if (string.IsNullOrEmpty(response.OperationId))
                    {
                         return Results.BadRequest(new ErrorResponse { Error = "OperationInitiationFailed", Message = response.Message });
                    }
                    return Results.Accepted($"/api/v1/operations/{response.OperationId}", response);
                })
                .WithName("CollectAppLogs")
                .WithTags(DiagnosticsApiTag)
                .WithSummary("Collect Logs/Dumps for an App")
                .WithDescription("Collects logs and memory dumps for a specific application. Requires Operator role or higher. *Audit Logged*")
                .Produces<OperationInitiationResponse>(StatusCodes.Status202Accepted)
                .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsOperatorOrHigher()));

            // Note: The /packages/optional/* endpoints have been removed as they are duplicates of the logic in the new helper.
            // They can be added back by calling the helper if desired:
            // operationsGroup.MapPostOperation<PackageNameRequest>("/packages/optional/install", OperationType.PackageOptionalInstall, SiteKeeperRoles.BasicAdmin, "Install Optional Package", "Initiates the installation of an optional package.");
            // operationsGroup.MapPostOperation<PackageNameRequest>("/packages/optional/uninstall", OperationType.PackageOptionalUninstall, SiteKeeperRoles.BasicAdmin, "Uninstall Optional Package", "Initiates the uninstallation of an optional package.");

            operationsGroup.MapPost("/{operationId}/cancel", async (string operationId, [FromServices] IMasterActionCoordinatorService masterActionService, ClaimsPrincipal user, [FromServices] IAuditLogService auditLog, HttpContext httpContext, [FromServices] ILogger<MasterConfig> logger) =>
            {
                // Swagger: Role appropriate to the original operation. For simplicity, OperatorOrHigher is used here.
                if (!user.IsOperatorOrHigher())
                {
                    logger.LogWarning("User {User} forbidden to cancel operation {OperationId}", user.GetUsername(), operationId);
                    return Results.Forbid();
                }
                var username = user.GetUsername();

                logger.LogInformation("User {User} requesting cancellation for operation {OperationId}", username, operationId);

                // The call is now directed to the new Master Action coordinator.
                // This service should return a response DTO to give the user immediate feedback.
                OperationCancelResponse cancelResponse = await masterActionService.RequestCancellationAsync( operationId, username);
    
                // The audit logging logic remains the same, as it's still best practice.
                bool isRequestSuccessful = cancelResponse.Status == OperationCancellationRequestStatus.CancellationPending;
                await auditLog.LogActionAsync(
                    username: username,
                    action: "RequestMasterActionCancellation",
                    targetResource: $"MasterAction:{operationId}",
                    parameters: null,
                    outcome: isRequestSuccessful ? AuditLogOutcome.Success.ToString() : AuditLogOutcome.Failure.ToString(),
                    details: $"User '{username}' requested cancellation. Service response: {cancelResponse.Message}",
                    clientIpAddress: httpContext.GetClientIpAddress());

                // The response handling also remains the same to ensure a consistent API for the UI.
                switch (cancelResponse.Status)
                {
                    case OperationCancellationRequestStatus.NotFound:
                        return Results.NotFound(new ErrorResponse("NotFound", cancelResponse.Message));

                    case OperationCancellationRequestStatus.AlreadyCompleted:
                    case OperationCancellationRequestStatus.CancellationNotSupported:
                        // 409 Conflict is an appropriate response for "can't do that now".
                        return Results.Conflict(new ErrorResponse("Conflict", cancelResponse.Message));

                    case OperationCancellationRequestStatus.CancellationPending:
                    default:
                        // 200 OK with the response body is the success case.
                        return Results.Ok(cancelResponse);
                }
            })
            .WithSummary("Cancel Ongoing Operation").Produces<OperationCancelResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

            operationsGroup.MapPost("/packages/optional/install", async (PackageNameRequest request, [FromServices] IMasterActionCoordinatorService masterActionService, ClaimsPrincipal user, [FromServices] IAuditLogService auditLog, HttpContext httpContext) =>
            {
                if (!user.IsBasicAdminOrHigher()) return Results.Forbid();
                var username = user.GetUsername();

                var initiateRequest = new OperationInitiateRequest
                {
                    OperationType = OperationType.PackageOptionalInstall,
                    Parameters = request.ToDictionary()
                };

                try
                {
                    var masterAction = await masterActionService.InitiateMasterActionAsync(initiateRequest, user);
                    var auditParameters = new Dictionary<string, object> { { "packageName", request.PackageName }, { "action", "Install" }, { "operationId", masterAction.Id } };
                    await auditLog.LogActionAsync(username, "RequestInstallOptionalPackage", $"Package:{request.PackageName}", auditParameters, AuditLogOutcome.Success.ToString(), details: $"Optional package install for '{request.PackageName}' initiated. ID: {masterAction.Id}", clientIpAddress: httpContext.GetClientIpAddress());

                    var response = new OperationInitiationResponse { OperationId = masterAction.Id, Message = "Optional package install initiated." };
                    return Results.Accepted($"/api/v1/operations/{masterAction.Id}", response);
                }
                catch (Exception ex)
                {
                    var auditParameters = new Dictionary<string, object> { { "packageName", request.PackageName }, { "action", "Install" } };
                    await auditLog.LogActionAsync(username, "RequestInstallOptionalPackage", $"Package:{request.PackageName}", auditParameters, AuditLogOutcome.Failure.ToString(), details: $"Failed to initiate install for '{request.PackageName}'. Reason: {ex.Message}", clientIpAddress: httpContext.GetClientIpAddress());
                    return Results.BadRequest(new ErrorResponse(error: "PACKAGE_INSTALL_FAILED", message: ex.Message));
                }
            }).WithSummary("Install Optional Package")
              .Produces<OperationInitiationResponse>(StatusCodes.Status202Accepted)
              .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

            operationsGroup.MapPost("/packages/optional/uninstall", async (PackageNameRequest request, [FromServices] IMasterActionCoordinatorService masterActionService, ClaimsPrincipal user, [FromServices] IAuditLogService auditLog, HttpContext httpContext) =>
            {
                if (!user.IsBasicAdminOrHigher()) return Results.Forbid();
                var username = user.GetUsername();

                var initiateRequest = new OperationInitiateRequest
                {
                    OperationType = OperationType.PackageOptionalUninstall,
                    Parameters = request.ToDictionary()
                };

                try
                {
                    var masterAction = await masterActionService.InitiateMasterActionAsync(initiateRequest, user);
                    var auditParameters = new Dictionary<string, object> { { "packageName", request.PackageName }, { "action", "Uninstall" }, { "operationId", masterAction.Id } };
                    await auditLog.LogActionAsync(username, "RequestUninstallOptionalPackage", $"Package:{request.PackageName}", auditParameters, AuditLogOutcome.Success.ToString(), details: $"Optional package uninstall for '{request.PackageName}' initiated. ID: {masterAction.Id}", clientIpAddress: httpContext.GetClientIpAddress());

                    var response = new OperationInitiationResponse { OperationId = masterAction.Id, Message = "Optional package uninstall initiated." };
                    return Results.Accepted($"/api/v1/operations/{masterAction.Id}", response);
                }
                catch (Exception ex)
                {
                    var auditParameters = new Dictionary<string, object> { { "packageName", request.PackageName }, { "action", "Uninstall" } };
                    await auditLog.LogActionAsync(username, "RequestUninstallOptionalPackage", $"Package:{request.PackageName}", auditParameters, AuditLogOutcome.Failure.ToString(), details: $"Failed to initiate uninstall for '{request.PackageName}'. Reason: {ex.Message}", clientIpAddress: httpContext.GetClientIpAddress());
                    return Results.BadRequest(new ErrorResponse(error: "PACKAGE_UNINSTALL_FAILED", message: ex.Message));
                }
            }).WithSummary("Uninstall Optional Package")
              .Produces<OperationInitiationResponse>(StatusCodes.Status202Accepted)
              .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

            // POST /operations/diagnostics/standard (formerly /diagnostics/run)
            operationsGroup.MapPost("/diagnostics/standard", async (RunHealthChecksRequest request, [FromServices] IMasterActionCoordinatorService masterActionService, ClaimsPrincipal user, [FromServices] IAuditLogService auditLog, HttpContext httpContext) =>
            {
                if (!user.IsOperatorOrHigher()) return Results.Forbid();

                var username = user.GetUsername();

                try
                {
                    var initiateRequest = new OperationInitiateRequest
                    {
                        OperationType = OperationType.RunStandardDiagnostics,
                        Parameters = request.ToDictionary()
                    };
                    var masterAction = await masterActionService.InitiateMasterActionAsync(initiateRequest, user);

                    var parameters = request.ToDictionary();
                    parameters.Add("operationId", masterAction.Id);


                    await auditLog.LogActionAsync(
                        username: username,
                        action: "RunStandardDiagnostics",
                        targetResource: $"Diagnostics:{string.Join(",", request.CheckIds ?? new List<string>())}",
                        parameters: parameters,
                        outcome: AuditLogOutcome.Success.ToString(),
                        details: $"Diagnostic run initiated successfully. Operation ID: {masterAction.Id}",
                        clientIpAddress: httpContext.GetClientIpAddress());

                    var response = new OperationInitiationResponse
                    {
                        OperationId = masterAction.Id,
                        Message = "Diagnostics collection initiated."
                    };

                    return Results.Accepted($"/api/v1/operations/{masterAction.Id}", response);
                }
                catch (Exception ex)
                {
                    var parameters = request.ToDictionary();
                    await auditLog.LogActionAsync(
                       username: username,
                       action: "RunStandardDiagnostics",
                       targetResource: $"Diagnostics:{string.Join(",", request.CheckIds ?? new List<string>())}",
                       parameters: parameters,
                       outcome: AuditLogOutcome.Failure.ToString(),
                       details: $"Failed to initiate diagnostic run. Error: {ex.Message}",
                       clientIpAddress: httpContext.GetClientIpAddress());
                    return Results.BadRequest(new ErrorResponse(error: "DIAGNOSTICS_INIT_FAILED", message: ex.Message));
                }
            }).WithSummary("Run Standard Diagnostics or Selected Health Checks").Produces<OperationInitiationResponse>(StatusCodes.Status202Accepted).Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

            return builder;
        }

        /// <summary>
        /// Maps a standardized POST endpoint for initiating a SiteKeeper operation.
        /// </summary>
        /// <typeparam name="TRequest">The DTO type for the request body. Use <see cref="EmptyRequest"/> for endpoints with no body.</typeparam>
        /// <param name="group">The endpoint route builder group.</param>
        /// <param name="pattern">The URL pattern for the endpoint.</param>
        /// <param name="operationType">The type of operation to initiate.</param>
        /// <param name="requiredRole">The role required to access this endpoint (from <see cref="SiteKeeperRoles"/>).</param>
        /// <param name="summary">A summary for Swagger documentation.</param>
        /// <param name="description">A description for Swagger documentation.</param>
        /// <returns>The configured endpoint route builder.</returns>
        public static IEndpointRouteBuilder MapPostOperation<TRequest>(
            this IEndpointRouteBuilder group,
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