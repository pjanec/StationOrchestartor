using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Releases
{
    /// <summary>
    /// Represents metadata associated with a specific release version.
    /// Corresponds to the 'metadata' object within the 'ReleaseVersionDetailsResponse' schema in `web api swagger.yaml`.
    /// </summary>
    public class ReleaseMetadataInfo
    {
        /// <summary>
        /// The build number associated with this release.
        /// </summary>
        /// <example>"build-001"</example>
        [JsonPropertyName("buildNumber")]
        public string? BuildNumber { get; set; }

        /// <summary>
        /// A URL pointing to the changelog for this release.
        /// </summary>
        /// <example>"https://example.com/changelog/1.2.5"</example>
        [JsonPropertyName("changelogLink")]
        public string? ChangelogLink { get; set; }
    }
} 