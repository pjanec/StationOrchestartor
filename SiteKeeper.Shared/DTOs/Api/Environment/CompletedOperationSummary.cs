using SiteKeeper.Shared.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Environment
{
    /// <summary>
    /// Provides a summary of an asynchronous operation that has completed.
    /// </summary>
    /// <remarks>
    /// This DTO is used in API responses, such as the <see cref="EnvironmentStatusResponse"/>, to inform clients
    /// about the outcome of recently finished operations. It includes the operation's ID, name, final status
    /// (Success, Failure, Cancelled), completion time, and duration.
    /// Based on the CompletedOperationSummary schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class CompletedOperationSummary
    {
        /// <summary>
        /// Unique identifier for the completed operation.
        /// </summary>
        /// <example>"op-envbackup-x7y8z9w0"</example>
        [Required]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// User-friendly name of the operation.
        /// </summary>
        /// <example>"Environment Backup"</example>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Final status of the action (Success, Failure, Cancelled).
        /// </summary>
        [Required]
        public CompletedOperationFinalStatus Status { get; set; }

        /// <summary>
        /// Timestamp (UTC) when the operation completed or reached a terminal state.
        /// </summary>
        /// <example>"2023-10-26T11:00:00Z"</example>
        [Required]
        public DateTime CompletedAt { get; set; }

        /// <summary>
        /// Duration of the operation in seconds, if available and applicable.
        /// Null if the duration could not be determined.
        /// </summary>
        /// <example>1800</example>
        [Range(0, int.MaxValue)]
        public int? DurationSeconds { get; set; }
    }
} 