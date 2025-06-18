using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Abstractions.Services.Journaling;
using SiteKeeper.Master.Hubs;
using SiteKeeper.Master.Model.InternalData;
using SiteKeeper.Shared.Abstractions.AgentHub;
using SiteKeeper.Shared.DTOs.AgentHub;
using SiteKeeper.Shared.DTOs.MasterSlave;
using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Services
{
    /// <summary>
    /// Manages connections, state, and direct communication with all connected Slave Agents.
    /// This service acts as the primary gateway for sending commands from the master to the slaves
    /// and is responsible for journaling key agent lifecycle events to the Change Journal.
    /// </summary>
    /// <remarks>
    /// This service implements <see cref="IAgentConnectionManagerService"/>. It maintains a real-time list
    /// of connected agents, facilitates sending typed messages via SignalR (AgentHub), and processes
    /// incoming messages like heartbeats and diagnostic reports by coordinating with other services
    /// such as <see cref="INodeHealthMonitorService"/> and <see cref="IJournalService"/>.
    /// Key responsibilities include agent registration upon connection, deregistration upon disconnection,
    /// providing current agent status, and abstracting direct SignalR hub invocations for other services.
    /// </remarks>
    public class AgentConnectionManagerService : IAgentConnectionManagerService
    {
        // A thread-safe dictionary holding information for all currently connected agents, keyed by their unique NodeName.
        private readonly ConcurrentDictionary<string, ConnectedAgentInfo> _connectedAgents = new();
        
        // A reverse lookup map for finding a NodeName quickly from a SignalR ConnectionId, crucial for disconnect handling.
        private readonly ConcurrentDictionary<string, string> _connectionIdToNodeNameMap = new();

        private readonly IHubContext<AgentHub, IAgentHub> _hubContext;
        private readonly ILogger<AgentConnectionManagerService> _logger;
        private readonly INodeHealthMonitorService _nodeHealthMonitorService;
        private readonly IJournalService _journalService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentConnectionManagerService"/> class.
        /// </summary>
        /// <param name="hubContext">The SignalR hub context for <see cref="AgentHub"/>, used to communicate with slave agents.</param>
        /// <param name="logger">The logger for recording service activity and errors.</param>
        /// <param name="nodeHealthMonitorService">The service responsible for tracking and updating node health based on agent interactions.</param>
        /// <param name="journalService">The service for recording significant agent lifecycle events in the system journal.</param>
        public AgentConnectionManagerService(
            IHubContext<AgentHub, IAgentHub> hubContext,
            ILogger<AgentConnectionManagerService> logger,
            INodeHealthMonitorService nodeHealthMonitorService,
            IJournalService journalService)
        {
            _hubContext = hubContext;
            _logger = logger;
            _nodeHealthMonitorService = nodeHealthMonitorService;
            _journalService = journalService;
        }

        #region Lifecycle and State Management

        /// <summary>
        /// Handles a new agent connecting to the Master's AgentHub.
        /// It registers the agent in internal collections, updates its health status via <see cref="INodeHealthMonitorService"/>,
        /// and creates a record in the Change Journal via <see cref="IJournalService"/>.
        /// </summary>
        /// <param name="connectionId">The SignalR connection ID of the newly connected agent.</param>
        /// <param name="request">The <see cref="SlaveRegistrationRequest"/> DTO containing details from the slave agent.</param>
        /// <param name="remoteIpAddress">The remote IP address of the connecting agent, if available.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the created <see cref="ConnectedAgentInfo"/> object for the agent.</returns>
        public async Task<ConnectedAgentInfo> OnAgentConnectedAsync(string connectionId, SlaveRegistrationRequest request, string? remoteIpAddress)
        {
            var agentInfo = new ConnectedAgentInfo(request.AgentName, connectionId, request.AgentVersion, AgentStatus.Online, remoteIpAddress);
            _connectedAgents[request.AgentName] = agentInfo;
            _connectionIdToNodeNameMap[connectionId] = request.AgentName;
            
            _logger.LogInformation("Agent '{NodeName}' connected with ConnectionId: {ConnectionId}.", request.AgentName, connectionId);

            // Journal the connection event
            var changeInfo = new StateChangeInfo { Type = ChangeEventType.SystemEvent, Description = $"Agent '{request.AgentName}' connected.", SourceMasterActionId = "system-event", InitiatedBy = "system" };
            var changeRecord = await _journalService.InitiateStateChangeAsync(changeInfo);
            await _journalService.FinalizeStateChangeAsync(new StateChangeFinalizationInfo { ChangeId = changeRecord.ChangeId, Outcome = OperationOutcome.Success, Description = $"Agent '{request.AgentName}' connected successfully.", ResultArtifact = new { connectionId, remoteIpAddress, agentVersion = request.AgentVersion } });

            await _nodeHealthMonitorService.OnAgentConnectedAsync(agentInfo);
            return agentInfo;
        }

        /// <summary>
        /// Handles an agent disconnecting from the Master's AgentHub.
        /// It removes the agent from internal tracking collections, updates its health status via <see cref="INodeHealthMonitorService"/>,
        /// and creates a record in the Change Journal via <see cref="IJournalService"/>.
        /// </summary>
        /// <param name="connectionId">The SignalR connection ID of the disconnected agent.</param>
        /// <param name="nodeName">The unique name of the node/agent that disconnected. This might be null if the node name was not yet known for this connectionId.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task OnAgentDisconnectedAsync(string connectionId, string? nodeName)
        {
            // Attempt to get nodeName from connectionId if not provided, or verify provided nodeName
            if (string.IsNullOrEmpty(nodeName) && _connectionIdToNodeNameMap.TryGetValue(connectionId, out var mappedNodeName))
            {
                nodeName = mappedNodeName;
            }

            if (!string.IsNullOrEmpty(nodeName) && _connectedAgents.TryRemove(nodeName, out _))
            {
                _connectionIdToNodeNameMap.TryRemove(connectionId, out _); // Clean up reverse map
                _logger.LogWarning("Agent '{NodeName}' disconnected with ConnectionId: {ConnectionId}.", nodeName, connectionId);

                // Journal the disconnection event
                var changeInfo = new StateChangeInfo { Type = ChangeEventType.SystemEvent, Description = $"Agent '{nodeName}' disconnected.", SourceMasterActionId = "system-event", InitiatedBy = "system" };
                var changeRecord = await _journalService.InitiateStateChangeAsync(changeInfo);
                await _journalService.FinalizeStateChangeAsync(new StateChangeFinalizationInfo { ChangeId = changeRecord.ChangeId, Outcome = OperationOutcome.Success, Description = $"Agent '{nodeName}' disconnected from connection {connectionId}.", ResultArtifact = new { connectionId } });

                await _nodeHealthMonitorService.OnAgentDisconnectedAsync(nodeName);
            }
            else
            {
                _logger.LogWarning("Agent with ConnectionId '{ConnectionId}' disconnected, but was not found in the active list or nodeName could not be determined.", connectionId);
            }
        }
        
        /// <summary>
        /// Retrieves information about a specific connected agent by its unique node name.
        /// </summary>
        /// <param name="nodeName">The name of the node (agent).</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="ConnectedAgentInfo"/> if the agent is currently connected; otherwise, null.</returns>
        public Task<ConnectedAgentInfo?> GetConnectedAgentAsync(string nodeName)
        {
             _connectedAgents.TryGetValue(nodeName, out var agentInfo);
            return Task.FromResult(agentInfo);
        }

        /// <summary>
        /// Retrieves information about a specific connected agent by its SignalR connection ID.
        /// </summary>
        /// <param name="connectionId">The SignalR connection ID of the agent.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="ConnectedAgentInfo"/> if an agent with that connection ID is found; otherwise, null.</returns>
        public Task<ConnectedAgentInfo?> GetConnectedAgentByConnectionIdAsync(string connectionId)
        {
            if (_connectionIdToNodeNameMap.TryGetValue(connectionId, out var nodeName) && nodeName != null)
            {
                return GetConnectedAgentAsync(nodeName);
            }
            return Task.FromResult<ConnectedAgentInfo?>(null);
        }

        /// <summary>
        /// Gets a list of all currently connected agents.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of <see cref="ConnectedAgentInfo"/> objects for all agents currently connected.</returns>
        public Task<List<ConnectedAgentInfo>> GetAllConnectedAgentsAsync()
        {
            return Task.FromResult(_connectedAgents.Values.ToList());
        }

        #endregion

        #region Master-to-Slave Commands

        /// <summary>
        /// Sends a "Prepare For Task" instruction to a specific agent.
        /// This is part of a two-phase task commit process, instructing the agent to check its readiness.
        /// </summary>
        /// <param name="nodeName">The unique name of the target slave node.</param>
        /// <param name="instruction">The <see cref="PrepareForTaskInstruction"/> DTO containing details of the preparation.</param>
        /// <returns>A task representing the asynchronous send operation.</returns>
        public Task SendPrepareForTaskInstructionAsync(string nodeName, PrepareForTaskInstruction instruction) =>
            SendToAgentAsync(nodeName, (client, inst) => client.ReceivePrepareForTaskInstructionAsync(inst), instruction);

        /// <summary>
        /// Sends an actual "Slave Task" instruction to a specific agent, typically after readiness has been confirmed.
        /// This instructs the agent to execute the specified task.
        /// </summary>
        /// <param name="nodeName">The unique name of the target slave node.</param>
        /// <param name="instruction">The <see cref="SlaveTaskInstruction"/> DTO containing details of the task to be executed.</param>
        /// <returns>A task representing the asynchronous send operation.</returns>
        public Task SendSlaveTaskAsync(string nodeName, SlaveTaskInstruction instruction) =>
            SendToAgentAsync(nodeName, (client, inst) => client.ReceiveSlaveTaskAsync(inst), instruction);

        /// <summary>
        /// Sends a "Cancel Task" request to a specific agent to attempt cancellation of an ongoing or scheduled task.
        /// </summary>
        /// <param name="nodeName">The unique name of the target slave node.</param>
        /// <param name="request">The <see cref="CancelTaskOnAgentRequest"/> DTO containing the ID of the task to cancel.</param>
        /// <returns>A task representing the asynchronous send operation.</returns>
        public Task SendCancelTaskAsync(string nodeName, CancelTaskOnAgentRequest request) =>
            SendToAgentAsync(nodeName, (client, req) => client.ReceiveCancelTaskRequestAsync(req), request);
        
        /// <summary>
        /// Sends a command to a specific slave agent instructing it to flush its buffered log queue for a given node action.
        /// </summary>
        /// <param name="nodeName">The unique name of the target slave node.</param>
        /// <param name="actionId">The unique identifier of the node action whose logs should be flushed.</param>
        /// <returns>A task representing the asynchronous send operation.</returns>
        public Task RequestLogFlushForTask(string nodeName, string actionId)
        {
            _logger.LogDebug("Sending log flush request to node {NodeName} for action {ActionId}", nodeName, actionId);
            return SendToAgentAsync(nodeName, (client, actId) => client.RequestLogFlushForTask(actId), actionId);
        }

        /// <summary>
        /// Sends a general command (e.g., ping, custom status request) to a specific agent.
        /// </summary>
        /// <param name="nodeName">The unique name of the target slave node.</param>
        /// <param name="request">The <see cref="NodeGeneralCommandRequest"/> DTO containing the command details.</param>
        /// <returns>A task representing the asynchronous send operation.</returns>
        public Task SendGeneralCommandAsync(string nodeName, NodeGeneralCommandRequest request) =>
            SendToAgentAsync(nodeName, (client, req) => client.SendGeneralCommandAsync(req), request);

        /// <summary>
        /// Sends Master state information (e.g., current manifest ID, master version) to a specific agent.
        /// </summary>
        /// <param name="nodeName">The unique name of the target slave node.</param>
        /// <param name="state">The <see cref="MasterStateForAgent"/> DTO containing the state information.</param>
        /// <returns>A task representing the asynchronous send operation.</returns>
        public Task SendMasterStateUpdateAsync(string nodeName, MasterStateForAgent state) =>
            SendToAgentAsync(nodeName, (client, st) => client.UpdateMasterStateAsync(st), state);

        /// <summary>
        /// Sends a command to adjust the system time on a specific agent based on the Master's authoritative time.
        /// </summary>
        /// <param name="nodeName">The unique name of the target slave node.</param>
        /// <param name="command">The <see cref="AdjustSystemTimeCommand"/> DTO containing the authoritative timestamp.</param>
        /// <returns>A task representing the asynchronous send operation.</returns>
        public Task SendAdjustSystemTimeCommandAsync(string nodeName, AdjustSystemTimeCommand command) =>
            SendToAgentAsync(nodeName, (client, cmd) => client.RequestTimeSyncAsync(cmd), command);
        
        #endregion

        #region Slave-to-Master Processing

        /// <summary>
        /// Processes a heartbeat received from a slave agent.
        /// This method delegates to the <see cref="INodeHealthMonitorService"/> to update the agent's health status.
        /// </summary>
        /// <param name="heartbeat">The <see cref="SlaveHeartbeat"/> DTO received from the agent.</param>
        /// <returns>A task representing the asynchronous processing operation.</returns>
        public async Task ProcessHeartbeatAsync(SlaveHeartbeat heartbeat)
        {
            await _nodeHealthMonitorService.UpdateNodeHealthFromHeartbeatAsync(heartbeat);
        }

        /// <summary>
        /// Processes a diagnostics report received from a slave agent.
        /// This method delegates to the <see cref="INodeHealthMonitorService"/> to update the agent's diagnostic information.
        /// </summary>
        /// <param name="diagnosticsReport">The <see cref="AgentNodeDiagnosticsReport"/> DTO received from the agent.</param>
        /// <returns>A task representing the asynchronous processing operation.</returns>
        public async Task ProcessDiagnosticsReportAsync(AgentNodeDiagnosticsReport diagnosticsReport)
        {
            if (diagnosticsReport == null)
            {
                 _logger.LogError("Received null diagnostics report object.");
                 // Consider journaling this error if it represents a significant issue.
                 return;
            }
            await _nodeHealthMonitorService.UpdateNodeDiagnosticsAsync(diagnosticsReport);
            _logger.LogInformation("Diagnostics report from '{AgentId}' forwarded to NodeHealthMonitorService.", diagnosticsReport.AgentId);
        }

        /// <summary>
        /// Processes a general command response received from a slave agent.
        /// </summary>
        /// <remarks>
        /// In a full implementation, this method would likely raise an event or forward this response
        /// to a service that is awaiting it, correlating it by <see cref="AgentGeneralCommandResponse.OriginalCommandType"/>
        /// or another unique identifier from the original request.
        /// </remarks>
        /// <param name="commandResponse">The <see cref="AgentGeneralCommandResponse"/> DTO received from the agent.</param>
        /// <returns>A task representing the asynchronous processing operation.</returns>
        public Task ProcessGeneralCommandResponseAsync(AgentGeneralCommandResponse commandResponse)
        {
             if (commandResponse == null)
            {
                _logger.LogError("Received null general command response object.");
                return Task.CompletedTask;
            }
            // In a real implementation, this would likely raise an event or forward this response to a service
            // that is waiting for it, correlating it by the OriginalCommandType or another ID.
            _logger.LogInformation("General command response from '{AgentId}' for CommandType '{CommandType}' (Success: {IsSuccess}) received.",
                commandResponse.AgentId, commandResponse.OriginalCommandType, commandResponse.IsSuccess);
            return Task.CompletedTask;
        }
        
        #endregion

        #region Private Helper

        /// <summary>
        /// A generic, private helper method to send a strongly-typed message to a specific agent.
        /// It looks up the agent's connection ID and invokes the specified hub action.
        /// </summary>
        /// <typeparam name="T">The type of the payload object.</typeparam>
        /// <param name="nodeName">The name of the target node.</param>
        /// <param name="hubAction">A lambda expression representing the client method to call on the IAgentHub interface.</param>
        /// <param name="payload">The data to send.</param>
        private async Task SendToAgentAsync<T>(string nodeName, Func<IAgentHub, T, Task> hubAction, T payload)
        {
            _logger.LogDebug("Attempting to send message of type {PayloadType} to agent '{NodeName}'.", typeof(T).Name, nodeName);

            if (_connectedAgents.TryGetValue(nodeName, out var agentInfo))
            {
                _logger.LogDebug("Found connected agent '{NodeName}' with ConnectionId '{ConnectionId}'. Sending message.", 
                    nodeName, agentInfo.SignalRConnectionId);

                try
                {
                    var clientProxy = _hubContext.Clients.Client(agentInfo.SignalRConnectionId);
                    await hubAction(clientProxy, payload);

                    _logger.LogInformation("Successfully sent message of type {PayloadType} to agent '{NodeName}'.", typeof(T).Name, nodeName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending message of type {PayloadType} to agent '{NodeName}'.", typeof(T).Name, nodeName);
                    
                    // Journal the communication failure as a significant system event.
                    var changeInfo = new StateChangeInfo { Type = ChangeEventType.SystemEvent, Description = $"Agent communication failure for node '{nodeName}'.", SourceMasterActionId = "system-event", InitiatedBy = "system" };
                    var changeRecord = await _journalService.InitiateStateChangeAsync(changeInfo);
                    await _journalService.FinalizeStateChangeAsync(new StateChangeFinalizationInfo { ChangeId = changeRecord.ChangeId, Outcome = OperationOutcome.Failure, Description = $"Failed to send message of type {typeof(T).Name} to agent '{nodeName}'.", ResultArtifact = new { errorMessage = ex.Message, payloadType = typeof(T).Name } });
                }
            }
            else
            {
                _logger.LogWarning("Attempted to send message to offline or unknown agent '{NodeName}'.", nodeName);
            }
        }
        #endregion
    }
}
