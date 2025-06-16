using System;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Environment
{
    /// <summary>
    /// Represents detailed information about a specific node, extending NodeSummary.
    /// As defined in swagger: #/components/schemas/NodeDetailsResponse
    /// </summary>
    public class NodeDetailsResponse : NodeSummary // Inherits properties from NodeSummary
    {
        /// <summary>
        /// Operating system information for the node.
        /// </summary>
        /// <example>"Windows Server 2019"</example>
        [JsonPropertyName("osInfo")]
        public string? OsInfo { get; set; }

        /// <summary>
        /// Version of the SiteKeeper agent running on the node.
        /// </summary>
        /// <example>"2.1.0"</example>
        [JsonPropertyName("agentVersion")]
        public string? AgentVersion { get; set; }

        /// <summary>
        /// Timestamp (UTC) when the agent on this node was last seen or reported a heartbeat.
        /// </summary>
        /// <example>"2025-05-29T23:50:00Z"</example>
        [JsonPropertyName("lastSeen")]
        public DateTime? LastSeen { get; set; }
    }
} 