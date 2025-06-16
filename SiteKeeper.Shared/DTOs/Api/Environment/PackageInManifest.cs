using SiteKeeper.Shared.Enums;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Environment
{
    /// <summary>
    /// Represents a package as defined within a node in the environment manifest.
    /// As defined in swagger: #/components/schemas/PackageInManifest
    /// </summary>
    /// <remarks>
    /// This DTO is used to describe individual packages listed in a <see cref="PureManifest"/>.
    /// It specifies the package name, its target version for the environment, and its manifest type (e.g., Core, Optional).
    /// Based on the PackageInManifest schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class PackageInManifest
    {
        /// <summary>
        /// The name of the package.
        /// </summary>
        /// <example>"CoreApp-bin"</example>
        [JsonPropertyName("packageName")]
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// The version of the package as defined in the manifest.
        /// </summary>
        /// <example>"1.5.0"</example>
        [JsonPropertyName("originalVersion")]
        public string OriginalVersion { get; set; } = string.Empty;

        /// <summary>
        /// The type of the package.
        /// </summary>
        /// <example>PackageType.Core</example>
        [JsonPropertyName("type")]
        public PackageType Type { get; set; }
    }
} 