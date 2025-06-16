using System;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Releases
{
    /// <summary>
    /// Provides summary information about an available release version.
    /// Corresponds to the 'ReleaseVersionInfo' schema in `web api swagger.yaml`,
    /// specifically as an item in the `ReleaseListResponse`.
    /// </summary>
    public class ReleaseVersionInfo
    {
        /// <summary>
        /// The unique identifier of the release version.
        /// </summary>
        /// <example>"1.2.5"</example>
        [Required]
        [JsonPropertyName("versionId")]
        public string VersionId { get; set; } = string.Empty;

        /// <summary>
        /// The date when this version was released.
        /// </summary>
        /// <example>"2025-05-30T10:00:00Z"</example>
        [Required]
        [JsonPropertyName("releaseDate")]
        public DateTime ReleaseDate { get; set; }

        /// <summary>
        /// A brief description of the release.
        /// </summary>
        /// <example>"Latest stable release."</example>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Indicates if this is the latest available release.
        /// Corresponds to 'isLatest' in Swagger schema for ReleaseListResponse.versions.items.
        /// </summary>
        [JsonPropertyName("isLatest")]
        public bool IsLatest { get; set; }
    }
} 