using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Represents the overall status of the managed software suite in the environment.
    /// </summary>
    /// <remarks>
    /// This enum provides a high-level overview of the state of all managed software components collectively.
    /// It is used in dashboard displays and for global system health assessments.
    /// Serialization to string is handled by <see cref="JsonStringEnumConverter"/> as per guidelines in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SystemSoftwareOverallStatus
    {
        /// <summary>
        /// All essential software components are running correctly.
        /// </summary>
        Running,

        /// <summary>
        /// All essential software components are intentionally stopped.
        /// </summary>
        Stopped,

        /// <summary>
        /// Some components are running, while others are stopped or in an error state.
        /// </summary>
        PartiallyRunning,

        /// <summary>
        /// The software suite is in the process of starting up.
        /// </summary>
        Initializing,

        /// <summary>
        /// The software suite is in the process of shutting down.
        /// </summary>
        Stopping,

        /// <summary>
        /// The software suite is in a transitional state towards an error or failure.
        /// </summary>
        Failing,

        /// <summary>
        /// One or more critical components have encountered an error, or the suite cannot operate as intended.
        /// </summary>
        Error,

        /// <summary>
        /// The status of the software suite cannot be determined.
        /// </summary>
        Unknown
    }
} 