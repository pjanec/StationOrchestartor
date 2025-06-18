using SiteKeeper.Shared.DTOs.API.PackageManagement;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Defines the contract for a service that handles package management information and related operations.
    /// </summary>
    /// <remarks>
    /// This service is responsible for retrieving data about software packages, such as lists of installed packages,
    /// available versions for a specific package, and other package-related metadata. It serves as an abstraction
    /// layer between API controllers/endpoints and underlying package data sources (e.g., configuration management,
    /// package repositories, or cached agent states).
    /// Operations to change package states (install, uninstall, change version) are typically coordinated by
    /// <see cref="IMasterActionCoordinatorService"/> but might be initiated based on data provided by this service.
    /// </remarks>
    public interface IPackageService
    {
        /// <summary>
        /// Lists all installed packages across the managed environment, detailing their version and status on each relevant node.
        /// This method is typically called by API controllers serving an endpoint like GET /api/packages.
        /// </summary>
        /// <param name="filterText">Optional text to filter packages by (e.g., name, description).</param>
        /// <param name="sortBy">Optional field name to sort the results by (e.g., "packageName", "type").</param>
        /// <param name="sortOrder">Optional sort order ("asc" or "desc").</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of <see cref="PackageEnvironmentStatus"/> DTOs.</returns>
        Task<List<PackageEnvironmentStatus>> ListInstalledPackagesAsync(string? filterText, string? sortBy, string? sortOrder);

        /// <summary>
        /// Lists all available versions for a specific package identified by its name.
        /// This method queries known package sources (e.g., a repository, internal database, or manifest definitions)
        /// and returns the versions in a structure suitable for API responses.
        /// This method is typically called by API controllers serving an endpoint like GET /api/packages/{packageName}/versions.
        /// </summary>
        /// <param name="packageName">The unique name of the package for which to retrieve available versions.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. 
        /// The task result contains a <see cref="PackageVersionsResponse"/> DTO, which includes the package name
        /// and a list of its available version strings. Returns null if the package name is not found or no versions are registered.
        /// </returns>
        Task<PackageVersionsResponse?> ListPackageVersionsAsync(string packageName);

        // Other methods from swagger like:
        // Task<PackageVersionsResponse> GetPackageVersionsAsync(string packageName); // This seems to be a duplicate of ListPackageVersionsAsync based on DTO name.
        // Task<OperationInitiationResponse> InstallOptionalPackageAsync(PackageNameRequest request, ClaimsPrincipal user); // via IMasterActionCoordinatorService
        // Task<OperationInitiationResponse> UninstallOptionalPackageAsync(PackageNameRequest request, ClaimsPrincipal user); // via IMasterActionCoordinatorService
        // Task<OperationInitiationResponse> ChangePackageVersionAsync(PackageChangeVersionRequest request, ClaimsPrincipal user); // via IMasterActionCoordinatorService
        // Task<OperationInitiationResponse> RevertPackageDeviationsAsync(PackageOperationRequest request, ClaimsPrincipal user); // via IMasterActionCoordinatorService
        // Task<OperationInitiationResponse> RefreshPackagesAsync(PackageRefreshRequest request, ClaimsPrincipal user); // via IMasterActionCoordinatorService
        // These will be added as we implement the corresponding API endpoints if they fall under this service.
    }
} 