using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.AgentHub
{
    /// <summary>
    /// DTO representing state information and expectations from the Master, sent to a Slave Agent.
    /// </summary>
    /// <remarks>
    /// This message might be sent periodically or upon agent connection to ensure the agent
    /// has the latest context from the Master. It can include the Master's current time,
    /// the expected status of the agent, information about the active manifest, and a list
    /// of operations that might be relevant to or involve this agent.
    /// Corresponds to the `MasterStateForAgent` schema in `web api swagger.yaml`.
    /// </remarks>
    public class MasterStateForAgent
    {
        /// <summary>
        /// The Master server's current timestamp (UTC). Can be used by the agent for time synchronization checks.
        /// </summary>
        /// <example>"2023-10-27T12:00:00Z"</example>
        [Required]
        public DateTime MasterTimestamp { get; set; }

        /// <summary>
        /// The status the Master expects this agent to be in (e.g., Online, MaintenanceMode).
        /// </summary>
        /// <example>AgentStatus.Online</example>
        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AgentStatus ExpectedAgentStatus { get; set; }

        /// <summary>
        /// Optional. The identifier of the currently active environment manifest in the Master.
        /// This can help the agent understand the target configuration for its node.
        /// </summary>
        /// <example>"manifest-prod-v1.2.5"</example>
        public string? ActiveManifestId { get; set; }

        /// <summary>
        /// Optional. A list of brief details about operations that are currently assigned to,
        /// or might affect, this agent or its node. This provides context to the agent
        /// about ongoing activities.
        /// </summary>
        public List<BriefOperationInfo>? AssignedOrRelevantOperations { get; set; }

        /// <summary>
        /// Optional. The current version of the Master Agent software.
        /// Can be used by the slave for compatibility checks or information.
        /// </summary>
        /// <example>"1.0.25"</example>
        public string? MasterVersion { get; set; }

        /// <summary>
        /// Optional. If true, instructs the Slave Agent to perform a full re-registration sequence with the Master,
        /// even if it believes it is already registered. This can be used to force a refresh of agent details
        /// or to recover from potential synchronization issues.
        /// Defaults to false.
        /// </summary>
        /// <example>false</example>
        public bool ForceAgentReregistration { get; set; } = false;
    }
} 