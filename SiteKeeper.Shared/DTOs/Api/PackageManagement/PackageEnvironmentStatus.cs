using System.Collections.Generic;
using System.Text.Json.Serialization;
using SiteKeeper.Shared.Enums; // For PackageType enum

namespace SiteKeeper.Shared.DTOs.API.PackageManagement
{
    /// <summary>
    /// Represents the status of a package across the environment, detailing its version on each node.
    /// As defined in swagger: #/components/schemas/PackageEnvironmentStatus
    /// </summary>
    public class PackageEnvironmentStatus
    {
        /// <summary>
        /// The name of the package.
        /// </summary>
        /// <example>"CoreApp-conf"</example>
        [JsonPropertyName("packageName")]
        public string PackageName { get; set; }

        /// <summary>
        /// The type of the package.
        /// </summary>
        /// <example>PackageType.Core</example>
        [JsonPropertyName("type")]
        public PackageType Type { get; set; }

        /// <summary>
        /// List of nodes and the version of this package currently on them.
        /// </summary>
        [JsonPropertyName("nodes")]
        public List<NodePackageVersionStatus> Nodes { get; set; }

        public PackageEnvironmentStatus()
        {
            Nodes = new List<NodePackageVersionStatus>();
        }
    }

    /// <summary>
    /// Represents the version and status of a specific package on a particular node.
    /// This is a helper class for PackageEnvironmentStatus.
    /// </summary>
    public class NodePackageVersionStatus
    {
        /// <summary>
        /// Name of the node.
        /// </summary>
        /// <example>"IOS1"</example>
        [JsonPropertyName("nodeName")]
        public string NodeName { get; set; }

        /// <summary>
        /// Current version of the package on this node. Null if not installed or version unknown.
        /// </summary>
        /// <example>"1.2.1"</example>
        [JsonPropertyName("currentVersion")]
        public string? CurrentVersion { get; set; }
    }
} 