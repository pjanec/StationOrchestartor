using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Generic;

namespace SiteKeeper.Master.Model.InternalData
{
    /// <summary>
    /// Stores information about a Slave Agent currently connected to the Master Agent via SignalR.
    /// </summary>
    /// <remarks>
    /// This class is used by the Master Agent (specifically by services like AgentConnectionManagerService
    /// and NodeHealthMonitorService) to keep track of active slave connections, their status, versions,
    /// and other relevant metadata for management and communication.
    ///
    /// Based on "SiteKeeper - Master - Data Structures.md", this includes:
    /// - Node name (primary identifier).
    /// - SignalR connection ID.
    /// - Agent software version.
    /// - Last heartbeat timestamp and reported status/health.
    /// - Connection timestamp.
    /// - IP address and other metadata.
    /// </remarks>
    public class ConnectedAgentInfo
    {
        /// <summary>
        /// Unique name of the node, which also serves as the Agent ID.
        /// </summary>
        /// <example>"AppServer01"</example>
        public string NodeName { get; private set; }

        /// <summary>
        /// The SignalR connection ID associated with this agent.
        /// </summary>
        public string SignalRConnectionId { get; set; }

        /// <summary>
        /// The version of the Slave Agent software running on the node.
        /// </summary>
        /// <example>"1.1.0-slave"</example>
        public string? AgentVersion { get; set; }

        /// <summary>
        /// Timestamp (UTC) of the last heartbeat received from this agent.
        /// </summary>
        public DateTime LastHeartbeatTime { get; set; }

        /// <summary>
        /// The last status reported by the agent via heartbeat.
        /// </summary>
        public AgentStatus LastKnownStatus { get; set; }

        /// <summary>
        /// The last node health summary reported by the agent via heartbeat.
        /// </summary>
        public NodeHealthSummary? LastKnownNodeHealth { get; set; }

        /// <summary>
        /// Timestamp (UTC) when the agent initially connected in the current session.
        /// </summary>
        public DateTime ConnectedSince { get; private set; }

        /// <summary>
        /// The remote IP address of the connected agent, if available.
        /// </summary>
        public string? RemoteIpAddress { get; set; }

        /// <summary>
        /// A description of the operating system on which the slave agent is running.
        /// </summary>
        public string? OsDescription { get; set; }

        /// <summary>
        /// A description of the .NET runtime/framework the slave agent is using.
        /// </summary>
        public string? FrameworkDescription { get; set; }

        /// <summary>
        /// The maximum number of concurrent tasks the slave agent is configured to handle.
        /// </summary>
        public int MaxConcurrentTasks { get; set; }

        /// <summary>
        /// The hostname of the machine where the slave agent is running.
        /// </summary>
        public string? Hostname { get; set; }

        /// <summary>
        /// A dictionary for storing any other relevant metadata reported by the agent
        /// during handshake or via heartbeat. Keys and value types would be specific
        /// to the information being tracked (e.g., OSVersion, AvailableDiskSpace).
        /// </summary>
        public Dictionary<string, object> Metadata { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectedAgentInfo"/> class.
        /// </summary>
        /// <param name="nodeName">The unique name of the node.</param>
        /// <param name="signalRConnectionId">The SignalR connection ID.</param>
        /// <param name="agentVersion">The agent's software version.</param>
        /// <param name="initialStatus">The initial status of the agent upon connection.</param>
        /// <param name="remoteIpAddress">The remote IP address of the agent.</param>
        public ConnectedAgentInfo(string nodeName, string signalRConnectionId, string? agentVersion, AgentStatus initialStatus, string? remoteIpAddress = null)
        {
            if (string.IsNullOrWhiteSpace(nodeName))
                throw new ArgumentNullException(nameof(nodeName));
            if (string.IsNullOrWhiteSpace(signalRConnectionId))
                throw new ArgumentNullException(nameof(signalRConnectionId));

            NodeName = nodeName;
            SignalRConnectionId = signalRConnectionId;
            AgentVersion = agentVersion;
            LastKnownStatus = initialStatus;
            ConnectedSince = DateTime.UtcNow;
            LastHeartbeatTime = ConnectedSince; // Initialize with connection time
            RemoteIpAddress = remoteIpAddress;
            Metadata = new Dictionary<string, object>();
        }
    }
} 