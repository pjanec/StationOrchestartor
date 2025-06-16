using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.AgentHub
{
    /// <summary>
    /// DTO used by the Master to send a general command to a Slave Agent.
    /// </summary>
    /// <remarks>
    /// This message allows the Master to invoke ad-hoc commands on an agent, outside the context
    /// of a formal, trackable operation task. Examples include ping, request for detailed status,
    /// or triggering a specific diagnostic routine on the node.
    /// Corresponds to the `NodeGeneralCommandRequest` schema in `web api swagger.yaml`.
    /// The specific `CommandType` strings and expected `CommandPayload` structures should be
    /// agreed upon between Master and Slave implementations.
    /// </remarks>
    public class NodeGeneralCommandRequest
    {
        /// <summary>
        /// A string identifying the type of command to be executed by the agent.
        /// </summary>
        /// <example>"Ping" or "GetDetailedStatus" or "RestartComponent"</example>
        [Required]
        public string CommandType { get; set; } = string.Empty;

        /// <summary>
        /// Optional. A payload containing parameters or data required for the command.
        /// The structure of this object depends on the <see cref="CommandType"/>.
        /// For example, for "RestartComponent", it might include {"componentName": "LoggingService"}.
        /// </summary>
        /// <example>{"componentName": "LoggingService"}</example>
        public Dictionary<string, object>? CommandPayload { get; set; }

        /// <summary>
        /// Optional. Specifies the maximum time in seconds the agent should allow for this command to complete.
        /// If the command exceeds this timeout, the agent may report a timeout or failure.
        /// If not provided, a default timeout might apply.
        /// </summary>
        /// <example>60</example>
        public int? CommandTimeoutSeconds { get; set; }
    }
} 