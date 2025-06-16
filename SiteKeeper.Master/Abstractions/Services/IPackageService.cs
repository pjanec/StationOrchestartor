using SiteKeeper.Shared.DTOs.API.PackageManagement;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Defines the contract for a service that handles package management information.
    /// This service is responsible for retrieving data about software packages, such as lists of installed packages,
    /// available versions for a specific package, and other package-related metadata. It serves as an abstraction
    /// layer between the API controllers/endpoints and the underlying package data sources or logic.
    /// </summary>
    public interface IPackageService
    {
        /// <summary>
        /// Lists all installed packages in the environment, with their status on each node.
        /// </summary>
        /// <param name="filterText">Optional text to filter packages by (e.g., name, description).</param>
        /// <param name="sortBy">Optional field to sort the package list by.</param>
        /// <param name="sortOrder">Optional sort order ("asc" or "desc").</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of <see cref="PackageEnvironmentStatus"/> objects.</returns>
        Task<List<PackageEnvironmentStatus>> ListInstalledPackagesAsync(string? filterText, string? sortBy, string? sortOrder);

        /// <summary>
        /// Lists all available versions for a specific package name.
        /// This method queries the available package versions (e.g., from a repository or internal database)
        /// and returns them in a structure that aligns with the API contract (Swagger: PackageVersionsResponse).
        /// </summary>
        /// <param name="packageName">The name of the package for which to retrieve available versions.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. 
        /// The task result contains a <see cref="PackageVersionsResponse"/> DTO, which includes the package name
        /// and a list of version strings. Returns null if the package is not found or no versions are available.
        /// </returns>
        Task<PackageVersionsResponse?> ListPackageVersionsAsync(string packageName);

        // Other methods from swagger like:
        // Task<PackageVersionsResponse> GetPackageVersionsAsync(string packageName);
        // These will be added as we implement the corresponding API endpoints if they fall under this service.
    }
} 