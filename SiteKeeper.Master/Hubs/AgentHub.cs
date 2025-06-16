using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Services;
using SiteKeeper.Master.Workflow.StageHandlers; // The new location for the stage handler
using SiteKeeper.Shared.Abstractions.AgentHub;
using SiteKeeper.Shared.DTOs.AgentHub;
using SiteKeeper.Shared.DTOs.MasterSlave;
using System;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Hubs
{
    /// <summary>
    /// SignalR Hub for communication between the Master Agent and Slave Agents.
    /// In the new architecture, this hub acts as a lightweight message router. Its primary
    /// responsibility is to receive messages from connected slaves and forward them to the
    /// appropriate singleton service for processing. It no longer contains complex business logic.
    /// </summary>
    public class AgentHub : Hub<IAgentHub>, IAgentHubClient
    {
        private const string NodeNameItemKey = "NodeName";
        private readonly IAgentConnectionManagerService _agentConnectionManager;
        private readonly MultiNodeOperationStageHandler _multiNodeStageHandler;
        private readonly ILogger<AgentHub> _logger;
        private readonly IJournalService _journalService;

        /// <summary>
        /// Initializes a new instance of the AgentHub.
        // It injects the concrete MultiNodeOperationStageHandler class, which must be registered
        // as a singleton, to call its public methods for processing slave feedback.
        /// </summary>
        public AgentHub(
            IAgentConnectionManagerService agentConnectionManager,
            MultiNodeOperationStageHandler multiNodeStageHandler,
            ILogger<AgentHub> logger,
            IJournalService journalService)
        {
            _agentConnectionManager = agentConnectionManager ?? throw new ArgumentNullException(nameof(agentConnectionManager));
            _multiNodeStageHandler = multiNodeStageHandler ?? throw new ArgumentNullException(nameof(multiNodeStageHandler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_journalService = journalService ?? throw new ArgumentNullException( nameof( journalService ) );
		}

        #region Connection Lifecycle Management

        /// <summary>
        /// Called when a new client connects. It logs the connection and waits for the agent to register.
        /// The logic is delegated to the AgentConnectionManagerService after registration.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected: {ConnectionId}. Waiting for agent registration.", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when a client disconnects. It identifies the agent via its connection context
        /// and delegates the disconnection logic to the AgentConnectionManagerService.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Retrieve the NodeName that was stored in the context during registration.
            string? nodeName = Context.Items.TryGetValue(NodeNameItemKey, out var nodeNameObj) ? nodeNameObj as string : null;

            if (!string.IsNullOrEmpty(nodeName))
            {
                _logger.LogInformation("Agent disconnected: {NodeName} (ConnectionId: {ConnectionId}).", nodeName, Context.ConnectionId);
                await _agentConnectionManager.OnAgentDisconnectedAsync(Context.ConnectionId, nodeName);
            }
            else
            {
                _logger.LogWarning("Unregistered client disconnected: {ConnectionId}.", Context.ConnectionId);
                await _agentConnectionManager.OnAgentDisconnectedAsync(Context.ConnectionId, null);
            }
            await base.OnDisconnectedAsync(exception);
        }

        #endregion

        #region Slave-to-Master Message Forwarding

        /// <summary>
        /// Handles the registration request from a newly connected slave agent.
        /// This is a critical step that associates a ConnectionId with a NodeName.
        /// </summary>
        public async Task RegisterSlaveAsync(SlaveRegistrationRequest request)
        {
            var remoteIpAddress = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
            var agentInfo = await _agentConnectionManager.OnAgentConnectedAsync(Context.ConnectionId, request, remoteIpAddress);
            
            // Store the authenticated NodeName in the connection's context for reliable retrieval on disconnect.
            Context.Items[NodeNameItemKey] = agentInfo.NodeName;
        }

        /// <summary>
        /// Forwards a heartbeat from a slave to the AgentConnectionManagerService.
        /// </summary>
        public async Task SendHeartbeatAsync(SlaveHeartbeat heartbeat)
        {
            // The hub's responsibility ends here. The manager service will pass it
            // to the health monitor for processing.
            await _agentConnectionManager.ProcessHeartbeatAsync(heartbeat);
        }

        /// <summary>
        /// Forwards a task progress update from a slave directly to the MultiNodeOperationStageHandler.
        /// </summary>
        public async Task ReportOngoingTaskProgressAsync(SlaveTaskProgressUpdate taskUpdate)
        {
            // The hub is just a pass-through. The stage handler contains all the logic
            // for updating the state of the operation.
            await _multiNodeStageHandler.ProcessTaskStatusUpdateAsync(taskUpdate);
        }

        /// <summary>
        /// Forwards a task readiness report from a slave directly to the MultiNodeOperationStageHandler.
        /// </summary>
        public async Task ReportSlaveTaskReadinessAsync(SlaveTaskReadinessReport readinessReport)
        {
            await _multiNodeStageHandler.ProcessSlaveTaskReadinessAsync(readinessReport);
        }

        /// <summary>
        /// Forwards a log flush confirmation from a slave directly to the MultiNodeOperationStageHandler.
        /// This is the final step in the log synchronization handshake.
        /// </summary>
        public async Task ConfirmLogFlushForTask(string operationId, string nodeName)
        {
            _multiNodeStageHandler.ConfirmLogFlush(operationId, nodeName);
            await Task.CompletedTask; // Hub methods must return a Task.
        }

        /// <summary>
        /// Receives a log entry from a slave and forwards it to the Journal Service.
        /// This is the missing piece that allows slave logs to be persisted on the master.
        /// </summary>
        /// <param name="logEntry">The log entry DTO from the slave.</param>
        public async Task ReportSlaveTaskLogAsync(SlaveTaskLogEntry logEntry)
        {
            if (logEntry == null || string.IsNullOrEmpty(logEntry.OperationId))
            {
                _logger.LogWarning("Received an invalid or empty log entry from a slave on connection {ConnectionId}.", Context.ConnectionId);
                return;
            }

			    // The journal service is designed to find the active journal by OperationId and append the log.
            await _journalService.AppendToStageLogAsync(logEntry.OperationId, logEntry);
        }

        #endregion

        public async Task ReportResourceUsageAsync(SlaveResourceUsage resourceUsage)
        {
            if (resourceUsage == null || string.IsNullOrWhiteSpace(resourceUsage.NodeName))
            {
                 _logger.LogWarning("Invalid resource usage report received (null or no NodeName) from {ConnectionId}.", Context.ConnectionId);
                return;
            }
            _logger.LogInformation("Resource usage report received from NodeName: {NodeName}. CPU: {CpuUsage}%, Mem: {MemUsage}B, Disk: {DiskSpaceMb}MB",
                resourceUsage.NodeName, resourceUsage.CpuUsagePercentage, resourceUsage.MemoryUsageBytes, resourceUsage.AvailableDiskSpaceMb);
            // TODO: Implement processing of this report, e.g., by calling a method on _agentConnectionManagerService or another service.
            // await _agentConnectionManagerService.ProcessResourceUsageAsync(resourceUsage);
            await Task.CompletedTask; // Placeholder
        }

    }
}
