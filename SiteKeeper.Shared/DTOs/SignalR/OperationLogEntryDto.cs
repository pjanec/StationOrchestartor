using System;

namespace SiteKeeper.Shared.DTOs.SignalR
{
    /// <summary>
    /// Represents a single log entry related to an ongoing operation, intended for real-time display in a user interface.
    /// </summary>
    /// <remarks>
    /// This DTO is typically sent by the Master Hub to connected GUI clients via SignalR, specifically using the
    /// <see cref="Abstractions.GuiHub.IGuiHub.ReceiveOperationLogEntry"/> method.
    /// It provides a simplified view of a log event for immediate operational awareness.
    /// </remarks>
    public class OperationLogEntryDto
    {
        /// <summary>
        /// Gets or sets the unique identifier of the operation this log entry belongs to.
        /// </summary>
        /// <example>"op-deploy-webapp-123"</example>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp (preferably UTC) when the log event occurred.
        /// </summary>
        /// <example>"2023-10-27T10:30:05Z"</example>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the severity level of the log entry.
        /// Common values include "Debug", "Information", "Warning", "Error", "Critical".
        /// </summary>
        /// <example>"Information"</example>
        public string Level { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the formatted log message.
        /// </summary>
        /// <example>"Deployment step 'CopyFiles' started on node 'AppServer01'."</example>
        public string Message { get; set; } = string.Empty;
    }
} 