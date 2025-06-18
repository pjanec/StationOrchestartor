using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Services; // NodeCoordinator is now in this namespace
using SiteKeeper.Shared.Abstractions.AgentHub;
using SiteKeeper.Shared.DTOs.AgentHub;
using SiteKeeper.Shared.DTOs.MasterSlave;
using System;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Hubs
{
    /// <summary>
    /// SignalR Hub for real-time communication between the SiteKeeper Master Agent and connected Slave Agents.
    /// This hub implements <see cref="IAgentHubClient"/> for methods callable by Slave Agents,
    /// and uses <see cref="IAgentHub"/> (via <see cref="IHubContext{THub, TClient}"/>) to call methods on Slave Agents.
    /// </summary>
    /// <remarks>
    /// In the current architecture, this hub primarily acts as a lightweight message router. Its main
    /// responsibility is to receive messages from connected slaves and forward them to the
    /// appropriate singleton service (e.g., <see cref="IAgentConnectionManagerService"/>, <see cref="NodeCoordinator"/>)
    /// for processing. It generally does not contain complex business logic itself.
    /// Connection lifecycle events (connect/disconnect) are managed here and delegated to the <see cref="IAgentConnectionManagerService"/>.
    /// A <c>NodeNameItemKey</c> is used with <see cref="HubCallerContext.Items"/> to associate SignalR connections with authenticated node names.
    /// </remarks>
    public class AgentHub : Hub<IAgentHub>, IAgentHubClient
    {
        private const string NodeNameItemKey = "NodeName"; // Key for storing NodeName in Context.Items
        private readonly IAgentConnectionManagerService _agentConnectionManager;
        private readonly NodeCoordinator _multiNodeStageHandler; // Concrete class injection for direct method calls
        private readonly ILogger<AgentHub> _logger;
        // IJournalService is injected but primarily used via NodeCoordinator for slave logs.
        // If AgentHub were to directly journal other specific events, it could use this.
        private readonly IJournalService _journalService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentHub"/> class.
        /// </summary>
        /// <param name="agentConnectionManager">Service for managing agent connections and lifecycle.</param>
        /// <param name="multiNodeStageHandler">Handler for processing task-related messages from agents (status, readiness, logs).</param>
        /// <param name="logger">Logger for hub activities.</param>
        /// <param name="journalService">Service for journaling (though direct use here might be minimal, often delegated).</param>
        /// <exception cref="ArgumentNullException">Thrown if any of the injected services are null.</exception>
        public AgentHub(
            IAgentConnectionManagerService agentConnectionManager,
            NodeCoordinator multiNodeStageHandler, // Assuming this is correctly registered as singleton or scoped if Hub is transient
            ILogger<AgentHub> logger,
            IJournalService journalService)
        {
            _agentConnectionManager = agentConnectionManager ?? throw new ArgumentNullException(nameof(agentConnectionManager));
            _multiNodeStageHandler = multiNodeStageHandler ?? throw new ArgumentNullException(nameof(multiNodeStageHandler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_journalService = journalService ?? throw new ArgumentNullException(nameof(journalService));
		}

        #region Connection Lifecycle Management

        /// <summary>
        /// Called when a new client connects to the hub.
        /// Logs the connection attempt. Actual agent registration and association with a node name
        /// occur when the agent calls the <see cref="RegisterSlaveAsync"/> method.
        /// </summary>
        /// <returns>A <see cref="Task"/> that represents the asynchronous connect event.</returns>
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected: {ConnectionId}. Waiting for agent registration.", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when a client disconnects from the hub.
        /// Retrieves the node name associated with the connection (if registered) from <see cref="HubCallerContext.Items"/>
        /// and delegates the disconnection logic to the <see cref="IAgentConnectionManagerService"/>.
        /// </summary>
        /// <param name="exception">The <see cref="Exception"/> that occurred during disconnect, if any.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous disconnect event.</returns>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            string? nodeName = Context.Items.TryGetValue(NodeNameItemKey, out var nodeNameObj) ? nodeNameObj as string : null;

            if (!string.IsNullOrEmpty(nodeName))
            {
                _logger.LogInformation("Agent '{NodeName}' (ConnectionId: {ConnectionId}) disconnected. Exception: {Exception}", nodeName, Context.ConnectionId, exception?.Message ?? "N/A");
                await _agentConnectionManager.OnAgentDisconnectedAsync(Context.ConnectionId, nodeName);
            }
            else
            {
                _logger.LogWarning("Unregistered client disconnected: {ConnectionId}. Exception: {Exception}", Context.ConnectionId, exception?.Message ?? "N/A");
                await _agentConnectionManager.OnAgentDisconnectedAsync(Context.ConnectionId, null); // Ensure manager service is notified even for unregistered disconnects
            }
            await base.OnDisconnectedAsync(exception);
        }

        #endregion

        #region Slave-to-Master Message Forwarding

        /// <summary>
        /// Handles the registration request from a newly connected slave agent.
        /// Delegates to <see cref="IAgentConnectionManagerService.OnAgentConnectedAsync"/> to register the agent
        /// and then stores the authenticated node name in the <see cref="HubCallerContext.Items"/> for this connection.
        /// </summary>
        /// <param name="request">The <see cref="SlaveRegistrationRequest"/> DTO sent by the slave agent.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous registration operation.</returns>
        public async Task RegisterSlaveAsync(SlaveRegistrationRequest request)
        {
            var remoteIpAddress = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
            var agentInfo = await _agentConnectionManager.OnAgentConnectedAsync(Context.ConnectionId, request, remoteIpAddress);
            
            Context.Items[NodeNameItemKey] = agentInfo.NodeName; // Associate NodeName with ConnectionId
            _logger.LogInformation("Agent '{NodeName}' successfully registered for ConnectionId {ConnectionId}.", agentInfo.NodeName, Context.ConnectionId);
        }

        /// <summary>
        /// Receives a heartbeat from a slave agent and forwards it to the <see cref="IAgentConnectionManagerService"/>
        /// for processing (which typically delegates to <see cref="INodeHealthMonitorService"/>).
        /// </summary>
        /// <param name="heartbeat">The <see cref="SlaveHeartbeat"/> DTO from the agent.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous processing of the heartbeat.</returns>
        public async Task SendHeartbeatAsync(SlaveHeartbeat heartbeat)
        {
            _logger.LogTrace("Heartbeat received from Node: {NodeName}", heartbeat.NodeName);
            await _agentConnectionManager.ProcessHeartbeatAsync(heartbeat);
        }

        /// <summary>
        /// Receives a task progress update from a slave agent and forwards it directly to the
        /// <see cref="NodeCoordinator"/> for processing and state updates.
        /// </summary>
        /// <param name="taskUpdate">The <see cref="SlaveTaskProgressUpdate"/> DTO from the agent.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous processing of the task update.</returns>
        public async Task ReportOngoingTaskProgressAsync(SlaveTaskProgressUpdate taskUpdate)
        {
            _logger.LogTrace("Task progress update from Node: {NodeName}, TaskId: {TaskId}, Status: {Status}",
                taskUpdate.NodeName, taskUpdate.TaskId, taskUpdate.Status);
            await _multiNodeStageHandler.ProcessTaskStatusUpdateAsync(taskUpdate);
        }

        /// <summary>
        /// Receives a task readiness report from a slave agent and forwards it directly to the
        /// <see cref="NodeCoordinator"/> for processing.
        /// </summary>
        /// <param name="readinessReport">The <see cref="SlaveTaskReadinessReport"/> DTO from the agent.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous processing of the readiness report.</returns>
        public async Task ReportSlaveTaskReadinessAsync(SlaveTaskReadinessReport readinessReport)
        {
            _logger.LogDebug("Task readiness report from Node: {NodeName}, TaskId: {TaskId}, IsReady: {IsReady}",
                readinessReport.NodeName, readinessReport.TaskId, readinessReport.IsReady);
            await _multiNodeStageHandler.ProcessSlaveTaskReadinessAsync(readinessReport);
        }

        /// <summary>
        /// Receives a log flush confirmation from a slave agent and forwards it to the
        /// <see cref="NodeCoordinator"/>. This is part of log synchronization.
        /// </summary>
        /// <param name="actionId">The ID of the node action for which logs were flushed.</param>
        /// <param name="nodeName">The name of the node confirming the flush.</param>
        /// <returns>A <see cref="Task"/> representing the completion of this hub method call.</returns>
        public Task ConfirmLogFlushForTask(string actionId, string nodeName)
        {
            _logger.LogDebug("Log flush confirmation from Node: {NodeName} for ActionId: {ActionId}", nodeName, actionId);
            _multiNodeStageHandler.ConfirmLogFlush(actionId, nodeName);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Receives a log entry from a slave agent related to a task and forwards it to the
        /// <see cref="NodeCoordinator"/> for journaling.
        /// </summary>
        /// <param name="logEntry">The <see cref="SlaveTaskLogEntry"/> DTO from the slave agent.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous processing of the log entry.</returns>
        public async Task ReportSlaveTaskLogAsync(SlaveTaskLogEntry logEntry)
        {
            _logger.LogTrace("Slave task log from Node: {NodeName}, ActionId: {ActionId}, TaskId: {TaskId}, Message: '{Message}'",
                logEntry.NodeName, logEntry.ActionId, logEntry.TaskId, logEntry.LogMessage);

            if (logEntry == null || string.IsNullOrEmpty(logEntry.ActionId))
            {
                _logger.LogWarning("Received an invalid or empty log entry from a slave on connection {ConnectionId}.", Context.ConnectionId);
                return;
            }
            await _multiNodeStageHandler.JournalSlaveLogAsync(logEntry);
        }

        /// <summary>
        /// Receives a resource usage report from a slave agent.
        /// </summary>
        /// <remarks>
        /// Currently, this method logs the received report. Processing of this report (e.g., updating
        /// <see cref="INodeHealthMonitorService"/> or <see cref="IAgentConnectionManagerService"/>) is noted as a TODO.
        /// </remarks>
        /// <param name="resourceUsage">The <see cref="SlaveResourceUsage"/> DTO from the agent.</param>
        /// <returns>A <see cref="Task"/> representing the completion of this hub method call.</returns>
        public Task ReportResourceUsageAsync(SlaveResourceUsage resourceUsage)
        {
            if (resourceUsage == null || string.IsNullOrWhiteSpace(resourceUsage.NodeName))
            {
                 _logger.LogWarning("Invalid resource usage report received (null or no NodeName) from {ConnectionId}.", Context.ConnectionId);
                return Task.CompletedTask;
            }
            _logger.LogInformation("Resource usage report received from NodeName: {NodeName}. CPU: {CpuUsage}%, Mem: {MemUsageBytes}B, Disk: {DiskSpaceMb}MB",
                resourceUsage.NodeName, resourceUsage.CpuUsagePercentage, resourceUsage.MemoryUsageBytes, resourceUsage.AvailableDiskSpaceMb);
            // TODO: Implement processing of this report, e.g., by calling a method on _agentConnectionManagerService or INodeHealthMonitorService.
            // Example: await _agentConnectionManagerService.ProcessResourceUsageAsync(resourceUsage);
            return Task.CompletedTask; // Placeholder
        }
        #endregion
    }
}
