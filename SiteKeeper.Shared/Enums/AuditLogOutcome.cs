using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Indicates the outcome of an audited action or operation.
    /// </summary>
    /// <remarks>
    /// This enumeration is used in DTOs such as <see cref="SiteKeeper.Shared.DTOs.API.AuditLog.AuditLogEntry"/>
    /// to record the result of an event that has been logged for auditing purposes.
    /// It is typically serialized as a string in API responses.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AuditLogOutcome
    {
        /// <summary>
        /// The audited action or operation completed successfully.
        /// </summary>
        Success,

        /// <summary>
        /// The audited action or operation failed.
        /// </summary>
        Failure,

        /// <summary>
        /// The audited action or operation was partially successful. Some aspects may have succeeded while others failed.
        /// </summary>
        PartialSuccess,

        /// <summary>
        /// The outcome of the audited action or operation is unknown or could not be determined at the time of logging.
        /// </summary>
        Unknown
    }
} 