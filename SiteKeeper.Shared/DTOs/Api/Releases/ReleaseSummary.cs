using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Releases
{
    /// <summary>
    /// Provides a summary of a specific release version.
    /// This DTO aligns with the 'ReleaseSummary' schema defined in `web api swagger.yaml`.
    /// It is typically used in lists of releases.
    /// </summary>
    /// <remarks>
    /// This DTO contains key information about a release, such as its version ID, associated environment type,
    /// release date, a brief description, and an indicator if it's the latest known release.
    /// It's a component of <see cref="ReleaseListResponse"/>.
    /// </remarks>
    public class ReleaseSummary
    {
        /// <summary>
        /// Gets or sets the type of environment this release is typically associated with.
        /// Corresponds to the 'environmentType' property in the Swagger schema.
        /// </summary>
        /// <example>"Production"</example>
        [Required]
        [JsonPropertyName("environmentType")]
        public string EnvironmentType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique identifier for this release version.
        /// Corresponds to the 'versionId' property in the Swagger schema.
        /// </summary>
        /// <example>"PROD-2023.07.21-01"</example>
        [Required]
        [JsonPropertyName("versionId")]
        public string VersionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the date and time when this release was made available or published.
        /// Corresponds to the 'releaseDate' property in the Swagger schema (format: date-time).
        /// </summary>
        [Required]
        [JsonPropertyName("releaseDate")]
        public DateTime ReleaseDate { get; set; }

        /// <summary>
        /// Gets or sets an optional description for this release.
        /// Corresponds to the 'description' property in the Swagger schema (nullable).
        /// </summary>
        /// <example>"Quarterly update with new features."</example>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating if this release is considered the latest for its environment type.
        /// This is an optional hint, primarily for UI purposes.
        /// Corresponds to the 'isCurrentLatest' property in the Swagger schema (nullable).
        /// </summary>
        /// <example>true</example>
        [JsonPropertyName("isCurrentLatest")]
        public bool? IsCurrentLatest { get; set; }
    }
} 