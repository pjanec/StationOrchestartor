using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.DTOs.API.Operations;
using SiteKeeper.Shared.DTOs.API.SoftwareControl;
using SiteKeeper.Shared.DTOs.Common;
using SiteKeeper.Shared.Enums;
using SiteKeeper.Shared.Security;
using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace SiteKeeper.Master.Web.Apis
{
    /// <summary>
    /// Defines API endpoints related to Application and Application Plan control and status monitoring.
    /// This is a partial class, and these endpoints are registered via the main <see cref="ApiEndpoints.MapAll(WebApplication)"/> method.
    /// </summary>
    /// <remarks>
    /// This class utilizes Minimal API features such as <see cref="RouteGroupBuilder.MapGroup(string)"/> to structure endpoints
    /// under base paths like <c>/api/v1/apps</c> and <c>/api/v1/plans</c>.
    /// Authorization policies (e.g., requiring Operator or BasicAdmin roles) are applied at group or endpoint level.
    /// Endpoints typically interact with services like <see cref="IAppControlService"/>, <see cref="IPlanControlService"/>,
    /// and <see cref="IMasterActionCoordinatorService"/> to fulfill client requests.
    /// </remarks>
    public static partial class ApiEndpoints
    {
        /// <summary>
        /// Maps all API endpoints related to applications and application plans.
        /// This includes listing applications/plans and initiating actions (start, stop, restart) on them.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to which the endpoints will be mapped (typically the <see cref="WebApplication"/> instance).</param>
        /// <param name="guiHostConstraint">A hostname constraint string to apply to these endpoints, ensuring they are only accessible via specific hostnames configured for GUI/API access.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with the newly mapped application and plan endpoints.</returns>
        /// <remarks>
        /// This method creates two main route groups:
        /// <list type="bullet">
        ///   <item><description><c>/api/v1/apps</c>: For listing individual applications and performing actions on them. Requires at least Operator privileges, with listing potentially requiring BasicAdmin.</description></item>
        ///   <item><description><c>/api/v1/plans</c>: For listing application plans and performing actions on them. Requires at least Operator privileges, with listing potentially requiring BasicAdmin.</description></item>
        /// </list>
        /// It calls private helper methods <see cref="MapAppActionEndpoints"/> and <see cref="MapPlanActionEndpoints"/> to define specific action routes.
        /// </remarks>
        public static IEndpointRouteBuilder MapAppsAndPlansApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            // =====================================================================================
            // Apps API Endpoints (Group: /api/v1/apps) - Formerly SoftwareControl
            // =====================================================================================
            var appsGroup = app.MapGroup("/api/v1/apps")
                .WithTags("AppsControl")
                .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsOperatorOrHigher()))
                .RequireHost(guiHostConstraint);

            // Defines GET /api/v1/apps
            // Retrieves a list of all manageable applications and their current statuses.
            // Requires BasicAdmin role. Calls IAppControlService.ListAppsAsync.
            // Supports filtering by filterText, and sorting by sortBy field and sortOrder.
            appsGroup.MapGet("/", async ([FromServices] IAppControlService appControlService, ClaimsPrincipal user, [FromQuery] string? filterText, [FromQuery] string? sortBy, [FromQuery] string? sortOrder) =>
            {
                // Authorization: Requires BasicAdmin or higher.
                if (!user.IsBasicAdminOrHigher()) return Results.Forbid();
                var apps = await appControlService.ListAppsAsync(filterText, sortBy, sortOrder);
                return Results.Ok(apps);
            }).WithSummary("List Individual Apps").Produces<List<AppStatusInfo>>();

            // Refactored App Action Endpoints
            MapAppActionEndpoints(appsGroup);

            // =====================================================================================
            // Plans API Endpoints (Group: /api/v1/plans) - Formerly SoftwareControl
            // =====================================================================================
            var plansGroup = app.MapGroup("/api/v1/plans")
                .WithTags("PlansControl")
                // Group-level authorization: Requires at least Operator role for any plan action.
                // Specific endpoints may add further role checks (e.g., ListPlans requires BasicAdmin).
                .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsOperatorOrHigher()))
                .RequireHost(guiHostConstraint);

            // Defines GET /api/v1/plans
            // Retrieves a list of all defined application plans and their current aggregated statuses.
            // Requires BasicAdmin role. Calls IPlanControlService.ListPlansAsync.
            // Supports filtering by filterText, and sorting by sortBy field and sortOrder.
            plansGroup.MapGet("/", async ([FromServices] IPlanControlService planControlService, ClaimsPrincipal user, [FromQuery] string? filterText, [FromQuery] string? sortBy, [FromQuery] string? sortOrder) =>
            {
                // Endpoint-specific authorization: Listing plans requires BasicAdmin.
                if (!user.IsBasicAdminOrHigher()) return Results.Forbid();
                var plans = await planControlService.ListPlansAsync(filterText, sortBy, sortOrder);
                return Results.Ok(plans);
            }).WithSummary("List All Plans")
              .Produces<PlanListResponse>()
              .Produces(StatusCodes.Status403Forbidden);

            // Refactored Plan Action Endpoints
            MapPlanActionEndpoints(plansGroup);

            return app;
        }

        /// <summary>
        /// Maps common action endpoints (start, stop, restart) for individual applications.
        /// These endpoints are POST requests to routes like /api/v1/apps/{appId}/start.
        /// </summary>
        /// <param name="appsGroup">The <see cref="IEndpointRouteBuilder"/> for the "/api/v1/apps" group.</param>
        /// <remarks>
        /// Each action initiates a corresponding <see cref="MasterAction"/> via the <see cref="IMasterActionCoordinatorService"/>.
        /// The action type (e.g., <see cref="OperationType.AppStart"/>) and the target application ID are passed as parameters
        /// to the master action. Audit logs are created for each initiated action.
        /// These endpoints require at least Operator privileges.
        /// </remarks>
        private static void MapAppActionEndpoints(IEndpointRouteBuilder appsGroup)
        {
            var appActions = new Dictionary<string, (OperationType opType, string summary, string description)>
            {
                { "start", (OperationType.AppStart, "Start App", "Initiates an operation to start a specific application on its target node(s).") },
                { "stop", (OperationType.AppStop, "Stop App", "Initiates an operation to stop a specific application on its target node(s).") },
                { "restart", (OperationType.AppRestart, "Restart App", "Initiates an operation to restart a specific application on its target node(s).") }
            };

            foreach (var action in appActions)
            {
                // Defines POST /api/v1/apps/{appId}/start, POST /api/v1/apps/{appId}/stop, etc.
                // Initiates a MasterAction (e.g., AppStart, AppStop) for the specified appId.
                // Requires Operator role. Calls IMasterActionCoordinatorService.InitiateMasterActionAsync and IAuditLogService.LogActionAsync.
                // Returns 202 Accepted with OperationInitiationResponse.
                appsGroup.MapPost("/{appId}/" + action.Key, async (
                    string appId,
                    [FromServices] IMasterActionCoordinatorService masterActionService,
                    ClaimsPrincipal user,
                    [FromServices] IAuditLogService auditLog,
                    HttpContext httpContext) =>
                {
                    // Authorization: Requires Operator or higher for app actions.
                    if (!user.IsOperatorOrHigher()) return Results.Forbid();

                    var username = user.GetUsername(); // For auditing
                    var opType = action.Value.opType;
                    var parameters = new Dictionary<string, object> { { "appId", appId } }; // Parameters for the MasterAction

                    var initiateRequest = new OperationInitiateRequest { OperationType = opType, Parameters = parameters, Description = $"{action.Value.summary}: {appId}" };
                    var masterAction = await masterActionService.InitiateMasterActionAsync(initiateRequest, user);

                    await auditLog.LogActionAsync(
                        username: username ?? "unknown",
                        action: $"App::{action.Key}", 
                        targetResource: $"App:{appId}", 
                        parameters: parameters, 
                        outcome: Shared.Enums.AuditLogOutcome.Success.ToString(), // Audit success of initiation, not operation outcome
                        wasAuthorized: true,
                        details: $"Application action '{action.Key}' for app ID '{appId}' initiated successfully. MasterActionId: {masterAction.Id}.",
                        clientIpAddress: httpContext.Connection.RemoteIpAddress?.ToString()
                    );
                    return Results.Accepted($"/api/v1/operations/{masterAction.Id}", new OperationInitiationResponse { OperationId = masterAction.Id, Message = "Application action initiated." });
                })
                .WithSummary(action.Value.summary)
                .WithDescription(action.Value.description)
                .Produces<OperationInitiationResponse>(StatusCodes.Status202Accepted)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status401Unauthorized)
                .WithTags("AppsControl");
            }
        }

        /// <summary>
        /// Maps common action endpoints (start, stop, restart) for application plans.
        /// These endpoints are POST requests to routes like /api/v1/plans/{planId}/start.
        /// </summary>
        /// <param name="plansGroup">The <see cref="IEndpointRouteBuilder"/> for the "/api/v1/plans" group.</param>
        /// <remarks>
        /// Each action initiates a corresponding <see cref="MasterAction"/> via the <see cref="IMasterActionCoordinatorService"/>.
        /// The action type (e.g., <see cref="OperationType.PlanStart"/>) and the target plan ID are passed as parameters
        /// to the master action. Audit logs are created for each initiated action.
        /// These endpoints require at least Operator privileges.
        /// </remarks>
        private static void MapPlanActionEndpoints(IEndpointRouteBuilder plansGroup)
        {
            var planActions = new Dictionary<string, (OperationType opType, string summary, string description)>
            {
                { "start", (OperationType.PlanStart, "Start Plan", "Initiates an operation to start all applications in a specific plan.") },
                { "stop", (OperationType.PlanStop, "Stop Plan", "Initiates an operation to stop all applications in a specific plan.") },
                { "restart", (OperationType.PlanRestart, "Restart Plan", "Initiates an operation to restart all applications in a specific plan.") }
            };

            foreach (var action in planActions)
            {
                // Defines POST /api/v1/plans/{planId}/start, POST /api/v1/plans/{planId}/stop, etc.
                // Initiates a MasterAction (e.g., PlanStart, PlanStop) for the specified planId.
                // Requires Operator role. Calls IMasterActionCoordinatorService.InitiateMasterActionAsync and IAuditLogService.LogActionAsync.
                // Returns 202 Accepted with OperationInitiationResponse.
                plansGroup.MapPost("/{planId}/" + action.Key, async (
                    string planId,
                    [FromServices] IMasterActionCoordinatorService masterActionService,
                    ClaimsPrincipal user,
                    [FromServices] IAuditLogService auditLog,
                    HttpContext httpContext) =>
                {
                    // Authorization: Requires Operator or higher for plan actions (inherited from group).
                    if (!user.IsOperatorOrHigher()) return Results.Forbid(); // Redundant if group has this, but good for clarity.

                    var username = user.GetUsername(); // For auditing
                    var opType = action.Value.opType;
                    var parameters = new Dictionary<string, object> { { "planId", planId } }; // Parameters for the MasterAction

                    var initiateRequest = new OperationInitiateRequest { OperationType = opType, Parameters = parameters, Description = $"{action.Value.summary}: {planId}" };
                    var masterAction = await masterActionService.InitiateMasterActionAsync(initiateRequest, user);

                    await auditLog.LogActionAsync(
                        username: username ?? "unknown",
                        action: $"Plan::{action.Key}",
                        targetResource: $"Plan:{planId}",
                        parameters: parameters,
                        outcome: Shared.Enums.AuditLogOutcome.Success.ToString(), // Audit success of initiation
                        wasAuthorized: true,
                        details: $"Plan action '{action.Key}' for plan ID '{planId}' initiated successfully. MasterActionId: {masterAction.Id}.",
                        clientIpAddress: httpContext.Connection.RemoteIpAddress?.ToString()
                    );
                    return Results.Accepted($"/api/v1/operations/{masterAction.Id}", new OperationInitiationResponse { OperationId = masterAction.Id, Message = "Plan action initiated." });
                })
                .WithSummary(action.Value.summary)
                .WithDescription(action.Value.description)
                .Produces<OperationInitiationResponse>(StatusCodes.Status202Accepted)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status401Unauthorized)
                .WithTags("PlansControl");
            }
        }
    }
} 