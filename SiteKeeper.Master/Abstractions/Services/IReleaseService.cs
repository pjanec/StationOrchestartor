using SiteKeeper.Shared.DTOs.API.Releases;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Defines the contract for a service that manages release information for the SiteKeeper environment.
    /// </summary>
    /// <remarks>
    /// This service is responsible for operations such as retrieving lists of available releases,
    /// fetching details for a specific release version (including its manifest and metadata),
    /// and potentially other release-related actions. It acts as an intermediary between the
    /// API layer (e.g., controllers under /api/releases) and the underlying data sources or business logic for releases.
    /// </remarks>
    public interface IReleaseService
    {
        /// <summary>
        /// Lists all available releases, optionally filtered by environment type.
        /// The response structure <see cref="ReleaseListResponse"/> contains an environment type and a list of <see cref="ReleaseVersionInfo"/> objects.
        /// This method is typically called by API controllers serving an endpoint like GET /api/releases.
        /// </summary>
        /// <param name="environmentType">Optional filter to get releases for a specific environment type (e.g., "Production", "Staging"). If null or empty, releases for all relevant environment types may be returned, depending on implementation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. 
        /// The task result contains a <see cref="ReleaseListResponse"/> DTO which includes the environment type
        /// and a list of <see cref="ReleaseVersionInfo"/> summaries for each version.
        /// Returns null if an error occurs or if the response should be handled as 'not found' at a higher level for specific scenarios.
        /// </returns>
        Task<ReleaseListResponse?> ListReleasesAsync(string? environmentType);

        /// <summary>
        /// Gets detailed information for a specific release version.
        /// This method retrieves comprehensive details for the given versionId, including its manifest and metadata,
        /// structured according to the <see cref="ReleaseVersionDetailsResponse"/> DTO which aligns with the Swagger definition.
        /// This method is typically called by API controllers serving an endpoint like GET /api/releases/{versionId}.
        /// </summary>
        /// <param name="versionId">The unique identifier of the release version to retrieve.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains a <see cref="ReleaseVersionDetailsResponse"/> DTO with the release details.
        /// Returns null if no release is found for the specified versionId.
        /// </returns>
        Task<ReleaseVersionDetailsResponse?> GetReleaseDetailsAsync(string versionId);

        // Other methods from swagger like:
        // Task<ReleaseVersionDetailsResponse> GetReleaseVersionDetailsAsync(string versionId); // Seems like a duplicate of GetReleaseDetailsAsync
        // These will be added as we implement the corresponding API endpoints if they fall under this service.
    }
} 