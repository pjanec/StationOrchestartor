using System;

namespace SiteKeeper.Shared.DTOs.SignalR
{
    /// <summary>
    /// Represents a single log entry related to an operation, sent to the UI via SignalR.
    /// </summary>
    public class OperationLogEntryDto
    {
        /// <summary>
        /// The ID of the operation this log entry belongs to.
        /// </summary>
        public string OperationId { get; set; }

        /// <summary>
        /// The timestamp of the log entry.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// The severity level of the log (e.g., "Info", "Warn", "Error").
        /// </summary>
        public string Level { get; set; }

        /// <summary>
        /// The formatted log message.
        /// </summary>
        public string Message { get; set; }
    }
} 