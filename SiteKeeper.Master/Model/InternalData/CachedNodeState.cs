using SiteKeeper.Shared.DTOs.API.Diagnostics;
using SiteKeeper.Shared.DTOs.API.Environment; // For NodeSummary.PackageOnNode
using SiteKeeper.Shared.DTOs.API.Nodes;
using SiteKeeper.Shared.DTOs.API.SoftwareControl; // For AppStatusInfo
using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Generic;

namespace SiteKeeper.Master.Model.InternalData
{
    /// <summary>
    /// Represents the Master Agent's cached understanding of a node's state.
    /// </summary>
    /// <remarks>
    /// This class stores a snapshot of information about a specific node, including its connectivity,
    /// health, installed packages, application statuses, and diagnostics. This cache is updated
    /// based on heartbeats, task reports, and direct queries to agents when they are online.
    /// It allows the Master to have a recent view of node states even if an agent is temporarily disconnected.
    ///
    /// Based on "SiteKeeper - Master - Data Structures.md", this includes:
    /// - Node name.
    /// - Agent connectivity status (derived, e.g., Online if recently heartbeating, Offline otherwise).
    /// - Timestamps for last heartbeat and last cache update.
    /// - Agent version, health summary.
    /// - Last full diagnostics report.
    /// - Lists of currently installed packages and application statuses on the node.
    /// </remarks>
    public class CachedNodeState
    {
        /// <summary>
        /// Unique name of the node this cached state pertains to.
        /// </summary>
        /// <example>"AppServer01"</example>
        public string NodeName { get; private set; }

        /// <summary>
        /// The Master's assessment of the agent's connectivity status.
        /// This is typically derived from heartbeat frequency and other interactions.
        /// </summary>
        public AgentConnectivityStatus ConnectivityStatus { get; set; }

        /// <summary>
        /// Timestamp (UTC) of the last successful heartbeat received from this node's agent.
        /// Null if no heartbeat has ever been received or if the node is considered permanently offline.
        /// </summary>
        public DateTime? LastHeartbeatTimestamp { get; set; }

        /// <summary>
        /// The last known version of the Slave Agent software on the node.
        /// </summary>
        public string? LastKnownAgentVersion { get; set; }

        /// <summary>
        /// The last reported health summary of the node.
        /// </summary>
        public NodeHealthSummary? LastHealthSummary { get; set; }

        /// <summary>
        /// The last known CPU usage percentage from a heartbeat.
        /// </summary>
        public double? LastCpuUsagePercent { get; set; }

        /// <summary>
        /// The last known RAM usage percentage from a heartbeat.
        /// </summary>
        public double? LastRamUsagePercent { get; set; }

        /// <summary>
        /// The last full diagnostics report received from the node.
        /// This would store the content of a <see cref="NodeDiagnosticsReport"/> DTO.
        /// </summary>
        public NodeDiagnosticsReport? LastFullDiagnosticsReport { get; set; }

        /// <summary>
        /// List of software packages currently understood to be on the node, with their versions and status.
        /// This maps to the `PackageOnNode` structure defined within `NodeSummary` DTO.
        /// </summary>
        public List<PackageOnNode> CurrentPackages { get; set; }

        /// <summary>
        /// List of applications and their current statuses on the node.
        /// </summary>
        public List<AppStatusInfo> CurrentAppStatuses { get; set; }

        /// <summary>
        /// Timestamp (UTC) when this cached state entry was last updated with new information.
        /// </summary>
        public DateTime LastStateUpdateTime { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedNodeState"/> class.
        /// </summary>
        /// <param name="nodeName">The name of the node.</param>
        public CachedNodeState(string nodeName)
        {
            if (string.IsNullOrWhiteSpace(nodeName))
                throw new ArgumentNullException(nameof(nodeName));

            NodeName = nodeName;
            ConnectivityStatus = AgentConnectivityStatus.Unknown; // Initial state
            CurrentPackages = new List<PackageOnNode>();
            CurrentAppStatuses = new List<AppStatusInfo>();
            LastStateUpdateTime = DateTime.UtcNow;
        }
    }
} 