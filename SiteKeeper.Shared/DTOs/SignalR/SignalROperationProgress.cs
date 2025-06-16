using SiteKeeper.Shared.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.SignalR
{
    /// <summary>
    /// DTO for sending real-time progress updates for an ongoing operation via SignalR.
    /// As defined in swagger: #/components/schemas/SignalROperationProgress
    /// </summary>
    /// <remarks>
    /// The Master Agent's <c>GuiHub</c> sends messages of this type to clients to provide granular updates
    /// on an operation's progress, including percentage completion, current status, and log messages.
    /// This is distinct from <see cref="SignalROperationCompleted"/> which signals the final outcome.
    /// Based on the SignalROperationProgress schema in `web api swagger.yaml`.
    /// </remarks>
    public class SignalROperationProgress
    {
        /// <summary>
        /// The unique identifier of the operation.
        /// </summary>
        /// <example>"op-envupdate-abc123"</example>
        [Required]
        [JsonPropertyName("operationId")]
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// The current progress status of the operation (e.g., InProgress, Pending, Retrying).
        /// </summary>
        [Required]
        [JsonPropertyName("status")]
        public SignalROperationProgressStatus Status { get; set; }

        /// <summary>
        /// Current progress percentage (0-100) of the overall operation. Null if not applicable.
        /// </summary>
        /// <example>50</example>
        [JsonPropertyName("progressPercent")]
        public int? ProgressPercent { get; set; }

        /// <summary>
        /// A log message or status update text related to the current progress point.
        /// </summary>
        /// <example>"[INFO] Applying package CoreApp-conf on IOS1..."</example>
        [JsonPropertyName("logMessage")]
        public string? Message { get; set; }
    }
} 