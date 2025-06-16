using Microsoft.Extensions.Logging;
using SiteKeeper.Shared.DTOs.API.PackageManagement;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace SiteKeeper.Master.Services.Placeholders
{
    /// <summary>
    /// Placeholder implementation of the <see cref="IPackageService"/>.
    /// This service provides mocked or sample data for package management functionalities
    /// for development and testing purposes before a real implementation is available.
    /// </summary>
    /// <remarks>
    /// The methods in this class simulate interactions with a package repository or manifest system.
    /// It returns predefined data that aligns with the expected DTO structures, including the
    /// <see cref="PackageVersionsResponse"/> which now uses a simple list of version strings.
    /// The sample data for installed packages uses the <see cref="NodePackageVersionStatus"/> DTO
    /// to represent the node-specific version information, aligning with the structure of
    /// the <c>PackageEnvironmentStatus</c> DTO's 'nodes' property.
    /// </remarks>
    public class PlaceholderPackageService : IPackageService
    {
        private readonly ILogger<PlaceholderPackageService> _logger;

        // Sample data for installed packages
        private readonly List<PackageEnvironmentStatus> _sampleInstalledPackages;

        // Sample data for package versions
        private readonly Dictionary<string, List<string>> _packageVersions = new Dictionary<string, List<string>>
        {
            { "CoreApp-bin", new List<string> { "1.4.0", "1.5.0", "1.5.1" } },
            { "CoreApp-conf", new List<string> { "1.1.0", "1.2.0", "1.2.1", "1.3.0" } },
            { "OptionalToolA", new List<string> { "0.9.0", "1.0.0" } },
            { "NewToolB", new List<string> { "2.0.0", "2.1.0-beta" } }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaceholderPackageService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for this service.</param>
        public PlaceholderPackageService(ILogger<PlaceholderPackageService> logger)
        {
            _logger = logger;
            // Initialize sample data for installed packages
            _sampleInstalledPackages = new List<PackageEnvironmentStatus>
            {
                new PackageEnvironmentStatus
                {
                    PackageName = "CoreApp-bin",
                    Type = PackageType.Core,
                    Nodes = new List<NodePackageVersionStatus>
                    {
                        new NodePackageVersionStatus { NodeName = "SIMSERVER", CurrentVersion = "1.5.0" },
                        new NodePackageVersionStatus { NodeName = "IOS1", CurrentVersion = "1.5.0" }
                    }
                },
                new PackageEnvironmentStatus
                {
                    PackageName = "CoreApp-conf",
                    Type = PackageType.Core,
                    Nodes = new List<NodePackageVersionStatus>
                    {
                        new NodePackageVersionStatus { NodeName = "SIMSERVER", CurrentVersion = "1.2.1" },
                        new NodePackageVersionStatus { NodeName = "IOS1", CurrentVersion = "1.2.1" }
                    }
                },
                new PackageEnvironmentStatus
                {
                    PackageName = "OptionalToolA",
                    Type = PackageType.Optional,
                    Nodes = new List<NodePackageVersionStatus>
                    {
                        new NodePackageVersionStatus { NodeName = "SIMSERVER", CurrentVersion = "1.0.0" }
                    }
                }
            };
        }

        /// <summary>
        /// Lists all installed packages, applying optional filtering and sorting.
        /// This is a placeholder implementation and currently returns a predefined list of sample packages.
        /// Actual filtering and sorting logic would be more complex in a real service.
        /// </summary>
        /// <param name="filterText">Text to filter package names by (case-insensitive contains).</param>
        /// <param name="sortBy">Field to sort by (currently supports "packageName" or defaults to no sort).</param>
        /// <param name="sortOrder">Sort order ("asc" or "desc").</param>
        /// <returns>A list of <see cref="PackageEnvironmentStatus"/> objects representing installed packages.</returns>
        public Task<List<PackageEnvironmentStatus>> ListInstalledPackagesAsync(string? filterText, string? sortBy, string? sortOrder)
        {
            _logger.LogInformation("Listing installed packages with filter: '{FilterText}', sortBy: '{SortBy}', sortOrder: '{SortOrder}' (Placeholder).", filterText, sortBy, sortOrder);
            IEnumerable<PackageEnvironmentStatus> query = _sampleInstalledPackages;

            if (!string.IsNullOrWhiteSpace(filterText))
            {
                query = query.Where(p => p.PackageName.Contains(filterText, System.StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(sortBy) && sortBy.Equals("packageName", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(sortOrder) && sortOrder.Equals("desc", System.StringComparison.OrdinalIgnoreCase))
                {
                    query = query.OrderByDescending(p => p.PackageName);
                }
                else
                {
                    query = query.OrderBy(p => p.PackageName);
                }
            }
            return Task.FromResult(query.ToList());
        }

        /// <summary>
        /// Lists all available versions for a specific package name.
        /// This placeholder implementation returns a predefined list of version strings for known packages.
        /// The returned DTO <see cref="PackageVersionsResponse"/> now contains a simple list of strings for versions,
        /// aligning with the updated Swagger definition.
        /// </summary>
        /// <param name="packageName">The name of the package for which to list versions.</param>
        /// <returns>
        /// A <see cref="PackageVersionsResponse"/> containing the package name and its available versions as strings.
        /// Returns null if the package name is not found in the sample data.
        /// </returns>
        public Task<PackageVersionsResponse?> ListPackageVersionsAsync(string packageName)
        {
            _logger.LogInformation("Listing versions for package: '{PackageName}' (Placeholder).", packageName);
            if (_packageVersions.TryGetValue(packageName, out var versions))
            {
                var response = new PackageVersionsResponse
                {
                    PackageName = packageName,
                    Versions = versions
                };
                return Task.FromResult<PackageVersionsResponse?>(response);
            }
            _logger.LogWarning("Package '{PackageName}' not found for version listing (Placeholder).", packageName);
            return Task.FromResult<PackageVersionsResponse?>(null);
        }
    }
} 