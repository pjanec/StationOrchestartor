using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Abstractions.Services.Journaling;
using SiteKeeper.Master.Model.InternalData;
using SiteKeeper.Shared.DTOs.AgentHub;
using SiteKeeper.Shared.DTOs.MasterSlave;
using SiteKeeper.Shared.DTOs.SignalR;
using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Services
{
    /// <summary>
    /// A singleton service responsible for monitoring the health and connectivity status of all Slave Agents (nodes).
    /// It implements <see cref="IHostedService"/> to run background checks for unresponsive agents and
    /// <see cref="INodeHealthMonitorService"/> to process incoming health data and provide status.
    /// </summary>
    /// <remarks>
    /// This service maintains a <see cref="ConcurrentDictionary{TKey,TValue}"/> of <see cref="CachedNodeState"/> objects,
    /// representing the last known state of each node. It processes heartbeats and diagnostic reports from agents,
    /// updates these cached states, and uses a timer to periodically check for agents that have missed heartbeats
    /// beyond configured tolerance levels (<see cref="MasterConfig.HeartbeatIntervalSeconds"/>, with internal multipliers for tolerance/offline).
    /// Significant status changes (e.g., agent online/offline, health degradation) are journaled via <see cref="IJournalService"/>
    /// and broadcast to GUI clients via <see cref="IGuiNotifierService"/>.
    /// </remarks>
    public class NodeHealthMonitorService : INodeHealthMonitorService, IHostedService, IDisposable
    {
        private readonly ConcurrentDictionary<string, CachedNodeState> _nodeStates = new();
        private readonly ILogger<NodeHealthMonitorService> _logger;
        private readonly IJournalService _journalService;
        private readonly IGuiNotifierService _guiNotifierService;
        private readonly MasterConfig _config;
        private Timer? _overdueAgentCheckTimer;

        private readonly TimeSpan _heartbeatTolerance; // Derived from config, e.g., 1.5x to 2x heartbeat interval
        private readonly TimeSpan _offlineThreshold;   // Derived from config, e.g., 3x to 5x heartbeat interval

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeHealthMonitorService"/> class.
        /// </summary>
        /// <param name="logger">The logger for recording service activity, health checks, and errors.</param>
        /// <param name="journalService">The service for recording significant node status changes to the system journal.</param>
        /// <param name="guiNotifierService">The service for sending real-time node status updates to GUI clients.</param>
        /// <param name="configOptions">The master configuration options, used to determine heartbeat intervals and thresholds.</param>
        public NodeHealthMonitorService(
            ILogger<NodeHealthMonitorService> logger,
            IJournalService journalService,
            IGuiNotifierService guiNotifierService,
            IOptions<MasterConfig> configOptions)
        {
            _logger = logger;
            _journalService = journalService;
            _guiNotifierService = guiNotifierService;
            _config = configOptions.Value;

            // Example: Define tolerance and offline thresholds based on configured heartbeat interval
            // These could be made more configurable if needed.
            _heartbeatTolerance = TimeSpan.FromSeconds(Math.Max(10, _config.HeartbeatIntervalSeconds * 1.5)); // At least 10s, or 1.5x interval
            _offlineThreshold = TimeSpan.FromSeconds(Math.Max(30, _config.HeartbeatIntervalSeconds * 3));    // At least 30s, or 3x interval
            _logger.LogInformation("NodeHealthMonitorService configured. HeartbeatTolerance: {Tolerance}s, OfflineThreshold: {Threshold}s",
                _heartbeatTolerance.TotalSeconds, _offlineThreshold.TotalSeconds);
        }

        #region IHostedService Implementation
        
        /// <summary>
        /// Called by the application host when the service is starting.
        /// Initializes and starts a periodic timer to check for overdue (unresponsive) agents.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to indicate if startup should be aborted.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous start operation.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("NodeHealthMonitorService starting. Overdue agent check interval: {Seconds}s", _offlineThreshold.TotalSeconds / 2); // Check more frequently than full offline threshold
            _overdueAgentCheckTimer = new Timer(
                async _ => await CheckForAllOverdueAgentsAsync(), 
                null, 
                TimeSpan.FromSeconds(15), // Initial delay before first check
                TimeSpan.FromSeconds(Math.Max(5, _config.HeartbeatIntervalSeconds))); // Subsequent checks based on heartbeat interval (min 5s)
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called by the application host when the service is stopping, during a graceful shutdown.
        /// Stops the periodic timer for checking overdue agents.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to indicate if shutdown should be quick.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous stop operation.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("NodeHealthMonitorService stopping.");
            _overdueAgentCheckTimer?.Change(Timeout.Infinite, 0); // Stop the timer
            return Task.CompletedTask;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// Disposes of the timer used for periodic agent checks.
        /// </summary>
        public void Dispose()
        {
            _overdueAgentCheckTimer?.Dispose();
            GC.SuppressFinalize(this); // Suppress finalization as Dispose does the cleanup
        }

        #endregion

        #region INodeHealthMonitorService Implementation

        /// <summary>
        /// Handles a new agent connecting by creating or updating its cached state to <see cref="AgentConnectivityStatus.Online"/>.
        /// Journals the event and notifies GUI clients if the status changed.
        /// </summary>
        /// <param name="agentInfo">Information about the newly connected agent, as provided by <see cref="IAgentConnectionManagerService"/>.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task OnAgentConnectedAsync(ConnectedAgentInfo agentInfo)
        {
            var nodeState = await GetOrCreateNodeStateAsync(agentInfo.NodeName);
            var oldStatus = nodeState.ConnectivityStatus;
            
            nodeState.ConnectivityStatus = AgentConnectivityStatus.Online;
            nodeState.LastKnownAgentVersion = agentInfo.AgentVersion;
            nodeState.LastHeartbeatTimestamp = agentInfo.LastHeartbeatTime;
            nodeState.LastStateUpdateTime = DateTime.UtcNow;
            
            if (oldStatus != AgentConnectivityStatus.Online)
            {
                _logger.LogInformation("Node '{NodeName}' came online. Previous status: {OldStatus}", agentInfo.NodeName, oldStatus);
                await JournalAndNotifyStatusChange(nodeState, oldStatus);
            }
        }

        /// <summary>
        /// Handles an agent disconnecting by updating its cached state to <see cref="AgentConnectivityStatus.Offline"/>.
        /// Journals the event and notifies GUI clients if the status changed.
        /// </summary>
        /// <param name="nodeName">The unique name of the node whose agent has disconnected.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task OnAgentDisconnectedAsync(string nodeName)
        {
            var nodeState = await GetOrCreateNodeStateAsync(nodeName);
            var oldStatus = nodeState.ConnectivityStatus;
            
            nodeState.ConnectivityStatus = AgentConnectivityStatus.Offline;
            nodeState.LastStateUpdateTime = DateTime.UtcNow;
            
            if (oldStatus != AgentConnectivityStatus.Offline)
            {
                _logger.LogWarning("Node '{NodeName}' went offline. Previous status: {OldStatus}", nodeName, oldStatus);
                await JournalAndNotifyStatusChange(nodeState, oldStatus);
            }
        }

        /// <summary>
        /// Processes a heartbeat DTO received from an agent to update its health status,
        /// including last heartbeat time, resource usage, and setting connectivity to <see cref="AgentConnectivityStatus.Online"/>.
        /// Journals and notifies GUI clients if the connectivity status changed, or always notifies for resource usage updates.
        /// </summary>
        /// <param name="heartbeat">The <see cref="SlaveHeartbeat"/> DTO received from the agent.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task UpdateNodeHealthFromHeartbeatAsync(SlaveHeartbeat heartbeat)
        {
            var nodeState = await GetOrCreateNodeStateAsync(heartbeat.NodeName);
            var oldStatus = nodeState.ConnectivityStatus;

            nodeState.LastHeartbeatTimestamp = heartbeat.Timestamp.UtcDateTime;
            nodeState.ConnectivityStatus = AgentConnectivityStatus.Online;
            
            // Store the resource usage from the heartbeat in the cache.
            nodeState.LastCpuUsagePercent = heartbeat.CpuUsagePercent;
            nodeState.LastRamUsagePercent = heartbeat.RamUsagePercent;
            nodeState.LastStateUpdateTime = DateTime.UtcNow;

            if (oldStatus != AgentConnectivityStatus.Online)
            {
                _logger.LogInformation("Node '{NodeName}' is back online due to heartbeat. Previous status: {OldStatus}", heartbeat.NodeName, oldStatus);
                await JournalAndNotifyStatusChange(nodeState, oldStatus);
            }
            else
            {
                // If it was already online, still send a UI notification
                // to update the CPU/RAM values in real-time.
                await NotifyStatusChange(nodeState);
            }
        }

        /// <summary>
        /// Processes a diagnostics report DTO received from an agent, updating the cached diagnostics information
        /// and health summary for the node. Journals and notifies GUI clients if the health summary changed.
        /// </summary>
        /// <param name="diagnosticsReport">The <see cref="AgentNodeDiagnosticsReport"/> DTO received from the agent.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task UpdateNodeDiagnosticsAsync(AgentNodeDiagnosticsReport diagnosticsReport)
        {
            var nodeState = await GetOrCreateNodeStateAsync(diagnosticsReport.AgentId);
            var oldHealthSummary = nodeState.LastHealthSummary;

            nodeState.LastFullDiagnosticsReport = diagnosticsReport.Report;
            nodeState.LastHealthSummary = diagnosticsReport.Report.OverallHealth;
            nodeState.LastStateUpdateTime = DateTime.UtcNow;
            
            if (oldHealthSummary != nodeState.LastHealthSummary)
            {
                await JournalHealthChange(nodeState, oldHealthSummary);
                await NotifyStatusChange(nodeState);
            }
        }

        /// <summary>
        /// Retrieves the cached health state (<see cref="CachedNodeState"/>) for a specific node.
        /// </summary>
        /// <param name="nodeName">The unique name of the node.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="CachedNodeState"/> for the node, or null if the node is not tracked.</returns>
        public Task<CachedNodeState?> GetNodeCachedStateAsync(string nodeName)
        {
            _nodeStates.TryGetValue(nodeName, out var nodeState);
            return Task.FromResult(nodeState);
        }

        /// <summary>
        /// Forces an update or re-evaluation of a node's status by checking its last heartbeat time.
        /// This provides an on-demand way to get the most current connectivity status. This method is now public.
        /// </summary>
        public async Task<AgentConnectivityStatus> RefreshNodeConnectivityStatusAsync(string nodeName)
        {
            var nodeState = await GetOrCreateNodeStateAsync(nodeName);
            var oldStatus = nodeState.ConnectivityStatus;
            
            if (nodeState.LastHeartbeatTimestamp.HasValue)
            {
                var timeSinceLastHeartbeat = DateTime.UtcNow - nodeState.LastHeartbeatTimestamp.Value;
                if (timeSinceLastHeartbeat > _offlineThreshold)
                    nodeState.ConnectivityStatus = AgentConnectivityStatus.Offline;
                else if (timeSinceLastHeartbeat > _heartbeatTolerance)
                    nodeState.ConnectivityStatus = AgentConnectivityStatus.Unreachable;
                else
                    nodeState.ConnectivityStatus = AgentConnectivityStatus.Online;
            }
            else
            {
                if(nodeState.ConnectivityStatus != AgentConnectivityStatus.NeverConnected)
                    nodeState.ConnectivityStatus = AgentConnectivityStatus.Unknown;
            }

            if (oldStatus != nodeState.ConnectivityStatus)
            {
                nodeState.LastStateUpdateTime = DateTime.UtcNow;
                _logger.LogWarning("Node '{NodeName}' connectivity status changed from {OldStatus} to {NewStatus} via refresh check.", nodeName, oldStatus, nodeState.ConnectivityStatus);
                await JournalAndNotifyStatusChange(nodeState, oldStatus);
            }
            return nodeState.ConnectivityStatus;
        }

        #endregion

        #region Private Helper & Background Methods

        /// <summary>
        /// Periodically called by a timer to iterate through all tracked nodes and refresh their connectivity status,
        /// particularly to identify agents that have become Unreachable or Offline due to missed heartbeats.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of checking all agents.</returns>
        public async Task CheckForAllOverdueAgentsAsync()
        {
            _logger.LogDebug("Running periodic check for overdue agents...");
            var nodeNames = _nodeStates.Keys.ToList(); 

            foreach (var nodeName in nodeNames)
            {
                if (_nodeStates.TryGetValue(nodeName, out var nodeState) && 
                   (nodeState.ConnectivityStatus == AgentConnectivityStatus.Online || nodeState.ConnectivityStatus == AgentConnectivityStatus.Unreachable))
                {
                    await RefreshNodeConnectivityStatusAsync(nodeName); 
                }
            }
        }

        private Task<CachedNodeState> GetOrCreateNodeStateAsync(string nodeName)
        {
            return Task.FromResult(_nodeStates.GetOrAdd(nodeName, nn => {
                _logger.LogInformation("Creating new CachedNodeState for newly discovered node: {NodeName}", nn);
                return new CachedNodeState(nn);
            }));
        }

        private async Task JournalAndNotifyStatusChange(CachedNodeState nodeState, AgentConnectivityStatus oldStatus)
        {
            var changeInfo = new StateChangeInfo { Type = ChangeEventType.SystemEvent, Description = $"Node '{nodeState.NodeName}' status changed from {oldStatus} to {nodeState.ConnectivityStatus}.", SourceMasterActionId = "system-health-monitor", InitiatedBy = "system" };
            var changeRecord = await _journalService.InitiateStateChangeAsync(changeInfo);
            await _journalService.FinalizeStateChangeAsync(new StateChangeFinalizationInfo { ChangeId = changeRecord.ChangeId, Outcome = OperationOutcome.Success, Description = changeInfo.Description, ResultArtifact = new { nodeState.NodeName, oldStatus, newStatus = nodeState.ConnectivityStatus } });
            await NotifyStatusChange(nodeState);
        }

        private async Task JournalHealthChange(CachedNodeState nodeState, NodeHealthSummary? oldHealth)
        {
             var changeInfo = new StateChangeInfo { Type = ChangeEventType.SystemEvent, Description = $"Node '{nodeState.NodeName}' health changed from {oldHealth} to {nodeState.LastHealthSummary}.", SourceMasterActionId = "system-health-monitor", InitiatedBy = "system" };
            var changeRecord = await _journalService.InitiateStateChangeAsync(changeInfo);
            await _journalService.FinalizeStateChangeAsync(new StateChangeFinalizationInfo { ChangeId = changeRecord.ChangeId, Outcome = OperationOutcome.Success, Description = changeInfo.Description, ResultArtifact = new { nodeState.NodeName, oldHealth, newHealth = nodeState.LastHealthSummary } });
        }
        
        private async Task NotifyStatusChange(CachedNodeState nodeState)
        {
            var updateDto = new SignalRNodeStatusUpdate
            {
                NodeName = nodeState.NodeName,
                AgentStatus = MapToApiStatus(nodeState.ConnectivityStatus),
                HealthSummary = nodeState.LastHealthSummary ?? NodeHealthSummary.Unknown,
                // Read the values from the correct properties on CachedNodeState.
                CpuUsagePercent = (int?)(nodeState.LastCpuUsagePercent),
                RamUsagePercent = (int?)(nodeState.LastRamUsagePercent)
            };
            await _guiNotifierService.NotifyNodeStatusUpdateAsync(updateDto);
        }

        private AgentStatus MapToApiStatus(AgentConnectivityStatus internalStatus) => internalStatus switch
        {
            AgentConnectivityStatus.Online => AgentStatus.Online,
            AgentConnectivityStatus.Offline => AgentStatus.Offline,
            AgentConnectivityStatus.Unreachable => AgentStatus.Offline,
            _ => AgentStatus.Unknown,
        };

        #endregion

    }
}
