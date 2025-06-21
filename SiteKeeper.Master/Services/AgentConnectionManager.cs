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
    public class AgentConnectionManager : IAgentConnectionManager
    {
        // A thread-safe dictionary holding information for all currently connected agents, keyed by their unique NodeName.
        private readonly ConcurrentDictionary<string, ConnectedAgentInfo> _connectedAgents = new();
        
        // A reverse lookup map for finding a NodeName quickly from a SignalR ConnectionId, crucial for disconnect handling.
        private readonly ConcurrentDictionary<string, string> _connectionIdToNodeNameMap = new();

        private readonly IHubContext<AgentHub, IAgentHub> _hubContext;
        private readonly ILogger<AgentConnectionManager> _logger;
        private readonly INodeHealthMonitor _nodeHealthMonitorService;
        private readonly IJournal _journalService;

        public AgentConnectionManager(
            IHubContext<AgentHub, IAgentHub> hubContext,
            ILogger<AgentConnectionManager> logger,
            INodeHealthMonitor nodeHealthMonitorService,
            IJournal journalService)
        {
            _hubContext = hubContext;
            _logger = logger;
            _nodeHealthMonitorService = nodeHealthMonitorService;
            _journalService = journalService;
        }

        #region Lifecycle and State Management

        /// <summary>
        /// Handles a new agent connecting to the Master's AgentHub.
        /// It registers the agent, updates its state, and creates a record in the Change Journal.
        /// </summary>
        public async Task<ConnectedAgentInfo> OnAgentConnectedAsync(string connectionId, SlaveRegistrationRequest request, string? remoteIpAddress)
        {
            var agentInfo = new ConnectedAgentInfo(request.AgentName, connectionId, request.AgentVersion, AgentStatus.Online, remoteIpAddress);
            _connectedAgents[request.AgentName] = agentInfo;
            _connectionIdToNodeNameMap[connectionId] = request.AgentName;
            
            _logger.LogInformation("Agent '{NodeName}' connected with ConnectionId: {ConnectionId}.", request.AgentName, connectionId);

            var changeInfo = new StateChangeInfo { Type = ChangeEventType.SystemEvent, Description = $"Agent '{request.AgentName}' connected.", SourceMasterActionId = "system-event", InitiatedBy = "system" };
            var changeRecord = await _journalService.InitiateStateChangeAsync(changeInfo);
            await _journalService.FinalizeStateChangeAsync(new StateChangeFinalizationInfo { ChangeId = changeRecord.ChangeId, Outcome = OperationOutcome.Success, Description = $"Agent '{request.AgentName}' connected successfully.", ResultArtifact = new { connectionId, remoteIpAddress, agentVersion = request.AgentVersion } });

            await _nodeHealthMonitorService.OnAgentConnectedAsync(agentInfo);
            return agentInfo;
        }

        /// <summary>
        /// Handles an agent disconnecting from the Master's AgentHub.
        /// It removes the agent from the active list and creates a record in the Change Journal.
        /// </summary>
        public async Task OnAgentDisconnectedAsync(string connectionId, string? nodeName)
        {
            if (!string.IsNullOrEmpty(nodeName) && _connectedAgents.TryRemove(nodeName, out _))
            {
                _connectionIdToNodeNameMap.TryRemove(connectionId, out _);
                _logger.LogWarning("Agent '{NodeName}' disconnected.", nodeName);

                var changeInfo = new StateChangeInfo { Type = ChangeEventType.SystemEvent, Description = $"Agent '{nodeName}' disconnected.", SourceMasterActionId = "system-event", InitiatedBy = "system" };
                var changeRecord = await _journalService.InitiateStateChangeAsync(changeInfo);
                await _journalService.FinalizeStateChangeAsync(new StateChangeFinalizationInfo { ChangeId = changeRecord.ChangeId, Outcome = OperationOutcome.Success, Description = $"Agent '{nodeName}' disconnected from connection {connectionId}.", ResultArtifact = new { connectionId } });

                await _nodeHealthMonitorService.OnAgentDisconnectedAsync(nodeName);
            }
        }
        
        public Task<ConnectedAgentInfo?> GetConnectedAgentAsync(string nodeName)
        {
             _connectedAgents.TryGetValue(nodeName, out var agentInfo);
            return Task.FromResult(agentInfo);
        }

        public Task<ConnectedAgentInfo?> GetConnectedAgentByConnectionIdAsync(string connectionId)
        {
            if (_connectionIdToNodeNameMap.TryGetValue(connectionId, out var nodeName) && nodeName != null)
            {
                return GetConnectedAgentAsync(nodeName);
            }
            return Task.FromResult<ConnectedAgentInfo?>(null);
        }

        public Task<List<ConnectedAgentInfo>> GetAllConnectedAgentsAsync()
        {
            return Task.FromResult(_connectedAgents.Values.ToList());
        }

        #endregion

        #region Master-to-Slave Commands

        public Task SendPrepareForTaskInstructionAsync(string nodeName, PrepareForTaskInstruction instruction) =>
            SendToAgentAsync(nodeName, (client, inst) => client.ReceivePrepareForTaskInstructionAsync(inst), instruction);

        public Task SendSlaveTaskAsync(string nodeName, SlaveTaskInstruction instruction) =>
            SendToAgentAsync(nodeName, (client, inst) => client.ReceiveSlaveTaskAsync(inst), instruction);

        public Task SendCancelTaskAsync(string nodeName, CancelTaskOnAgentRequest request) =>
            SendToAgentAsync(nodeName, (client, req) => client.ReceiveCancelTaskRequestAsync(req), request);
        
        public Task RequestLogFlushForTask(string nodeName, string actionId)
        {
            _logger.LogDebug("Sending log flush request to node {NodeName} for action {ActionId}", nodeName, actionId);
            return SendToAgentAsync(nodeName, (client, opId) => client.RequestLogFlushForTask(opId), actionId);
        }

        public Task SendGeneralCommandAsync(string nodeName, NodeGeneralCommandRequest request) =>
            SendToAgentAsync(nodeName, (client, req) => client.SendGeneralCommandAsync(req), request);

        public Task SendMasterStateUpdateAsync(string nodeName, MasterStateForAgent state) =>
            SendToAgentAsync(nodeName, (client, st) => client.UpdateMasterStateAsync(st), state);

        public Task SendAdjustSystemTimeCommandAsync(string nodeName, AdjustSystemTimeCommand command) =>
            SendToAgentAsync(nodeName, (client, cmd) => client.RequestTimeSyncAsync(cmd), command);
        
        #endregion

        #region Slave-to-Master Processing

        public async Task ProcessHeartbeatAsync(SlaveHeartbeat heartbeat)
        {
            await _nodeHealthMonitorService.UpdateNodeHealthFromHeartbeatAsync(heartbeat);
        }

        public async Task ProcessDiagnosticsReportAsync(AgentNodeDiagnosticsReport diagnosticsReport)
        {
            if (diagnosticsReport == null)
            {
                 _logger.LogError("Received null diagnostics report object.");
                 // Journaling this error is a good practice
                 return;
            }
            await _nodeHealthMonitorService.UpdateNodeDiagnosticsAsync(diagnosticsReport);
            _logger.LogInformation("Diagnostics report from '{AgentId}' forwarded to NodeHealthMonitorService.", diagnosticsReport.AgentId);
        }

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
