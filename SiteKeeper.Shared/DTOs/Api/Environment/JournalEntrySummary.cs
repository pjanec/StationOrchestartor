using SiteKeeper.Shared.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Environment
{
    /// <summary>
    /// Provides a summary of a single journal entry, representing a recorded event or operation.
    /// Aligns with the JournalEntrySummary schema in `web api swagger.yaml`.
    /// </summary>
    /// <remarks>
    /// This DTO is used in API responses that list historical journal entries (e.g., GET /journal).
    /// It includes the entry's unique ID, timestamp, the type of operation, a brief summary, and the outcome.
    /// </remarks>
    public class JournalEntrySummary
    {
        /// <summary>
        /// Gets or sets the unique identifier for the journal entry.
        /// Corresponds to the 'journalRecordId' field in Swagger.
        /// </summary>
        /// <example>"journal-001"</example>
        [Required]
        public string JournalRecordId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp (UTC) when the event or operation was recorded in the journal.
        /// Corresponds to the 'timestamp' field in Swagger (format: date-time).
        /// </summary>
        /// <example>"2025-05-29T18:30:00Z"</example>
        [Required]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the type of operation that this journal entry pertains to (e.g., "Environment Backup").
        /// Corresponds to the 'operationType' field in Swagger.
        /// </summary>
        /// <example>"Environment Backup"</example>
        [Required]
        public string? OperationType { get; set; }

        /// <summary>
        /// Gets or sets a brief, human-readable summary or description of the journaled event or operation.
        /// Corresponds to the 'summary' field in Swagger.
        /// </summary>
        /// <example>"Backup of MyProdEnv-SiteA completed."</example>
        [Required]
        public string? Summary { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the outcome of the operation (e.g., "Success", "Failure", "InProgress").
        /// Corresponds to the 'outcome' field in Swagger and its defined enum values.
        /// </summary>
        /// <example>"Success"</example>
        [Required]
        public string? Outcome { get; set; }
    }
} 