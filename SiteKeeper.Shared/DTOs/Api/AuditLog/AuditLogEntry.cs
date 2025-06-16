using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SiteKeeper.Shared.Enums;

namespace SiteKeeper.Shared.DTOs.API.AuditLog
{
    /// <summary>
    /// Represents a single entry in the audit log, as exposed by the API.
    /// Corresponds to the 'AuditLogEntry' schema in `web api swagger.yaml`.
    /// </summary>
    public class AuditLogEntry
    {
        /// <summary>
        /// Unique identifier for the audit log entry.
        /// </summary>
        /// <example>"audit-001"</example>
        [Required]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp of when the audited event occurred (UTC).
        /// </summary>
        [Required]
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Username of the user who performed the action.
        /// </summary>
        /// <example>"op_user"</example>
        [Required]
        [JsonPropertyName("user")]
        public string User { get; set; } = string.Empty;

        /// <summary>
        /// Type of operation or action performed.
        /// </summary>
        /// <example>"System Software Stop"</example>
        [Required]
        [JsonPropertyName("operationType")]
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// The primary target of the operation, if applicable.
        /// </summary>
        /// <example>"Environment: MyProdEnv-SiteA"</example>
        [JsonPropertyName("target")]
        public string? Target { get; set; }

        /// <summary>
        /// JSON string of parameters used in the operation, if any.
        /// </summary>
        /// <example>"{\"force\": true}"</example>
        [JsonPropertyName("parameters")]
        public string? Parameters { get; set; } // JSON string as per Swagger

        /// <summary>
        /// Outcome of the operation.
        /// </summary>
        /// <example>"Success"</example>
        [Required]
        [JsonPropertyName("outcome")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AuditLogOutcome Outcome { get; set; } // Reusing existing AuditLogOutcome enum

        /// <summary>
        /// Additional details about the event or outcome.
        /// </summary>
        /// <example>"System software stop command issued."</example>
        [JsonPropertyName("details")]
        public string? Details { get; set; }
    }
} 