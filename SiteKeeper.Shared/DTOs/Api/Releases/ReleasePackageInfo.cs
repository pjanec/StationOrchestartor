using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Releases
{
    /// <summary>
    /// Describes a package included in a specific release version.
    /// Aligns with ReleasePackageInfo schema in `web api swagger.yaml`.
    /// </summary>
    public class ReleasePackageInfo
    {
        /// <summary>
        /// Name of the package.
        /// </summary>
        /// <example>"CoreApplication"</example>
        [JsonPropertyName("packageName")]
        [Required]
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// Version of the package in this release.
        /// </summary>
        /// <example>"1.2.3"</example>
        [JsonPropertyName("packageVersion")]
        [Required]
        public string PackageVersion { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if this package is mandatory for the release.
        /// </summary>
        [JsonPropertyName("isMandatory")]
        public bool IsMandatory { get; set; }
    }
} 