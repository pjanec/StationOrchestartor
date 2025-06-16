using System;
using System.Text.Json.Serialization;
using SiteKeeper.Shared.Enums; // For LogLevel if it will be an enum

namespace SiteKeeper.Shared.DTOs.API.Operations
{
    /// <summary>
    /// Represents a single entry in the operation journal.
    /// Aligns with the OperationJournalEntry schema in `web api swagger.yaml`.
    /// </summary>
    public class OperationJournalEntry
    {
        /// <summary>
        /// Timestamp of the journal entry.
        /// </summary>
        /// <example>"2023-10-27T10:30:00Z"</example>
        [JsonPropertyName("timestamp")]
        [JsonRequired]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Identifier of the operation, if applicable.
        /// </summary>
        /// <example>"op-abc123xyz"</example>
        [JsonPropertyName("operationId")]
        public string? OperationId { get; set; }

        /// <summary>
        /// Identifier of the task, if applicable.
        /// </summary>
        /// <example>"op-abc123xyz-node01-1"</example>
        [JsonPropertyName("taskId")]
        public string? TaskId { get; set; }

        /// <summary>
        /// Name of the node involved, if applicable.
        /// </summary>
        /// <example>"WorkerNode01"</example>
        [JsonPropertyName("nodeName")]
        public string? NodeName { get; set; }

        /// <summary>
        /// Type of the event.
        /// </summary>
        /// <example>"TaskStatusChanged"</example>
        [JsonPropertyName("eventType")]
        [JsonRequired]
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// Main message of the journal entry.
        /// </summary>
        /// <example>"Task status changed to InProgress.
        [JsonPropertyName("message")]
        [JsonRequired]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Optional structured details about the event.
        /// </summary>
        [JsonPropertyName("details")]
        public object? Details { get; set; }

        /// <summary>
        /// Log level of the entry (e.g., Information, Warning, Error).
        /// Corresponds to LogLevel enum (Information, Warning, Error, Critical) in Swagger.
        /// </summary>
        /// <example>"Information"</example>
        [JsonPropertyName("logLevel")]
        [JsonConverter(typeof(JsonStringEnumConverter))] // Assuming LogLevel is an enum
        public LogLevel? LogLevel { get; set; } // Define LogLevel enum if not already present and matching swagger
    }
} 