using SiteKeeper.Shared.DTOs.API.Environment; // For JournalEntrySummary base class
using System.Collections.Generic;

namespace SiteKeeper.Shared.DTOs.API.Journal
{
    /// <summary>
    /// Represents detailed information for a single journal entry, extending the summary view.
    /// Aligns with the JournalEntry schema in `web api swagger.yaml`.
    /// </summary>
    /// <remarks>
    /// This DTO is used for API responses that provide full details for a specific journal entry,
    /// such as GET /journal/{journalRecordId}.
    /// It inherits from <see cref="JournalEntrySummary"/> and adds properties for duration,
    /// operation-specific structured details, and key log snippets.
    /// </remarks>
    public class JournalEntry : JournalEntrySummary
    {
        /// <summary>
        /// Gets or sets the duration of the recorded operation in seconds, if applicable.
        /// Corresponds to the 'durationSeconds' field in Swagger.
        /// </summary>
        /// <example>125</example>
        public int? DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets operation-specific structured details.
        /// The content of this object varies depending on the OperationType.
        /// Corresponds to the 'details' field in Swagger.
        /// </summary>
        /// <example>{"backupLocation": "/path/to/backup.zip", "filesBackedUp": 1024}</example>
        public object? Details { get; set; }

        /// <summary>
        /// Gets or sets a list of key log lines or snippets from the operation, if applicable.
        /// Corresponds to the 'logSnippets' field in Swagger.
        /// </summary>
        /// <example>["[INFO] Starting backup operation...", "[WARN] Skipped file X due to lock.", "[INFO] Backup completed successfully."]</example>
        public List<string>? LogSnippets { get; set; }
    }
} 