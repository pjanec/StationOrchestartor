using SiteKeeper.Shared.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Environment
{
    /// <summary>
    /// Provides a summary of an asynchronous operation that is currently ongoing or pending.
    /// </summary>
    /// <remarks>
    /// This DTO is used in API responses, such as the <see cref="EnvironmentStatusResponse"/>, to inform clients
    /// about active operations within the SiteKeeper system. It includes the operation's ID, name, current status,
    /// progress percentage, start time, and a recent log snippet if available.
    /// Based on the OngoingOperationSummary schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class OngoingOperationSummary
    {
        /// <summary>
        /// Unique identifier for the ongoing operation.
        /// </summary>
        /// <example>"op-envverify-b3a4c1d2"</example>
        [Required]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// User-friendly name of the operation (e.g., "Environment Verification", "Software Update").
        /// </summary>
        /// <example>"Environment Verification"</example>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Current status of the operation (e.g., InProgress, Pending, Cancelling).
        /// </summary>
        /// <example>OngoingOperationStatus.InProgress</example>
        [Required]
        public OngoingOperationStatus Status { get; set; }

        /// <summary>
        /// Current progress percentage (0-100) of the operation, if applicable.
        /// Null if progress is not tracked or not applicable to this operation type or state.
        /// </summary>
        /// <example>75</example>
        [Range(0, 100)]
        public int? ProgressPercent { get; set; }

        /// <summary>
        /// Timestamp (UTC) when the operation was started.
        /// </summary>
        /// <example>"2023-10-26T10:30:00Z"</example>
        [Required]
        public DateTime StartTime { get; set; }

        /// <summary>
        /// A recent log snippet or status message from the operation, if available.
        /// This provides a quick glimpse into the operation's current activity.
        /// </summary>
        /// <example>"Node 'Slave01': Task 'VerifyPackages' started."</example>
        public string? LatestLogSnippet { get; set; }
    }
} 