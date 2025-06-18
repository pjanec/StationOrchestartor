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
    /// Defines API endpoints related to managing and retrieving information about software releases.
    /// This is a partial class, and these endpoints are registered via the main <see cref="ApiEndpoints.MapAll(WebApplication)"/> method.
    /// </summary>
    /// <remarks>
    /// This class utilizes Minimal API features to structure endpoints under the base path <c>/api/v1/releases</c>.
    /// General authorization is required for this group, with specific endpoints typically requiring at least Observer privileges
    /// for read-only operations. Endpoints interact primarily with the <see cref="IReleaseService"/> to retrieve release data.
    /// Operations related to applying a release (environment update) are typically handled via the Operations API.
    /// </remarks>
    public static partial class ApiEndpoints
    {
        /// <summary>
        /// Maps all API endpoints related to software release information.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to which the endpoints will be mapped (typically the <see cref="WebApplication"/> instance).</param>
        /// <param name="guiHostConstraint">A hostname constraint string to apply to these endpoints, ensuring they are only accessible via specific hostnames configured for GUI/API access.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with the newly mapped release information endpoints.</returns>
        /// <remarks>
        /// This method creates a route group for <c>/api/v1/releases</c> which requires general authorization.
        /// Currently, it defines endpoints for:
        /// <list type="bullet">
        ///   <item><description><c>GET /</c>: Lists all available releases. Requires Observer privileges.</description></item>
        ///   <item><description>(Future: <c>GET /{versionId}</c>: Gets detailed information for a specific release version. Requires Observer privileges.)</description></item>
        /// </list>
        /// </remarks>
        public static IEndpointRouteBuilder MapReleasesApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            var releasesGroup = app.MapGroup("/api/v1/releases")
                .WithTags("Releases")
                // General authorization for the group; specific endpoints also verify roles.
                .RequireAuthorization()
                .RequireHost(guiHostConstraint);

            // Defines GET /api/v1/releases
            // Retrieves a list of available software releases.
            // Requires Observer role. Calls IReleaseService.ListReleasesAsync.
            releasesGroup.MapGet("/",
            /// <summary>
            /// Retrieves a list of available software releases, optionally filtered by environment type (though current implementation passes null).
            /// Requires Observer or higher privileges.
            /// </summary>
            /// <param name="releaseService">The <see cref="IReleaseService"/> for fetching release information.</param>
            /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
            /// <returns>
            /// An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with a <see cref="ReleaseListResponse"/>
            /// containing the list of available releases, or <see cref="Results.Forbid()"/> if unauthorized.
            /// May return an empty list or null structure from the service if no releases are found or an error occurs.
            /// </returns>
            async ([FromServices] IReleaseService releaseService, ClaimsPrincipal user) =>
            {
                if (!user.IsObserverOrHigher()) return Results.Forbid(); // Authorization check
                // Currently passes null for environmentType, meaning the service might return all or use a default.
                var releases = await releaseService.ListReleasesAsync(null);
                return Results.Ok(releases);
            }).WithSummary("Get available releases for update").Produces<ReleaseListResponse>();

            return app;
        }
    }
}
