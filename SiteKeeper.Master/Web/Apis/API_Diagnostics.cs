using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SiteKeeper.Master;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.DTOs.API.Diagnostics;
using SiteKeeper.Shared.DTOs.Common;
using SiteKeeper.Shared.Security;
using System.Security.Claims;

namespace SiteKeeper.Master.Web.Apis
{
    /// <summary>
    /// Provides API endpoints for diagnostics. This is a partial class that extends the main ApiEndpoints.
    /// The methods within this class are responsible for mapping all diagnostics-related endpoints.
    /// </summary>
    public static partial class ApiEndpoints
    {
        /// <summary>
        /// Maps all diagnostics-related endpoints.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to map endpoints to.</param>
        /// <param name="guiHostConstraint">The host string to constrain the endpoints to.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with mapped endpoints.</returns>
        public static IEndpointRouteBuilder MapDiagnosticsApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            var diagnosticsGroup = app.MapGroup("/api/v1/diagnostics")
                .WithTags("Diagnostics")
                .RequireAuthorization()
                .RequireHost(guiHostConstraint);

            diagnosticsGroup.MapGet("/health-checks", async ([FromServices] IDiagnosticsService diagnosticsService, ClaimsPrincipal user) =>
            {
                if (!user.IsOperatorOrHigher()) return Results.Forbid(); // Swagger: Operator. Corrected.
                var checks = await diagnosticsService.ListAvailableHealthChecksAsync();
                return Results.Ok(checks);
            }).WithSummary("List Available Health Checks").Produces<HealthCheckListResponse>();

            // GET /diagnostics/apps
            // Retrieves a list of applications discoverable for diagnostic purposes.
            diagnosticsGroup.MapGet("/apps",
                async ([FromServices] IDiagnosticsService diagnosticsService, [FromServices] ILogger<MasterConfig> logger) =>
                {
                    logger.LogInformation("API: Request received for listing diagnostic apps.");
                    var response = await diagnosticsService.ListDiagnosticAppsAsync();
                    return Results.Ok(response);
                })
                .WithName("ListDiagnosticApps")
                .WithSummary("List Discoverable Apps for Diagnostics")
                .WithDescription("Retrieves a list of applications that can be targeted for diagnostic operations (e.g., log collection). Requires Operator role.")
                .Produces<AppListResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsOperatorOrHigher()));


            // GET /diagnostics/apps/{appId}/data-package-types
            // Retrieves data package types for a specific application.
            diagnosticsGroup.MapGet("/apps/{appId}/data-package-types",
                async (string appId, [FromServices] IDiagnosticsService diagnosticsService, [FromServices] ILogger<MasterConfig> logger) =>
                {
                    logger.LogInformation("API: Request received for data package types for app ID '{AppId}'.", appId);
                    var response = await diagnosticsService.GetAppDataPackageTypesAsync(appId);
                    if (response == null)
                    {
                        logger.LogInformation("API: App with ID '{AppId}' not found for data package types.", appId);
                        return Results.NotFound(new ErrorResponse { Error = "NotFound", Message = $"Application with ID '{appId}' not found." });
                    }
                    return Results.Ok(response);
                })
                .WithName("GetAppDataPackageTypes")
                .WithSummary("Get Data Package Types for an App")
                .WithDescription("Retrieves a list of data package types (e.g., logs, dumps) that can be collected for a specific application. Requires Operator role.")
                .Produces<AppDataPackageTypesResponse>(StatusCodes.Status200OK)
                .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsOperatorOrHigher()));

            return app;
        }
    }
} 