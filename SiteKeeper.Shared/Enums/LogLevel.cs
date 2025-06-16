using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Defines the severity levels for log entries, aligning with standard logging practices.
    /// Used in OperationJournalEntry and potentially other logging DTOs.
    /// </summary>
    /// <remarks>
    /// Based on the LogLevel enum string values (Information, Warning, Error, Critical) defined in `web api swagger.yaml`.
    /// Serialization to string is handled by <see cref="JsonStringEnumConverter"/>.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LogLevel
    {
        /// <summary>
        /// Informational messages that highlight the progress of the application at coarse-grained level.
        /// </summary>
        Information,

        /// <summary>
        /// Potentially harmful situations or unexpected behavior that is not critical but should be noted.
        /// </summary>
        Warning,

        /// <summary>
        /// Errors that occurred during processing but the application might still be able to continue.
        /// </summary>
        Error,

        /// <summary>
        /// Severe errors that will presumably lead the application to abort.
        /// </summary>
        Critical
    }
} 