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
    /// Provides API endpoints for package management. This is a partial class that extends the main ApiEndpoints.
    /// The methods within this class are responsible for mapping all package-related endpoints.
    /// </summary>
    public static partial class ApiEndpoints
    {
        /// <summary>
        /// Maps all package-related endpoints, such as listing packages and their versions.
        /// These endpoints are grouped under the "/api/v1/packages" route and tagged for OpenAPI documentation.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to map endpoints to.</param>
        /// <param name="guiHostConstraint">The host string to constrain the endpoints to.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with mapped endpoints.</returns>
        public static IEndpointRouteBuilder MapPackagesApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            var packagesGroup = app.MapGroup("/api/v1/packages")
                .WithTags("Packages")
                .RequireAuthorization()
                .RequireHost(guiHostConstraint);

            packagesGroup.MapGet("/", async ([FromServices] IPackageService packageService, ClaimsPrincipal user, [FromQuery] string? filterText, [FromQuery] string? sortBy, [FromQuery] string? sortOrder, [FromServices] ILogger<MasterConfig> logger) =>
            {
                if (!user.IsObserverOrHigher())
                {
                    logger.LogWarning("API: Forbidden GET /packages attempt by user {User}", user.GetUsername());
                    return Results.Forbid();
                }
                logger.LogInformation("API: Request to list installed packages by user {User}. Filter: {Filter}, SortBy: {SortBy}, SortOrder: {SortOrder}", user.GetUsername(), filterText, sortBy, sortOrder);
                var packages = await packageService.ListInstalledPackagesAsync(filterText, sortBy, sortOrder);
                return Results.Ok(packages);
            })
            .WithName("ListInstalledPackages")
            .WithSummary("List all installed packages in the environment")
            .WithDescription("Retrieves a list of all installed packages, their types, and status on each node. Supports filtering and sorting.")
            .Produces<List<PackageEnvironmentStatus>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden);

            packagesGroup.MapGet("/{packageName}/versions", async (string packageName, [FromServices] IPackageService packageService, [FromServices] ILogger<MasterConfig> logger, ClaimsPrincipal user) =>
            {
                if (!user.IsObserverOrHigher())
                {
                    logger.LogWarning("API: Forbidden GET /packages/{PackageName}/versions attempt by user {User}", packageName, user.GetUsername());
                    return Results.Forbid();
                }

                logger.LogInformation("API: Request to get versions for package: {PackageName} by user {User}", packageName, user.GetUsername());
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
