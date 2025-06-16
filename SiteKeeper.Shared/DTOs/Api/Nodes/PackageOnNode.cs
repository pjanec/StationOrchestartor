using SiteKeeper.Shared.Enums; // Assuming PackageType enum might be here or similar
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Nodes
{
    /// <summary>
    /// Represents a software package installed on a specific node.
    /// </summary>
    /// <remarks>
    /// This DTO is used in the response for listing packages on a node (e.g., GET /nodes/{nodeName}/packages).
    /// It aligns with the 'PackageOnNode' schema defined in `web api swagger.yaml`.
    /// </remarks>
    public class PackageOnNode
    {
        /// <summary>
        /// Gets or sets the name of the package.
        /// </summary>
        /// <example>"CoreApp-conf"</example>
        [Required]
        [JsonPropertyName("packageName")]
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current installed version of the package on the node.
        /// </summary>
        /// <example>"1.2.1"</example>
        [Required]
        [JsonPropertyName("currentVersion")]
        public string CurrentVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the package.
        /// </summary>
        /// <remarks>
        /// The type indicates its role or classification (e.g., Core, Optional).
        /// This maps to the shared <see cref="SiteKeeper.Shared.Enums.PackageType"/> enum.
        /// </remarks>
        /// <example>PackageType.Core</example>
        [Required]
        [JsonPropertyName("type")]
        public PackageType Type { get; set; }
    }
} 