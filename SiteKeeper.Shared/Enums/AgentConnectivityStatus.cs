using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Represents the detailed internal connectivity status of a Slave Agent, 
    /// as tracked by the Master Agent's NodeHealthMonitorService.
    /// </summary>
    /// <remarks>
    /// This enum provides a more granular status than the API-facing <see cref="AgentStatus"/>.
    /// It includes states like Unreachable (missed heartbeats but not yet fully offline) and 
    /// NeverConnected for nodes that have been configured but never made initial contact.
    /// See "SiteKeeper - Master - Data Structures.md" (CachedNodeState.ConnectivityStatus).
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))] // Though primarily internal, converter is good practice
    public enum AgentConnectivityStatus
    {
        /// <summary>
        /// The agent has never successfully connected or sent a heartbeat.
        /// Initial state for a newly configured node's cached state before any contact.
        /// </summary>
        NeverConnected,

        /// <summary>
        /// The agent is actively connected and sending heartbeats within tolerance.
        /// </summary>
        Online,

        /// <summary>
        /// The agent has missed one or more heartbeats and is outside the immediate tolerance window,
        /// but not yet considered fully offline. Attempts to communicate might still succeed or be queued.
        /// </summary>
        Unreachable,

        /// <summary>
        /// The agent is confirmed to be disconnected or has missed heartbeats beyond the offline threshold.
        /// </summary>
        Offline,

        /// <summary>
        /// The agent's connectivity status cannot be determined. This might be a transient state 
        /// or indicate an issue if persistent.
        /// </summary>
        Unknown
    }
} 