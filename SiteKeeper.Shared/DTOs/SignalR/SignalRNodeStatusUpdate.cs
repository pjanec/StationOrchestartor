using SiteKeeper.Shared.Enums;
using System;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.SignalR
{
    /// <summary>
    /// DTO for SignalR message notifying clients of a node status change.
    /// Corresponds to the 'NodeStatusChanged' server-to-client message.
    /// </summary>
    public class SignalRNodeStatusUpdate
    {
        /// <summary>
        /// The name of the node whose status has changed.
        /// </summary>
        [JsonPropertyName("nodeName")]
        public string NodeName { get; set; }

        /// <summary>
        /// The new API-facing status of the agent on the node.
        /// </summary>
        [JsonPropertyName("agentStatus")]
        public AgentStatus AgentStatus { get; set; }

        /// <summary>
        /// A summary of the node's health.
        /// </summary>
        [JsonPropertyName("healthSummary")]
        public NodeHealthSummary HealthSummary { get; set; }

        /// <summary>
        /// Current CPU utilization percentage.
        /// </summary>
        [JsonPropertyName("cpuUsagePercent")]
        public int? CpuUsagePercent { get; set; }

        /// <summary>
        /// Current RAM utilization percentage.
        /// </summary>
        [JsonPropertyName("ramUsagePercent")]
        public int? RamUsagePercent { get; set; }
    }
} 