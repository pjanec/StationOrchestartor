using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Represents the operational status of an individual application or software component managed by SiteKeeper.
    /// </summary>
    /// <remarks>
    /// This enum is used in DTOs like <c>AppStatusInfo</c> and <c>SignalRAppStatusUpdate</c> to convey the current state
    /// of a specific application on a node. It helps in monitoring and controlling individual software components.
    /// Serialization to string is handled by <see cref="JsonStringEnumConverter"/> as per guidelines in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AppOperationalStatus
    {
        /// <summary>
        /// The application is confirmed to be running and operational.
        /// </summary>
        Running,

        /// <summary>
        /// The application is confirmed to be stopped (either intentionally or unintentionally).
        /// </summary>
        Stopped,

        /// <summary>
        /// The application is in an error state or has failed to run correctly.
        /// </summary>
        Error,

        /// <summary>
        /// The application is in the process of starting up.
        /// </summary>
        Starting,

        /// <summary>
        /// The application is in the process of shutting down.
        /// </summary>
        Stopping,

        /// <summary>
        /// The operational status of the application cannot be determined.
        /// </summary>
        Unknown
    }
} 