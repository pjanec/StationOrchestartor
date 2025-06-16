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
    /// Provides API endpoints for managing and monitoring the environment. This is a partial class that extends the main ApiEndpoints.
    /// The methods within this class are responsible for mapping all environment-related endpoints.
    /// </summary>
    public static partial class ApiEndpoints
    {
        /// <summary>
        /// Maps all environment-related endpoints, such as status, node lists, and recent changes.
        /// These endpoints are grouped under the "/api/v1/environment" route and tagged for OpenAPI documentation.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to map endpoints to.</param>
        /// <param name="guiHostConstraint">The host string to constrain the endpoints to, ensuring they are only reachable from the intended UI host.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with mapped endpoints.</returns>
        public static IEndpointRouteBuilder MapEnvironmentApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            var envGroup = app.MapGroup("/api/v1/environment")
                .WithTags("Environment")
                .RequireAuthorization()
                .RequireHost(guiHostConstraint);

            envGroup.MapGet("/status", async ([FromServices] IEnvironmentService envService, ClaimsPrincipal user) =>
            {
                if (!user.IsObserverOrHigher()) return Results.Forbid();
                var status = await envService.GetEnvironmentStatusAsync();
                return Results.Ok(status);
            }).WithSummary("Get Core Environment Status").Produces<EnvironmentStatusResponse>();

            envGroup.MapGet("/nodes", async ([FromServices] IEnvironmentService envService, ClaimsPrincipal user, [FromQuery] string? filterText, [FromQuery] string? sortBy, [FromQuery] string? sortOrder) =>
            {
                if (!user.IsObserverOrHigher()) return Results.Forbid();
                var nodes = await envService.ListEnvironmentNodesAsync(filterText, sortBy, sortOrder);
                return Results.Ok(nodes);
            }).WithSummary("List All Nodes in Environment").Produces<List<NodeSummary>>();

            envGroup.MapGet("/manifest", async ([FromServices] IEnvironmentService envService, ClaimsPrincipal user) =>
            {
                if (!user.IsObserverOrHigher()) return Results.Forbid();
                var manifest = await envService.GetEnvironmentManifestAsync();
                return Results.Ok(manifest);
            }).WithSummary("Get Pure Environment Manifest").Produces<PureManifest>();

            // GET /environment/recent-changes
            envGroup.MapGet("/recent-changes", 
                async (
                    [FromServices] IEnvironmentService environmentService, 
                    [FromServices] ILoggerFactory loggerFactory,
                    [FromQuery] int? limitQuery) =>
                {
                    var logger = loggerFactory.CreateLogger("SiteKeeper.Master.Web.Apis.Environment");
                    logger.LogInformation("API: Request received for recent environment changes. Limit query: {LimitQuery}", limitQuery);

                    // Validate and apply default for limit, conforming to swagger: default 5, min 1, max 20
                    int limit = limitQuery.HasValue ? limitQuery.Value : 5;
                    if (limit < 1) limit = 1;
                    if (limit > 20) limit = 20;

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