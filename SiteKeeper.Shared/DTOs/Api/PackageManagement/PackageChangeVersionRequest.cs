using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.PackageManagement
{
    /// <summary>
    /// Represents the request to change the version of a package on specified nodes.
    /// Based on the PackageChangeVersionRequest schema in `web api swagger.yaml`.
    /// </summary>
    public class PackageChangeVersionRequest
    {
        /// <summary>
        /// The name of the package to change.
        /// </summary>
        /// <example>"CoreApp-bin"</example>
        [Required]
        [JsonPropertyName("packageName")]
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// The target version to which the package should be changed.
        /// </summary>
        /// <example>"1.5.1"</example>
        [Required]
        [JsonPropertyName("targetVersion")]
        public string TargetVersion { get; set; } = string.Empty;

        /// <summary>
        /// List of node names to target for the version change. This field is required.
        /// </summary>
        /// <example>["SIMSERVER", "IOS1"]</example>
        [Required]
        [JsonPropertyName("nodeNames")]
        public List<string> NodeNames { get; set; } = new List<string>();
    }
} 