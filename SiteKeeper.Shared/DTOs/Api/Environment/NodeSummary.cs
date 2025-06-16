using SiteKeeper.Shared.DTOs.API.Nodes;
using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Environment
{
    /// <summary>
    /// Provides a summary of the status and details for a single managed node.
    /// As defined in swagger: #/components/schemas/NodeSummary
    /// </summary>
    /// <remarks>
    /// This DTO is used in API responses that list nodes (e.g., GET /environment/nodes).
    /// It includes the node's name, its role in the environment, agent connectivity, health summary,
    /// and resource utilization.
    /// Based on the NodeSummary schema in `web api swagger.yaml`.
    /// </remarks>
    public class NodeSummary
    {
        /// <summary>
        /// SiteKeeper alias for the node.
        /// </summary>
        /// <example>"SIMSERVER"</example>
        [Required]
        [JsonPropertyName("name")]
        public string NodeName { get; set; } = string.Empty;

        /// <summary>
        /// The IP address of the node.
        /// </summary>
        /// <example>"192.168.0.110"</example>
        [JsonPropertyName("ipAddress")]
        public string? IpAddress { get; set; }

        /// <summary>
        /// Unique numeric identifier for the node.
        /// </summary>
        /// <example>"001"</example>
        [JsonPropertyName("stationNumber")]
        public string? StationNumber { get; set; }

        /// <summary>
        /// Connectivity status of the SiteKeeper Slave Agent on this node.
        /// </summary>
        [Required]
        [JsonPropertyName("agentStatus")]
        public AgentStatus AgentStatus { get; set; }

        /// <summary>
        /// Overall health summary of the node.
        /// </summary>
        [Required]
        [JsonPropertyName("healthSummary")]
        public NodeHealthSummary HealthSummary { get; set; }

        /// <summary>
        /// Indicates if this is the master node.
        /// </summary>
        /// <example>true</example>
        [JsonPropertyName("isMaster")]
        public bool IsMaster { get; set; }

        /// <summary>
        /// Current CPU utilization percentage on the node, if available.
        /// Null if not reported or agent is offline.
        /// </summary>
        /// <example>15</example>
        [JsonPropertyName("cpuUsagePercent")]
        public int? CpuUsagePercent { get; set; }

        /// <summary>
        /// Current RAM utilization percentage on the node, if available.
        /// Null if not reported or agent is offline.
        /// </summary>
        /// <example>30</example>
        [JsonPropertyName("ramUsagePercent")]
        public int? RamUsagePercent { get; set; }
    }
} 