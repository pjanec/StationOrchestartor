using SiteKeeper.Shared.Enums;
using System;

namespace SiteKeeper.Shared.DTOs.SignalR
{
using System.Text.Json.Serialization; // Required for JsonPropertyName and JsonConverter
using System.ComponentModel.DataAnnotations; // Required for Required attribute

namespace SiteKeeper.Shared.DTOs.SignalR
{
    /// <summary>
    /// Represents a detailed log entry related to a specific task within an operation, sent to GUI clients via SignalR for real-time monitoring.
    /// Corresponds to the 'SignalROperationLogEntry' schema in `web api swagger.yaml`.
    /// </summary>
    /// <remarks>
    /// This DTO is sent by the Master Hub to connected GUI clients via the
    /// <see cref="Abstractions.GuiHub.IGuiHub.ReceiveOperationLogEntry"/> method.
    /// It provides contextual information including Operation ID, Task ID, and Node Name, along with the log message.
    /// </remarks>
    public class SignalROperationLogEntry
    {
        /// <summary>
        /// Gets or sets the unique identifier of the operation this log entry belongs to.
        /// </summary>
        /// <example>"op-deploy-webapp-123"</example>
        [Required]
        [JsonPropertyName("operationId")]
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique identifier of the specific task within the operation that this log entry pertains to.
        /// </summary>
        /// <example>"task-nodeA-deploy-pkgX"</example>
        [Required]
        [JsonPropertyName("taskId")]
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the node (agent) from which the log entry originated.
        /// </summary>
        /// <example>"AppServer01"</example>
        [Required]
        [JsonPropertyName("nodeName")]
        public string NodeName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp (UTC) when the log event occurred.
        /// </summary>
        /// <example>"2023-10-27T10:30:05Z"</example>
        [JsonPropertyName("timestampUtc")]
        public DateTime TimestampUtc { get; set; }

        /// <summary>
        /// Gets or sets the severity level of the log entry.
        /// </summary>
        /// <example>LogLevel.Information</example>
        [JsonPropertyName("logLevel")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LogLevel LogLevel { get; set; }

        /// <summary>
        /// Gets or sets the formatted log message.
        /// </summary>
        /// <example>"Package 'MyWebApp.zip' downloaded successfully."</example>
        [Required]
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
} 