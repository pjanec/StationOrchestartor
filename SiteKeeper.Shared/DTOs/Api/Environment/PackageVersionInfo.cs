using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Environment
{
    /// <summary>
    /// Represents package and version information, typically used within a manifest listing optional packages.
    /// As defined in swagger: #/components/schemas/PackageVersionInfo (used by PureManifest)
    /// </summary>
    public class PackageVersionInfo
    {
        /// <summary>
        /// The name of the package.
        /// </summary>
        /// <example>"OptionalToolA"</example>
        [Required]
        [JsonPropertyName("packageName")]
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// The specific version of the package.
        /// </summary>
        /// <example>"1.0.0"</example>
        [Required]
        [JsonPropertyName("originalVersion")]
        public string OriginalVersion { get; set; } = string.Empty;
    }
} 