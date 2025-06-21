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
    /// Provides API endpoints for managing applications and plans. This is a partial class that extends the main ApiEndpoints.
    /// The methods within this class are responsible for mapping all app and plan-related endpoints.
    /// </summary>
    public static partial class ApiEndpoints
    {
        /// <summary>
        /// Maps all application and plan-related endpoints.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to map endpoints to.</param>
        /// <param name="guiHostConstraint">The host string to constrain the endpoints to.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with mapped endpoints.</returns>
        public static IEndpointRouteBuilder MapAppsAndPlansApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            // =====================================================================================
            // Apps API Endpoints (Group: /api/v1/apps) - Formerly SoftwareControl
            // =====================================================================================
            var appsGroup = app.MapGroup("/api/v1/apps")
                .WithTags("AppsControl")
                .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsOperatorOrHigher()))
                .RequireHost(guiHostConstraint);

            appsGroup.MapGet("/", async ([FromServices] IAppControlService appControlService, ClaimsPrincipal user, [FromQuery] string? filterText, [FromQuery] string? sortBy, [FromQuery] string? sortOrder) =>
            {
                // Swagger: BasicAdmin. Corrected.
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
                .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsOperatorOrHigher()))
                .RequireHost(guiHostConstraint);

            plansGroup.MapGet("/", async ([FromServices] IPlanControlService planControlService, ClaimsPrincipal user, [FromQuery] string? filterText, [FromQuery] string? sortBy, [FromQuery] string? sortOrder) =>
            {
                if (!user.IsBasicAdminOrHigher()) return Results.Forbid(); // Matches Swagger
                var plans = await planControlService.ListPlansAsync(filterText, sortBy, sortOrder);
                return Results.Ok(plans);
            }).WithSummary("List All Plans")
              .Produces<PlanListResponse>()
              .Produces(StatusCodes.Status403Forbidden);

            // Refactored Plan Action Endpoints
            MapPlanActionEndpoints(plansGroup);

            return app;
        }

        private static void MapAppActionEndpoints(IEndpointRouteBuilder appsGroup)
        {
            var appActions = new Dictionary<string, (OperationType opType, string summary, string description)>
            {
                { "start", (OperationType.AppStart, "Start App", "Starts a specific application.") },
                { "stop", (OperationType.AppStop, "Stop App", "Stops a specific application.") },
                { "restart", (OperationType.AppRestart, "Restart App", "Restarts a specific application.") }
            };

            foreach (var action in appActions)
            {
                appsGroup.MapPost("/{appId}/" + action.Key, async (string appId, [FromServices] IMasterActionCoordinator masterActionService, ClaimsPrincipal user, [FromServices] IAuditLogService auditLog, HttpContext httpContext) =>
                {
                    if (!user.IsOperatorOrHigher()) return Results.Forbid();
                    var username = user.GetUsername();
                    var opType = action.Value.opType;
                    var parameters = new Dictionary<string, object> { { "appId", appId } };
                    var initiateRequest = new OperationInitiateRequest { OperationType = opType, Parameters = parameters };
                    var masterAction = await masterActionService.InitiateMasterActionAsync(initiateRequest, user);
                    await auditLog.LogActionAsync(
                        username: username, 
                        action: $"App::{action.Key}", 
                        targetResource: $"App:{appId}", 
                        parameters: parameters, 
                        outcome: "Success",
                        wasAuthorized: true,
                        details: $"App action {action.Key} for {appId} initiated with opId {masterAction.Id}"
                    );
                    return Results.Accepted($"/api/v1/operations/{masterAction.Id}", new OperationInitiationResponse { OperationId = masterAction.Id, Message = "Application action initiated." });
                })
                .WithSummary(action.Value.summary)
                .WithDescription(action.Value.description)
                .WithTags("AppsControl");
            }
        }

        private static void MapPlanActionEndpoints(IEndpointRouteBuilder plansGroup)
        {
            var planActions = new Dictionary<string, (OperationType opType, string summary, string description)>
            {
                { "start", (OperationType.PlanStart, "Start Plan", "Starts an entire application plan.") },
                { "stop", (OperationType.PlanStop, "Stop Plan", "Stops an entire application plan.") },
                { "restart", (OperationType.PlanRestart, "Restart Plan", "Restarts an entire application plan.") }
            };

            foreach (var action in planActions)
            {
                plansGroup.MapPost("/{planId}/" + action.Key, async (string planId, [FromServices] IMasterActionCoordinator masterActionService, ClaimsPrincipal user, [FromServices] IAuditLogService auditLog, HttpContext httpContext) =>
                {
                    if (!user.IsOperatorOrHigher()) return Results.Forbid();
                    var username = user.GetUsername();
                    var opType = action.Value.opType;
                    var parameters = new Dictionary<string, object> { { "planId", planId } };
                    var initiateRequest = new OperationInitiateRequest { OperationType = opType, Parameters = parameters };
                    var masterAction = await masterActionService.InitiateMasterActionAsync(initiateRequest, user);
                    await auditLog.LogActionAsync(
                        username: username, 
                        action: $"Plan::{action.Key}", 
                        targetResource: $"Plan:{planId}", 
                        parameters: parameters, 
                        outcome: "Success",
                        wasAuthorized: true,
                        details: $"Plan action {action.Key} for {planId} initiated with opId {masterAction.Id}"
                    );
                    return Results.Accepted($"/api/v1/operations/{masterAction.Id}", new OperationInitiationResponse { OperationId = masterAction.Id, Message = "Plan action initiated." });
                })
                .WithSummary(action.Value.summary)
                .WithDescription(action.Value.description)
                .WithTags("PlansControl");
            }
        }
    }
} 