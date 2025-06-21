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
    /// It runs as a background hosted service to periodically check for unresponsive agents.
    /// </summary>
    public class NodeHealthMonitor : INodeHealthMonitor, IHostedService, IDisposable
    {
        private readonly ConcurrentDictionary<string, CachedNodeState> _nodeStates = new();
        private readonly ILogger<NodeHealthMonitor> _logger;
        private readonly IJournal _journalService;
        private readonly IGuiNotifier _guiNotifierService;
        private readonly MasterConfig _config;
        private Timer? _overdueAgentCheckTimer;

        private readonly TimeSpan _heartbeatTolerance;
        private readonly TimeSpan _offlineThreshold;

        public NodeHealthMonitor(
            ILogger<NodeHealthMonitor> logger,
            IJournal journalService,
            IGuiNotifier guiNotifierService,
            IOptions<MasterConfig> configOptions)
        {
            _logger = logger;
            _journalService = journalService;
            _guiNotifierService = guiNotifierService;
            _config = configOptions.Value;
            _heartbeatTolerance = TimeSpan.FromSeconds(90); 
            _offlineThreshold = TimeSpan.FromSeconds(300);
        }

        #region IHostedService Implementation
        
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("NodeHealthMonitorService starting. Overdue agent check interval: {Seconds}s", _config.HeartbeatIntervalSeconds * 3);
            _overdueAgentCheckTimer = new Timer(
                async _ => await CheckForAllOverdueAgentsAsync(), 
                null, 
                TimeSpan.FromSeconds(15), 
                TimeSpan.FromSeconds(_config.HeartbeatIntervalSeconds * 3));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("NodeHealthMonitorService stopping.");
            _overdueAgentCheckTimer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _overdueAgentCheckTimer?.Dispose();
        }

        #endregion

        #region INodeHealthMonitorService Implementation

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
