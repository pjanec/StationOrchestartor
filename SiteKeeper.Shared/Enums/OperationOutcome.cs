using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Represents the outcome of a journaled or audited operation.
    /// </summary>
    /// <remarks>
    /// This enum is used in DTOs such as <c>JournalEntrySummary</c> and <c>AuditLogEntry</c> to record the result
    /// of various actions performed within the SiteKeeper system.
    /// It provides a more granular outcome than <see cref="CompletedOperationFinalStatus"/>, for example,
    /// by including an "InProgress" state for journal entries of operations that might not have completed
    /// if the master restarted, and "PartialSuccess" for audit logs where an operation might have mixed results.
    /// Serialization to string is handled by <see cref="JsonStringEnumConverter"/> as per guidelines in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OperationOutcome
    {
        /// <summary>
        /// The operation completed successfully.
        /// </summary>
        Success,

        /// <summary>
        /// The operation failed.
        /// </summary>
        Failure,

        /// <summary>
        /// The operation was still in progress when this journal entry was made or the audit event occurred.
        /// This is particularly relevant for journal entries that might be the last record before a system restart.
        /// </summary>
        InProgress,

        /// <summary>
        /// The operation was cancelled.
        /// </summary>
        Cancelled,

        /// <summary>
        /// The outcome of the operation is unknown or could not be determined.
        /// </summary>
        Unknown,

        /// <summary>
        /// The operation achieved some of its objectives but not all, or had mixed results.
        /// This is primarily intended for <c>AuditLogEntry</c> where an action might affect multiple targets with varying success.
        /// </summary>
        PartialSuccess
    }
} 