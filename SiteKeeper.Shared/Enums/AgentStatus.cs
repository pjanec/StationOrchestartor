using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Represents the connectivity status of a SiteKeeper Slave Agent on a managed node, as perceived by the Master Agent.
    /// </summary>
    /// <remarks>
    /// This enum is used in various DTOs like <c>NodeSummary</c> and <c>SignalRNodeStatusUpdate</c> to report
    /// whether a slave agent is currently communicating with the master.
    /// It is related to the <c>AgentConnectivityStatus</c> enum used internally by the Master Agent's
    /// <c>NodeHealthMonitorService</c> (see "SiteKeeper - Master - Data Structures.md"), though this API-facing
    /// enum is simpler.
    /// Serialization to string is handled by <see cref="JsonStringEnumConverter"/> as per guidelines in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AgentStatus
    {
        /// <summary>
        /// The Slave Agent is connected to the Master Agent and is actively sending heartbeats.
        /// </summary>
        Online,

        /// <summary>
        /// The Slave Agent is not currently connected to the Master Agent, or heartbeats have been missed.
        /// </summary>
        Offline,

        /// <summary>
        /// The Slave Agent's status is indeterminate. This might be a transient state during connection
        /// or if communication is unstable but not yet fully timed out.
        /// </summary>
        Unknown
    }
} 