using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Packages
{
    /// <summary>
    /// Represents version information for a software package.
    /// As defined in swagger: #/components/schemas/PackageVersionInfo
    /// </summary>
    public class PackageVersionInfo
    {
        /// <summary>
        /// The name of the package.
        /// </summary>
        [JsonPropertyName("packageName")]
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// The original version string of the package.
        /// </summary>
        [JsonPropertyName("originalVersion")]
        public string OriginalVersion { get; set; } = string.Empty;
    }
} 