using SiteKeeper.Shared.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Diagnostics
{
    /// <summary>
    /// Represents a single issue identified by a health check procedure.
    /// As defined in swagger: #/components/schemas/HealthCheckIssue
    /// </summary>
    /// <remarks>
    /// This DTO is used to detail a specific problem found during diagnostics, including its name, severity,
    /// a description of the issue, and potentially a suggested remediation or affected component.
    /// Based on the HealthCheckIssue schema in `web api swagger.yaml`.
    /// </remarks>
    public class HealthCheckIssue
    {
        /// <summary>
        /// The severity of the identified issue.
        /// </summary>
        /// <example>HealthCheckSeverity.Error</example>
        [Required]
        [JsonPropertyName("severity")]
        public HealthCheckSeverity Severity { get; set; }

        /// <summary>
        /// Timestamp when the issue was found.
        /// </summary>
        /// <example>"2025-05-30T10:05:00Z"</example>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Node name or App ID where issue was found.
        /// </summary>
        /// <example>"IOS1"</example>
        [Required]
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Name of the health check that found the issue.
        /// </summary>
        /// <example>"Disk Space Check"</example>
        [JsonPropertyName("checkName")]
        public string? CheckName { get; set; }

        /// <summary>
        /// A human-readable description of the issue found.
        /// </summary>
        /// <example>"Critical disk space low on C: (95% used)."</example>
        [Required]
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Optional suggested remediation steps for resolving the issue.
        /// </summary>
        /// <example>"Free up disk space immediately."</example>
        [JsonPropertyName("recommendedAction")]
        public string? RecommendedAction { get; set; }
    }
} 