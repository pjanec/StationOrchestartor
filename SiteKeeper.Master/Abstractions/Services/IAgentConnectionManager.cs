using SiteKeeper.Master.Model.InternalData;
using SiteKeeper.Shared.DTOs.AgentHub;
using SiteKeeper.Shared.DTOs.MasterSlave;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Service interface for managing connections and interactions with Slave Agents.
    /// </summary>
    /// <remarks>
    /// This service is responsible for:
    /// - Tracking currently connected agents (<see cref="ConnectedAgentInfo"/>).
    /// - Handling agent connection and disconnection events.
    /// - Providing methods to retrieve information about connected agents.
    /// - Facilitating sending messages/commands to specific agents or all agents.
    /// - Processing messages received from agents, such as heartbeats and task status updates (often by delegating to other services).
    /// Details based on "SiteKeeper - Master - Core Service Implementation Guidelines.md".
    /// </remarks>
    public interface IAgentConnectionManager
    {
        /// <summary>
        /// Handles a new agent connecting to the Master's AgentHub.
        /// </summary>
        /// <param name="connectionId">The SignalR connection ID of the newly connected agent.</param>
        /// <param name="request">The registration request DTO from the slave agent.</param>
        /// <param name="remoteIpAddress">The remote IP address of the connecting agent.</param>
        /// <returns>A task representing the asynchronous operation. The resulting <see cref="ConnectedAgentInfo"/>.</returns>
        Task<ConnectedAgentInfo> OnAgentConnectedAsync(string connectionId, SlaveRegistrationRequest request, string? remoteIpAddress);

        /// <summary>
        /// Handles an agent disconnecting from the Master's AgentHub.
        /// </summary>
        /// <param name="connectionId">The SignalR connection ID of the disconnected agent.</param>
        /// <param name="nodeName">The unique name of the node/agent that disconnected (if known).</param>

        Task OnAgentDisconnectedAsync(string connectionId, string? nodeName);

        /// <summary>
        /// Retrieves information about a specific connected agent by its node name.
        /// </summary>
        /// <param name="nodeName">The name of the node.</param>
        /// <returns>The <see cref="ConnectedAgentInfo"/> if the agent is connected; otherwise, null.</returns>
        Task<ConnectedAgentInfo?> GetConnectedAgentAsync(string nodeName);

        /// <summary>
        /// Retrieves information about a specific connected agent by its SignalR connection ID.
        /// </summary>
        /// <param name="connectionId">The SignalR connection ID.</param>
        /// <returns>The <see cref="ConnectedAgentInfo"/> if an agent with that connection ID is found; otherwise, null.</returns>
        Task<ConnectedAgentInfo?> GetConnectedAgentByConnectionIdAsync(string connectionId);

        /// <summary>
        /// Gets a list of all currently connected agents.
        /// </summary>
        /// <returns>A list of <see cref="ConnectedAgentInfo"/> objects.</returns>
        Task<List<ConnectedAgentInfo>> GetAllConnectedAgentsAsync();

        // Methods for sending messages to agents (delegated to IHubContext internally)
        // These are high-level methods; the service will use IHubContext<AgentHub, IAgentHub> to send.

        /// <summary>
        /// Sends a "Prepare For Task" instruction to a specific agent.
        /// </summary>
        Task SendPrepareForTaskInstructionAsync(string nodeName, PrepareForTaskInstruction instruction);

        /// <summary>
        /// Sends an actual "Slave Task" instruction to a specific agent (after readiness check if applicable).
        /// </summary>
        Task SendSlaveTaskAsync(string nodeName, SlaveTaskInstruction instruction);

        /// <summary>
        /// Sends a "Cancel Task" request to a specific agent.
        /// </summary>
        Task SendCancelTaskAsync(string nodeName, CancelTaskOnAgentRequest request);

        /// <summary>
        /// Sends a "General Command" to a specific agent.
        /// </summary>
        Task SendGeneralCommandAsync(string nodeName, NodeGeneralCommandRequest request);

        /// <summary>
        /// Sends a "Master State Update" to a specific agent.
        /// </summary>
        Task SendMasterStateUpdateAsync(string nodeName, MasterStateForAgent state);

        /// <summary>
        /// Sends a command to adjust system time on a specific agent.
        /// </summary>
        Task SendAdjustSystemTimeCommandAsync(string nodeName, AdjustSystemTimeCommand command);


        /// <summary>
        /// Sends a command to a specific slave agent instructing it to flush its buffered log queue.
        /// </summary>
        /// <param name="nodeName">The name of the target slave node.</param>
        /// <param name="operationId">The ID of the operation associated with the logs to be flushed.</param>
        Task RequestLogFlushForTask(string nodeName, string operationId);


        // Methods for processing messages from agents (called by the AgentHub)
        // These would typically be called by the AgentHub upon receiving messages from clients.

        /// <summary>
        /// Processes a heartbeat received from an agent.
        /// </summary>
        /// <remarks>This might delegate to NodeHealthMonitorService.</remarks>
        Task ProcessHeartbeatAsync(SlaveHeartbeat heartbeat);

        /// <summary>
        /// Processes a diagnostics report received from an agent.
        /// </summary>
        Task ProcessDiagnosticsReportAsync(AgentNodeDiagnosticsReport diagnosticsReport);

        /// <summary>
        /// Processes a general command response received from an agent.
        /// </summary>
        Task ProcessGeneralCommandResponseAsync(AgentGeneralCommandResponse commandResponse);
    }
} 