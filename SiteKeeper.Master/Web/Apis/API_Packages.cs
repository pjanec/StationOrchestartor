using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SiteKeeper.Master;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.DTOs.API.PackageManagement;
using SiteKeeper.Shared.DTOs.Common;
using SiteKeeper.Shared.Security;
using System.Collections.Generic;
using System.Security.Claims;

namespace SiteKeeper.Master.Web.Apis
{
    /// <summary>
    /// Defines API endpoints related to package management, such as listing installed packages and their versions.
    /// This is a partial class, and these endpoints are registered via the main <see cref="ApiEndpoints.MapAll(WebApplication)"/> method.
    /// </summary>
    /// <remarks>
    /// This class utilizes Minimal API features to structure endpoints under the base path <c>/api/v1/packages</c>.
    /// General authorization is required for this group, with specific endpoints typically requiring at least Observer privileges
    /// for read-only operations. Endpoints interact primarily with the <see cref="IPackageService"/> to retrieve package information.
    /// Actions related to modifying package states (install, uninstall, update) are typically handled via the Operations API.
    /// </remarks>
    public static partial class ApiEndpoints
    {
        /// <summary>
        /// Maps all API endpoints related to package information retrieval.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to which the endpoints will be mapped (typically the <see cref="WebApplication"/> instance).</param>
        /// <param name="guiHostConstraint">A hostname constraint string to apply to these endpoints, ensuring they are only accessible via specific hostnames configured for GUI/API access.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with the newly mapped package information endpoints.</returns>
        /// <remarks>
        /// This method creates a route group for <c>/api/v1/packages</c> which requires general authorization.
        /// It defines endpoints for:
        /// <list type="bullet">
        ///   <item><description><c>GET /</c>: Lists all installed packages across the environment with their status on each node. Requires Observer privileges.</description></item>
        ///   <item><description><c>GET /{packageName}/versions</c>: Lists all available versions for a specific package. Requires Observer privileges.</description></item>
        /// </list>
        /// </remarks>
        public static IEndpointRouteBuilder MapPackagesApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            var packagesGroup = app.MapGroup("/api/v1/packages")
                .WithTags("Packages")
                // General authorization for the group; specific endpoints also verify roles.
                .RequireAuthorization()
                .RequireHost(guiHostConstraint);

            // Defines GET /api/v1/packages
            // Retrieves a list of all installed packages and their status across the environment.
            // Requires Observer role. Calls IPackageService.ListInstalledPackagesAsync.
            // Supports filtering and sorting.
            packagesGroup.MapGet("/",
            /// <summary>
            /// Retrieves a list of all installed packages across the environment, detailing their version and status on each node.
            /// Supports optional filtering by text, and sorting by package name or other fields.
            /// Requires Observer or higher privileges.
            /// </summary>
            /// <param name="packageService">The <see cref="IPackageService"/> for fetching package information.</param>
            /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
            /// <param name="filterText">Optional text to filter packages by (e.g., name, description).</param>
            /// <param name="sortBy">Optional field name to sort the results by (e.g., "packageName").</param>
            /// <param name="sortOrder">Optional sort order ("asc" or "desc").</param>
            /// <param name="logger">A logger for this endpoint.</param>
            /// <returns>
            /// An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with a list of <see cref="PackageEnvironmentStatus"/> DTOs,
            /// or <see cref="Results.Forbid()"/> if unauthorized.
            /// </returns>
            async ([FromServices] IPackageService packageService, ClaimsPrincipal user, [FromQuery] string? filterText, [FromQuery] string? sortBy, [FromQuery] string? sortOrder, [FromServices] ILogger<MasterConfig> logger) =>
            {
                if (!user.IsObserverOrHigher())
                {
                    logger.LogWarning("API: Forbidden GET /packages attempt by user {User}", user.GetUsername() ?? "unknown");
                    return Results.Forbid();
                }
                logger.LogInformation("API: Request to list installed packages by user {User}. Filter: {Filter}, SortBy: {SortBy}, SortOrder: {SortOrder}", user.GetUsername() ?? "unknown", filterText, sortBy, sortOrder);
                var packages = await packageService.ListInstalledPackagesAsync(filterText, sortBy, sortOrder);
                return Results.Ok(packages);
            })
            .WithName("ListInstalledPackages")
            .WithSummary("List all installed packages in the environment")
            .WithDescription("Retrieves a list of all installed packages, their types, and status on each node. Supports filtering and sorting. Requires Observer role or higher.")
            .Produces<List<PackageEnvironmentStatus>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden);

            // Defines GET /api/v1/packages/{packageName}/versions
            // Retrieves a list of available versions for a specific package.
            // Requires Observer role. Calls IPackageService.ListPackageVersionsAsync.
            packagesGroup.MapGet("/{packageName}/versions",
            /// <summary>
            /// Retrieves a list of all available versions for a specific package, identified by its <paramref name="packageName"/>.
            /// Requires Observer or higher privileges.
            /// </summary>
            /// <param name="packageName">The unique name of the package for which to retrieve available versions.</param>
            /// <param name="packageService">The <see cref="IPackageService"/> for fetching package version information.</param>
            /// <param name="logger">A logger for this endpoint.</param>
            /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
            /// <returns>
            /// An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with a <see cref="PackageVersionsResponse"/> on success.
            /// Returns <see cref="Results.NotFound(object?)"/> with an <see cref="ErrorResponse"/> if the package is not found or has no versions.
            /// Returns <see cref="Results.Forbid()"/> if unauthorized.
            /// </returns>
            async (string packageName, [FromServices] IPackageService packageService, [FromServices] ILogger<MasterConfig> logger, ClaimsPrincipal user) =>
            {
                if (!user.IsObserverOrHigher())
                {
                    logger.LogWarning("API: Forbidden GET /packages/{PackageName}/versions attempt by user {User}", packageName, user.GetUsername() ?? "unknown");
                    return Results.Forbid();
                }

                logger.LogInformation("API: Request to get versions for package: {PackageName} by user {User}", packageName, user.GetUsername() ?? "unknown");
                PackageVersionsResponse? result = await packageService.ListPackageVersionsAsync(packageName);
                if (result is null)
                {
                    logger.LogWarning("API: Package not found or no versions available for {PackageName}", packageName);
                    return Results.NotFound(new ErrorResponse { Error = "PackageNotFound", Message = $"Package '{packageName}' not found or no versions available." });
                }
                return Results.Ok(result);
            })
            .WithName("GetPackageVersions")
            .WithSummary("Get available versions for a package")
            .WithDescription("Retrieves a list of all available versions for the specified package name.")
            .Produces<PackageVersionsResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsObserverOrHigher()));

            return app;
        }
    }
}
