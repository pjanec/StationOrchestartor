using SiteKeeper.Shared.Enums;
using System;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.AgentHub
{
    /// <summary>
    /// Represents a single, contextualized log entry originating from a slave agent during task execution.
    /// This DTO is sent from the slave's custom NLog target to the master's AgentHub.
    /// </summary>
    /// <remarks>
    /// As defined in "SiteKeeper - Task Related Log Flow and Handling.md".
    /// It contains all necessary context (OperationId, TaskId, NodeName) to allow the master
    /// to correlate the log message with the correct operation and persist it in the correct journal file.
    /// </remarks>
    public class SlaveTaskLogEntry
    {
        /// <summary>
        /// The unique ID of the overall operation this log entry belongs to.
        /// </summary>
        [JsonPropertyName("actionId")]
        public string ActionId { get; set; } = string.Empty;

        /// <summary>
        /// The unique ID of the specific task on the node this log entry belongs to.
        /// </summary>
        [JsonPropertyName("taskId")]
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// The name of the node (agent) where the log was generated.
        /// </summary>
        [JsonPropertyName("nodeName")]
        public string NodeName { get; set; } = string.Empty;

        /// <summary>
        /// The severity level of the log message.
        /// </summary>
        [JsonPropertyName("logLevel")]
        public LogLevel LogLevel { get; set; }

        /// <summary>
        /// The formatted log message.
        /// </summary>
        [JsonPropertyName("logMessage")]
        public string LogMessage { get; set; } = string.Empty;

        /// <summary>
        /// The UTC timestamp when the log event occurred.
        /// </summary>
        [JsonPropertyName("timestampUtc")]
        public DateTime TimestampUtc { get; set; }
    }
} 