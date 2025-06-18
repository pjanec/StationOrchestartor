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
    /// Defines API endpoints related to system diagnostics, including listing health checks,
    /// discoverable applications for diagnostics, and available data package types for applications.
    /// This is a partial class, and these endpoints are registered via the main <see cref="ApiEndpoints.MapAll(WebApplication)"/> method.
    /// </summary>
    /// <remarks>
    /// This class utilizes Minimal API features such as <see cref="RouteGroupBuilder.MapGroup(string)"/> to structure endpoints
    /// under the base path <c>/api/v1/diagnostics</c>.
    /// Authorization is applied at the group level and further refined at individual endpoints, typically requiring
    /// at least Operator privileges for accessing diagnostic information.
    /// Endpoints interact primarily with the <see cref="IDiagnosticsService"/> to retrieve diagnostic data.
    /// </remarks>
    public static partial class ApiEndpoints
    {
        /// <summary>
        /// Maps all API endpoints related to system diagnostics and health information.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to which the endpoints will be mapped (typically the <see cref="WebApplication"/> instance).</param>
        /// <param name="guiHostConstraint">A hostname constraint string to apply to these endpoints, ensuring they are only accessible via specific hostnames configured for GUI/API access.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with the newly mapped diagnostic endpoints.</returns>
        /// <remarks>
        /// This method creates a route group for <c>/api/v1/diagnostics</c> which requires general authorization.
        /// It defines endpoints for:
        /// <list type="bullet">
        ///   <item><description><c>GET /health-checks</c>: Lists available health checks. Requires Operator privileges.</description></item>
        ///   <item><description><c>GET /apps</c>: Lists applications discoverable for diagnostics. Requires Operator privileges.</description></item>
        ///   <item><description><c>GET /apps/{appId}/data-package-types</c>: Gets available data package types for a specific application. Requires Operator privileges.</description></item>
        /// </list>
        /// Note: Endpoints for running diagnostics and collecting logs, if part of this API surface, might be defined elsewhere or added here.
        /// </remarks>
        public static IEndpointRouteBuilder MapDiagnosticsApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            var diagnosticsGroup = app.MapGroup("/api/v1/diagnostics")
                .WithTags("Diagnostics")
                // General authorization for the group; specific endpoints might have more granular role/policy checks.
                .RequireAuthorization()
                .RequireHost(guiHostConstraint);

            // Defines GET /api/v1/diagnostics/health-checks
            // Retrieves a list of available health checks that can be performed.
            // Requires Operator role. Calls IDiagnosticsService.ListAvailableHealthChecksAsync.
            diagnosticsGroup.MapGet("/health-checks",
            /// <summary>
            /// Retrieves a list of all available health checks that can be executed in the system.
            /// Requires Operator or higher privileges.
            /// </summary>
            /// <param name="diagnosticsService">The <see cref="IDiagnosticsService"/> for fetching health check definitions.</param>
            /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user making the request.</param>
            /// <returns>
            /// An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with a <see cref="HealthCheckListResponse"/> on success,
            /// or <see cref="Results.Forbid()"/> if the user lacks sufficient permissions.
            /// </returns>
            async ([FromServices] IDiagnosticsService diagnosticsService, ClaimsPrincipal user) =>
            {
                if (!user.IsOperatorOrHigher()) return Results.Forbid(); // Authorization check
                var checks = await diagnosticsService.ListAvailableHealthChecksAsync();
                return Results.Ok(checks);
            }).WithSummary("List Available Health Checks").Produces<HealthCheckListResponse>();

            // GET /diagnostics/apps
            // Retrieves a list of applications discoverable for diagnostic purposes.
            diagnosticsGroup.MapGet("/apps",
                /// <summary>
                /// Retrieves a list of applications that are discoverable and can be targeted for diagnostic operations,
                /// such as log collection. Requires Operator or higher privileges.
                /// </summary>
                /// <param name="diagnosticsService">The <see cref="IDiagnosticsService"/> for fetching the list of diagnostic applications.</param>
                /// <param name="logger">A logger for this endpoint, typically for debugging or internal logging.</param>
                /// <returns>
                /// An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with an <see cref="AppListResponse"/> on success.
                /// Authorization is handled by the group policy.
                /// </returns>
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
                /// <summary>
                /// Retrieves a list of data package types (e.g., logs, configuration dumps) that can be collected
                /// for a specific application, identified by its <paramref name="appId"/>.
                /// Requires Operator or higher privileges.
                /// </summary>
                /// <param name="appId">The unique identifier of the application.</param>
                /// <param name="diagnosticsService">The <see cref="IDiagnosticsService"/> for fetching application-specific data package types.</param>
                /// <param name="logger">A logger for this endpoint.</param>
                /// <returns>
                /// An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with an <see cref="AppDataPackageTypesResponse"/>
                /// if the application is found and has defined package types. Returns <see cref="Results.NotFound(object?)"/>
                /// with an <see cref="ErrorResponse"/> if the application ID is not found.
                /// Authorization is handled by the group policy.
                /// </returns>
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