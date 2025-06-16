using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.DTOs.API.Releases;
using SiteKeeper.Shared.DTOs.API.Environment; // For PureManifest, NodeInManifest, PackageInManifest, PackageVersionInfo
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace SiteKeeper.Master.Services.Placeholders
{
    /// <summary>
    /// Placeholder implementation of the <see cref="IReleaseService"/>.
    /// This service provides mocked or sample data for release management functionalities,
    /// such as listing releases and getting release details. It's intended for development
    /// and testing before a real implementation connected to a release data source is available.
    /// </summary>
    /// <remarks>
    /// The data returned by this service, including <see cref="ReleaseListResponse"/> and <see cref="ReleaseVersionDetailsResponse"/>,
    /// is predefined and aims to simulate realistic scenarios and DTO structures.
    /// The sample <see cref="PureManifest"/> data is populated according to the current C# definition of <c>PureManifest.cs</c>.
    /// Note that <c>PureManifest.cs</c> itself has properties not present in its Swagger definition, which is a separate conformance issue.
    /// </remarks>
    public class PlaceholderReleaseService : IReleaseService
    {
        private readonly ILogger<PlaceholderReleaseService> _logger;
        private readonly List<ReleaseVersionDetailsResponse> _sampleReleaseDetails;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaceholderReleaseService"/> class.
        /// Prepares sample data for releases.
        /// </summary>
        /// <param name="logger">The logger instance for this service.</param>
        public PlaceholderReleaseService(ILogger<PlaceholderReleaseService> logger)
        {
            _logger = logger;
            _sampleReleaseDetails = new List<ReleaseVersionDetailsResponse>
            {
                new ReleaseVersionDetailsResponse
                {
                    EnvironmentType = "Production",
                    VersionId = "PROD-2023.07.21-01",
                    ReleaseDate = new DateTime(2023, 7, 21, 10, 0, 0, DateTimeKind.Utc),
                    Description = "Stable production release with major feature updates.",
                    Manifest = new PureManifest 
                    {
                        EnvironmentName = "Production Environment", // Using actual PureManifest.cs properties
                        VersionId = "PROD-2023.07.21-01_manifest_v1", // Using actual PureManifest.cs properties
                        AppliedAt = new DateTime(2023, 7, 21, 0, 0, 0, DateTimeKind.Utc), // Using actual PureManifest.cs properties
                        OptionalPackagesDefinedInManifest = new List<PackageVersionInfo>(),
                        Nodes = new List<NodeInManifest>
                        {
                            new NodeInManifest { NodeName = "SIMSERVER", Packages = new List<PackageInManifest> { new PackageInManifest { PackageName = "CoreSim", OriginalVersion = "1.5.0"} } },
                            new NodeInManifest { NodeName = "IOS1", Packages = new List<PackageInManifest> { new PackageInManifest { PackageName = "CoreIOS", OriginalVersion = "2.3.1"} } }
                        }
                    },
                    Metadata = new ReleaseMetadataInfo { BuildNumber = "build-501", ChangelogLink = "https://example.com/changelog/prod-2023.07.21-01" }
                },
                new ReleaseVersionDetailsResponse
                {
                    EnvironmentType = "Staging",
                    VersionId = "STAGING-2023.08.01-03",
                    ReleaseDate = new DateTime(2023, 8, 1, 15, 30, 0, DateTimeKind.Utc),
                    Description = "Latest staging candidate for upcoming production release.",
                    Manifest = new PureManifest 
                    {
                        EnvironmentName = "Staging Environment", // Using actual PureManifest.cs properties
                        VersionId = "STAGING-2023.08.01-03_manifest_v1", // Using actual PureManifest.cs properties
                        AppliedAt = new DateTime(2023, 8, 1, 0, 0, 0, DateTimeKind.Utc), // Using actual PureManifest.cs properties
                        OptionalPackagesDefinedInManifest = new List<PackageVersionInfo>(),
                        Nodes = new List<NodeInManifest>
                        {
                             new NodeInManifest { NodeName = "STG-SIM", Packages = new List<PackageInManifest> { new PackageInManifest { PackageName = "CoreSim", OriginalVersion = "1.6.0-beta"} } },
                        }
                    },
                    Metadata = new ReleaseMetadataInfo { BuildNumber = "build-550", ChangelogLink = "https://example.com/changelog/staging-2023.08.01-03" }
                },
                 new ReleaseVersionDetailsResponse
                {
                    EnvironmentType = "Production",
                    VersionId = "PROD-2023.06.15-01",
                    ReleaseDate = new DateTime(2023, 6, 15, 9, 0, 0, DateTimeKind.Utc),
                    Description = "Previous stable production release.",
                    Manifest = new PureManifest 
                    {
                        EnvironmentName = "Production Environment", // Using actual PureManifest.cs properties
                        VersionId = "PROD-2023.06.15-01_manifest_v1", // Using actual PureManifest.cs properties
                        AppliedAt = new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc), // Using actual PureManifest.cs properties
                        Nodes = new List<NodeInManifest>(), // Empty nodes list for simplicity in this example
                        OptionalPackagesDefinedInManifest = new List<PackageVersionInfo>()
                    },
                    Metadata = new ReleaseMetadataInfo { BuildNumber = "build-490" }
                }
            };
        }

        /// <summary>
        /// Lists all available releases, optionally filtered by environment type.
        /// This placeholder implementation constructs a <see cref="ReleaseListResponse"/> from sample data.
        /// </summary>
        /// <param name="environmentType">Optional filter for the environment type.</param>
        /// <returns>A <see cref="ReleaseListResponse"/> containing summaries of matching releases, or null if no data (though this placeholder always returns some data unless filtered to empty).</returns>
        public Task<ReleaseListResponse?> ListReleasesAsync(string? environmentType)
        {
            _logger.LogInformation("Placeholder: Listing releases. Filter - EnvironmentType: {EnvironmentType}", environmentType ?? "any");
            
            // Determine a default or overall environment type for the response.
            // If filtering, use that type. Otherwise, pick a common one or make it more generic.
            // For this placeholder, if a specific environmentType is requested and found, we'll use it.
            // Otherwise, we might need a convention if releases from multiple env types are returned without a filter.
            // Swagger's ReleaseListResponse has a single 'environmentType' field at the root.
            string responseEnvironmentType = environmentType ?? "Mixed"; // Default if no filter and multiple types exist

            var filteredDetails = _sampleReleaseDetails
                .Where(details => string.IsNullOrWhiteSpace(environmentType) || details.EnvironmentType.Equals(environmentType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (filteredDetails.Any() && environmentType != null)
            {
                responseEnvironmentType = environmentType; // Use the filtered type if specific and results exist
            }
            else if (filteredDetails.Any())
            {
                // If no filter, but results exist, try to pick the most common one or default
                var commonType = filteredDetails.GroupBy(d => d.EnvironmentType)
                                              .OrderByDescending(g => g.Count())
                                              .FirstOrDefault()?.Key;
                responseEnvironmentType = commonType ?? "General"; 
            }

            var releaseVersions = filteredDetails
                .Select(details => new ReleaseVersionInfo
                {
                    VersionId = details.VersionId,
                    ReleaseDate = details.ReleaseDate,
                    Description = details.Description,
                    // Determine IsLatest based on the filtered set for the given environmentType or overall if no type filter
                    IsLatest = details.VersionId == filteredDetails.OrderByDescending(r => r.ReleaseDate).FirstOrDefault()?.VersionId
                })
                .ToList();

            var response = new ReleaseListResponse
            {
                EnvironmentType = responseEnvironmentType,
                Versions = releaseVersions
            };
            
            return Task.FromResult<ReleaseListResponse?>(response);
        }

        /// <summary>
        /// Gets detailed information for a specific release version from sample data.
        /// The returned DTO is <see cref="ReleaseVersionDetailsResponse"/>.
        /// </summary>
        /// <param name="versionId">The unique identifier of the release version.</param>
        /// <returns>A <see cref="ReleaseVersionDetailsResponse"/> with the release details, or null if not found in sample data.</returns>
        public Task<ReleaseVersionDetailsResponse?> GetReleaseDetailsAsync(string versionId)
        {
            _logger.LogInformation("Placeholder: Getting release details for VersionId: {VersionId}", versionId);
            var details = _sampleReleaseDetails.FirstOrDefault(r => r.VersionId.Equals(versionId, StringComparison.OrdinalIgnoreCase));
            
            if (details == null)
            {
                _logger.LogWarning("Placeholder: Release VersionId {VersionId} not found.", versionId);
            }
            return Task.FromResult(details);
        }
    }
} 