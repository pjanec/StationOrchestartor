using SiteKeeper.Shared.DTOs.API.AuditLog; // Now reuses AuditLogEntry

namespace SiteKeeper.Shared.DTOs.SignalR
{
    /// <summary>
    /// DTO for sending a notification via SignalR when a new audit log entry has been added.
    /// </summary>
    /// <remarks>
    /// The Master Agent's <c>GuiHub</c> would send messages of this type to clients
    /// to inform them in real-time about new audit events.
    /// This DTO directly uses or wraps <see cref="API.AuditLog.AuditLogEntry"/>.
    /// For simplicity, this DTO can be a direct wrapper or alias if no additional SignalR-specific fields are needed.
    /// Based on requirements from "SiteKeeper Minimal API & SignalR Hub Handlers.md".
    /// </remarks>
    public class SignalRAuditLogEntryAdded : AuditLogEntry
    {
        // This class inherits all properties from AuditLogEntry.
        // No additional properties are specified for this SignalR message, 
        // so inheritance is used for direct reuse.
        // If SignalR message needed specific fields beyond what AuditLogEntry provides,
        // they would be added here, or a composition approach would be used.

        /// <summary>
        /// Constructor to allow easy creation from an AuditLogEntry.
        /// </summary>
        /// <param name="entry">The audit log entry that was added.</param>
        public SignalRAuditLogEntryAdded(AuditLogEntry entry)
        {
            Id = entry.Id;
            Timestamp = entry.Timestamp;
            User = entry.User;
            OperationType = entry.OperationType;
            Target = entry.Target;
            Parameters = entry.Parameters;
            Outcome = entry.Outcome;
            Details = entry.Details;
        }

        /// <summary>
        /// Default constructor for serialization purposes and to satisfy inheritance.
        /// </summary>
        public SignalRAuditLogEntryAdded() : base() { }
    }
} 