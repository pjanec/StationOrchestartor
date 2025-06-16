using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Diagnostics
{
    /// <summary>
    /// Represents a request to run specific health checks on specified nodes.
    /// Corresponds to the 'RunHealthChecksRequest' schema in `web api swagger.yaml`.
    /// </summary>
    /// <remarks>
    /// This DTO is used to initiate a diagnostic operation. If <see cref="CheckIds"/> is empty or null,
    /// it might imply running all default or pre-configured checks. Similarly for <see cref="NodeNames"/> and <see cref="AllNodes"/>
    /// determining the target nodes.
    /// </remarks>
    public class RunHealthChecksRequest
    {
        /// <summary>
        /// Specific check IDs to run. If empty or null, the behavior might be to run all default checks.
        /// </summary>
        /// <example>["disk.space.critical", "service.status.core"]</example>
        [JsonPropertyName("checkIds")]
        public List<string>? CheckIds { get; set; }

        /// <summary>
        /// Specific nodes to run checks on. If empty or null and <see cref="AllNodes"/> is false, 
        /// the behavior might be to run on all applicable nodes or a default set.
        /// </summary>
        /// <example>["IOS1"]</example>
        [JsonPropertyName("nodeNames")]
        public List<string>? NodeNames { get; set; }

        /// <summary>
        /// If true, runs checks on all applicable nodes, potentially overriding <see cref="NodeNames"/>.
        /// </summary>
        /// <example>false</example>
        [JsonPropertyName("allNodes")]
        public bool? AllNodes { get; set; } // Nullable to represent not specified, service can default

        // Helper method for API endpoint if needed (already in ApiEndpoints.cs)
        // public Dictionary<string, object> ToDictionary()
        // {
        //     var dict = new Dictionary<string, object>();
        //     if (CheckIds != null) dict["checkIds"] = CheckIds;
        //     if (NodeNames != null) dict["nodeNames"] = NodeNames;
        //     if (AllNodes.HasValue) dict["allNodes"] = AllNodes.Value;
        //     return dict;
        // }
    }
} 