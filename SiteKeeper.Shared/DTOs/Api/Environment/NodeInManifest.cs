using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Environment
{
    /// <summary>
    /// Represents a node and its list of expected packages within the environment manifest.
    /// As defined in swagger: #/components/schemas/NodeInManifest
    /// </summary>
    public class NodeInManifest
    {
        /// <summary>
        /// The name of the node.
        /// </summary>
        /// <example>"SIMSERVER"</example>
        [JsonPropertyName("nodeName")]
        public string NodeName { get; set; } = string.Empty;

        /// <summary>
        /// The list of packages that are expected to be on this node according to the manifest.
        /// </summary>
        [JsonPropertyName("packages")]
        public List<PackageInManifest> Packages { get; set; } = new();
    }
} 