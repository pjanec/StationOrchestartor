using SiteKeeper.Shared.Enums;
using System;

namespace SiteKeeper.Shared.DTOs.SignalR
{
    /// <summary>
    /// Represents a log entry related to an operation, sent to GUI clients via SignalR.
    /// </summary>
    public class SignalROperationLogEntry
    {
        /// <summary>
        /// The ID of the operation this log entry belongs to.
        /// </summary>
        public string OperationId { get; set; }

        /// <summary>
        /// The ID of the task this log entry belongs to.
        /// </summary>
        public string TaskId { get; set; }

        /// <summary>
        /// The name of the node that generated the log.
        /// </summary>
        public string NodeName { get; set; }

        /// <summary>
        /// The timestamp of the log entry.
        /// </summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>
        /// The log level.
        /// </summary>
        public LogLevel LogLevel { get; set; }

        /// <summary>
        /// The log message.
        /// </summary>
        public string Message { get; set; }
    }
} 