using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Specifies the reason why the Master service is shutting down.
    /// Used in SignalR notifications to clients.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MasterGoingDownReason
    {
        /// <summary>
        /// The reason for shutdown is unknown or not specified.
        /// </summary>
        Unknown,

        /// <summary>
        /// The Master service is shutting down for planned maintenance.
        /// </summary>
        PlannedMaintenance,

        /// <summary>
        /// The Master service is shutting down due to an unplanned event or issue.
        /// </summary>
        UnplannedShutdown,

        /// <summary>
        /// The Master service is shutting down due to a critical error.
        /// </summary>
        CriticalError,

        /// <summary>
        /// The Master service is shutting down as part of an update or new deployment.
        /// </summary>
        UpdateDeployment
    }
} 