using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.DTOs.API.Environment;
using SiteKeeper.Shared.DTOs.API.Journal;
using SiteKeeper.Shared.DTOs.API.Nodes;
using SiteKeeper.Shared.Security;
using System.Collections.Generic;
using System.Security.Claims;

namespace SiteKeeper.Master.Web.Apis
{
    /// <summary>
    /// Defines API endpoints related to retrieving overall environment status, node information,
    /// the environment manifest, and recent operational changes (journal highlights).
    /// This is a partial class, and these endpoints are registered via the main <see cref="ApiEndpoints.MapAll(WebApplication)"/> method.
    /// </summary>
    /// <remarks>
    /// This class utilizes Minimal API features to structure endpoints under the base path <c>/api/v1/environment</c>.
    /// General authorization is required for this group, with specific endpoints typically requiring at least Observer privileges.
    /// Endpoints interact primarily with the <see cref="IEnvironmentService"/> to retrieve aggregated environment data.
    /// </remarks>
    public static partial class ApiEndpoints
    {
        /// <summary>
        /// Maps all API endpoints related to environment monitoring and information retrieval.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to which the endpoints will be mapped (typically the <see cref="WebApplication"/> instance).</param>
        /// <param name="guiHostConstraint">A hostname constraint string to apply to these endpoints, ensuring they are only accessible via specific hostnames configured for GUI/API access.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with the newly mapped environment endpoints.</returns>
        /// <remarks>
        /// This method creates a route group for <c>/api/v1/environment</c> which requires general authorization.
        /// It defines endpoints for:
        /// <list type="bullet">
        ///   <item><description><c>GET /status</c>: Retrieves the overall environment status. Requires Observer privileges.</description></item>
        ///   <item><description><c>GET /nodes</c>: Lists all nodes in the environment. Requires Observer privileges.</description></item>
        ///   <item><description><c>GET /manifest</c>: Gets the pure environment manifest. Requires Observer privileges.</description></item>
        ///   <item><description><c>GET /recent-changes</c>: Gets a summary of recent journal entries. Requires Observer privileges.</description></item>
        /// </list>
        /// </remarks>
        public static IEndpointRouteBuilder MapEnvironmentApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            var envGroup = app.MapGroup("/api/v1/environment")
                .WithTags("Environment")
                // General authorization for the group; specific endpoints also verify roles.
                .RequireAuthorization()
                .RequireHost(guiHostConstraint);

            // Defines GET /api/v1/environment/status
            // Retrieves a comprehensive status overview of the managed environment.
            // Requires Observer role. Calls IEnvironmentService.GetEnvironmentStatusAsync.
            envGroup.MapGet("/status",
            /// <summary>
            /// Retrieves the overall status of the managed environment, including summaries for software, apps, nodes, and operations.
            /// Requires Observer or higher privileges.
            /// </summary>
            /// <param name="envService">The <see cref="IEnvironmentService"/> for fetching overall environment status.</param>
            /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
            /// <returns>An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with an <see cref="EnvironmentStatusResponse"/>, or <see cref="Results.Forbid()"/>.</returns>
            async ([FromServices] IEnvironmentService envService, ClaimsPrincipal user) =>
            {
                if (!user.IsObserverOrHigher()) return Results.Forbid();
                var status = await envService.GetEnvironmentStatusAsync();
                return Results.Ok(status);
            }).WithSummary("Get Core Environment Status").Produces<EnvironmentStatusResponse>();

            // Defines GET /api/v1/environment/nodes
            // Retrieves a list of all nodes in the environment with their summary status.
            // Requires Observer role. Calls IEnvironmentService.ListEnvironmentNodesAsync.
            // Supports filtering and sorting.
            envGroup.MapGet("/nodes",
            /// <summary>
            /// Retrieves a list of all nodes within the managed environment, along with summary information for each.
            /// Supports optional filtering and sorting. Requires Observer or higher privileges.
            /// </summary>
            /// <param name="envService">The <see cref="IEnvironmentService"/> for fetching node summaries.</param>
            /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
            /// <param name="filterText">Optional text to filter nodes by (e.g., name, role).</param>
            /// <param name="sortBy">Optional field name to sort the results by (e.g., "nodeName", "agentStatus").</param>
            /// <param name="sortOrder">Optional sort order ("asc" or "desc").</param>
            /// <returns>An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with a list of <see cref="NodeSummary"/> DTOs, or <see cref="Results.Forbid()"/>.</returns>
            async ([FromServices] IEnvironmentService envService, ClaimsPrincipal user, [FromQuery] string? filterText, [FromQuery] string? sortBy, [FromQuery] string? sortOrder) =>
            {
                if (!user.IsObserverOrHigher()) return Results.Forbid();
                var nodes = await envService.ListEnvironmentNodesAsync(filterText, sortBy, sortOrder);
                return Results.Ok(nodes);
            }).WithSummary("List All Nodes in Environment").Produces<List<NodeSummary>>();

            // Defines GET /api/v1/environment/manifest
            // Retrieves the currently active "pure" environment manifest.
            // Requires Observer role. Calls IEnvironmentService.GetEnvironmentManifestAsync.
            envGroup.MapGet("/manifest",
            /// <summary>
            /// Retrieves the currently active "pure" environment manifest, which defines the desired state of the environment.
            /// Requires Observer or higher privileges.
            /// </summary>
            /// <param name="envService">The <see cref="IEnvironmentService"/> for fetching the environment manifest.</param>
            /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
            /// <returns>An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with a <see cref="PureManifest"/> DTO, or <see cref="Results.Forbid()"/>.</returns>
            async ([FromServices] IEnvironmentService envService, ClaimsPrincipal user) =>
            {
                if (!user.IsObserverOrHigher()) return Results.Forbid();
                var manifest = await envService.GetEnvironmentManifestAsync();
                return Results.Ok(manifest);
            }).WithSummary("Get Pure Environment Manifest").Produces<PureManifest>();

            // Defines GET /api/v1/environment/recent-changes
            // Retrieves a summary of recent significant system events or operations from the journal.
            // Requires Observer role. Calls IEnvironmentService.GetRecentChangesAsync.
            // Supports a 'limit' query parameter.
            envGroup.MapGet("/recent-changes", 
                /// <summary>
                /// Retrieves a summary of recent significant system events or operations, typically sourced from the Change Journal.
                /// Used for display on dashboards. Requires Observer or higher privileges.
                /// </summary>
                /// <param name="environmentService">The <see cref="IEnvironmentService"/> for fetching recent changes.</param>
                /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> for creating a logger instance for this endpoint.</param>
                /// <param name="limitQuery">Optional. The maximum number of recent changes to return. Defaults to 5, min 1, max 20.</param>
                /// <returns>An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with a list of <see cref="JournalEntrySummary"/> DTOs.</returns>
                async (
                    [FromServices] IEnvironmentService environmentService, 
                    [FromServices] ILoggerFactory loggerFactory,
                    [FromQuery] int? limitQuery) =>
                {
                    var logger = loggerFactory.CreateLogger("SiteKeeper.Master.Web.Apis.Environment"); // Create logger for this specific endpoint handler
                    logger.LogInformation("API: Request received for recent environment changes. Limit query: {LimitQuery}", limitQuery);

                    // Validate and apply default for limit, conforming to swagger: default 5, min 1, max 20
                    int limit = limitQuery.HasValue ? Math.Clamp(limitQuery.Value, 1, 20) : 5;

                    var recentChanges = await environmentService.GetRecentChangesAsync(limit);
                    return Results.Ok(recentChanges);
                })
                .WithName("GetRecentChanges")
                .WithSummary("Get Recent Changes (Journal Highlights)")
                .WithDescription("Retrieves a summary of recent journal entries for the dashboard. Requires Observer role.")
                .Produces<List<JournalEntrySummary>>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsObserverOrHigher()));

            return app;
        }
    }
} 