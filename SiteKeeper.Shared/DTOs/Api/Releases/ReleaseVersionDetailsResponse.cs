using SiteKeeper.Shared.DTOs.API.Environment; // For PureManifest
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Releases
{
    /// <summary>
    /// DTO for detailed information about a specific release version.
    /// Aligns with the 'ReleaseVersionDetailsResponse' schema in `web api swagger.yaml`.
    /// </summary>
    /// <remarks>
    /// This DTO provides comprehensive details for a release, including its environment,
    /// version ID, release date, description, the full manifest content, and associated metadata.
    /// It was previously named ReleaseDetailsResponse.
    /// </remarks>
    public class ReleaseVersionDetailsResponse
    {
        /// <summary>
        /// Gets or sets the type of environment this release is typically associated with.
        /// </summary>
        /// <example>"Production"</example>
        [Required]
        [JsonPropertyName("environmentType")]
        public string EnvironmentType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique identifier for this release version.
        /// </summary>
        /// <example>"PROD-2023.07.21-01"</example>
        [Required]
        [JsonPropertyName("versionId")]
        public string VersionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the date and time when this release was made available or published.
        /// </summary>
        [Required]
        [JsonPropertyName("releaseDate")]
        public DateTime ReleaseDate { get; set; }

        /// <summary>
        /// Gets or sets an optional description for this release.
        /// </summary>
        /// <example>"Quarterly update with new features."</example>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the pure manifest associated with this release version.
        /// The manifest details the intended state of the environment for this release.
        /// </summary>
        [Required]
        [JsonPropertyName("manifest")]
        public PureManifest Manifest { get; set; } = new PureManifest();

        /// <summary>
        /// Gets or sets metadata associated with this release, such as build number or changelog link.
        /// </summary>
        [JsonPropertyName("metadata")]
        public ReleaseMetadataInfo? Metadata { get; set; }
    }
} 