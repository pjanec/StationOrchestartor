using SiteKeeper.Master.Model.InternalData; // For CachedNodeState
using SiteKeeper.Shared.DTOs.AgentHub;
using SiteKeeper.Shared.DTOs.MasterSlave;
using SiteKeeper.Shared.Enums;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Service interface for monitoring the health and status of Slave Agents (nodes).
    /// </summary>
    /// <remarks>
    /// Responsibilities include:
    /// - Processing agent heartbeats to update their last known status and health.
    /// - Processing diagnostic reports from agents.
    /// - Maintaining an up-to-date cache of node health information (<see cref="CachedNodeState"/>).
    /// - Detecting unresponsive or unhealthy agents based on missed heartbeats or reported issues.
    /// - Potentially triggering alerts or recovery actions for unhealthy/unresponsive nodes.
    /// Details based on "SiteKeeper - Master - Core Service Implementation Guidelines.md".
    /// </remarks>
    public interface INodeHealthMonitorService
    {
        /// <summary>
        /// Processes a heartbeat DTO received from an agent to update its health status.
        /// </summary>
        /// <param name="heartbeat">The heartbeat data from the agent.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UpdateNodeHealthFromHeartbeatAsync(SlaveHeartbeat heartbeat);

        /// <summary>
        /// Processes a diagnostics report DTO received from an agent.
        /// </summary>
        /// <param name="diagnosticsReport">The diagnostics report from the agent.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UpdateNodeDiagnosticsAsync(AgentNodeDiagnosticsReport diagnosticsReport);

        /// <summary>
        /// Retrieves the cached health state for a specific node.
        /// </summary>
        /// <param name="nodeName">The name of the node.</param>
        /// <returns>The <see cref="CachedNodeState"/> for the node, or null if not found or tracked.</returns>
        Task<CachedNodeState?> GetNodeCachedStateAsync(string nodeName);

        /// <summary>
        /// Forces an update or re-evaluation of a node's status, potentially by checking last heartbeat time.
        /// </summary>
        /// <param name="nodeName">The name of the node to check.</param>
        /// <returns>The updated <see cref="AgentConnectivityStatus"/> for the node.</returns>
        Task<AgentConnectivityStatus>RefreshNodeConnectivityStatusAsync(string nodeName);

        /// <summary>
        /// Method to be called periodically (e.g., by a background service) to check for unresponsive agents
        /// and update their statuses accordingly.
        /// </summary>
        Task CheckForAllOverdueAgentsAsync();

        /// <summary>
        /// Handles a new agent connecting. Initializes its health monitoring state.
        /// </summary>
        /// <param name="agentInfo">Information about the newly connected agent.</param>
        Task OnAgentConnectedAsync(ConnectedAgentInfo agentInfo);

        /// <summary>
        /// Handles an agent disconnecting. Updates its health monitoring state to reflect disconnection.
        /// </summary>
        /// <param name="nodeName">The name of the disconnected agent's node.</param>
        Task OnAgentDisconnectedAsync(string nodeName);
    }
} 