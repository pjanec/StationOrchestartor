using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.DTOs.API.Releases;
using SiteKeeper.Shared.Security;
using System.Collections.Generic;
using System.Security.Claims;

namespace SiteKeeper.Master.Web.Apis
{
    /// <summary>
    /// Provides API endpoints for release management. This is a partial class that extends the main ApiEndpoints.
    /// The methods within this class are responsible for mapping all release-related endpoints.
    /// </summary>
    public static partial class ApiEndpoints
    {
        /// <summary>
        /// Maps all release-related endpoints.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to map endpoints to.</param>
        /// <param name="guiHostConstraint">The host string to constrain the endpoints to.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with mapped endpoints.</returns>
        public static IEndpointRouteBuilder MapReleasesApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            var releasesGroup = app.MapGroup("/api/v1/releases")
                .WithTags("Releases")
                .RequireAuthorization()
                .RequireHost(guiHostConstraint);

            releasesGroup.MapGet("/", async ([FromServices] IReleaseService releaseService, ClaimsPrincipal user) =>
            {
                if (!user.IsObserverOrHigher()) return Results.Forbid();
                var releases = await releaseService.ListReleasesAsync(null);
                return Results.Ok(releases);
            }).WithSummary("Get available releases for update").Produces<ReleaseListResponse>();

            return app;
        }
    }
}
