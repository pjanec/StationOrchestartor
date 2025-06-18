using SiteKeeper.Shared.Enums;
using System;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.SignalR
{
    /// <summary>
    /// DTO for SignalR messages notifying GUI clients of a node's status change, including agent connectivity, health, and basic resource utilization.
    /// Corresponds to the 'SignalRNodeStatusUpdate' schema in `web api swagger.yaml`.
    /// </summary>
    /// <remarks>
    /// This DTO is sent by the Master Hub to connected GUI clients via the <see cref="Abstractions.GuiHub.IGuiHub.ReceiveNodeStatusUpdate"/> method.
    /// It enables real-time monitoring of individual node states in the user interface.
    /// </remarks>
    public class SignalRNodeStatusUpdate
    {
        /// <summary>
        /// Gets or sets the name of the node whose status has changed.
        /// </summary>
        /// <example>"AppServer01"</example>
        [JsonPropertyName("nodeName")]
        public string NodeName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the new connectivity status of the SiteKeeper agent on the node.
        /// </summary>
        /// <example>AgentStatus.Online</example>
        [JsonPropertyName("agentStatus")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AgentStatus AgentStatus { get; set; }

        /// <summary>
        /// Gets or sets a summary of the node's overall health.
        /// </summary>
        /// <example>NodeHealthSummary.Healthy</example>
        [JsonPropertyName("healthSummary")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public NodeHealthSummary HealthSummary { get; set; }

        /// <summary>
        /// Gets or sets the current CPU utilization percentage on the node.
        /// This may be null if the agent is offline or the data is not available.
        /// </summary>
        /// <example>35</example>
        [JsonPropertyName("cpuUsagePercent")]
        public int? CpuUsagePercent { get; set; }

        /// <summary>
        /// Gets or sets the current RAM utilization percentage on the node.
        /// This may be null if the agent is offline or the data is not available.
        /// </summary>
        /// <example>60</example>
        [JsonPropertyName("ramUsagePercent")]
        public int? RamUsagePercent { get; set; }
    }
} 