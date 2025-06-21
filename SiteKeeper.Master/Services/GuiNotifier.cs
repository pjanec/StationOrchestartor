using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Hubs; // For GuiHub
using SiteKeeper.Shared.Abstractions.GuiHub; // For IGuiHub client type in IHubContext
using SiteKeeper.Shared.DTOs.API.AuditLog;
using SiteKeeper.Shared.DTOs.SignalR;
using System;
using System.Threading.Tasks;
using SiteKeeper.Shared.DTOs.API.Diagnostics;
using SiteKeeper.Shared.DTOs.API.Environment;
using SiteKeeper.Shared.DTOs.AgentHub;

namespace SiteKeeper.Master.Services
{
    /// <summary>
    /// Service responsible for sending real-time notifications to GUI clients via SignalR.
    /// </summary>
    public class GuiNotifier : IGuiNotifier
    {
        private readonly IHubContext<GuiHub, IGuiHub> _guiHubContext;
        private readonly ILogger<GuiNotifier> _logger;
        private readonly IActionIdTranslator _actionIdTranslator;

        public GuiNotifier(
            IHubContext<GuiHub, IGuiHub> guiHubContext,
            ILogger<GuiNotifier> logger,
            IActionIdTranslator actionIdTranslator)
        {
            _guiHubContext = guiHubContext ?? throw new ArgumentNullException(nameof(guiHubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _actionIdTranslator = actionIdTranslator;
        }

        public async Task NotifyNodeStatusUpdateAsync(SignalRNodeStatusUpdate update)
        {
            _logger.LogDebug("Notifying all GUI clients of NodeStatusUpdate for Node: {NodeName}, AgentStatus: {AgentStatus}, HealthSummary: {HealthSummary}", 
                update.NodeName, update.AgentStatus, update.HealthSummary);
            await _guiHubContext.Clients.All.ReceiveNodeStatusUpdate(update);
        }

        public async Task NotifyAppStatusUpdateAsync(SignalRAppStatusUpdate update)
        {
            _logger.LogDebug("Notifying all GUI clients of AppStatusUpdate for App: {AppName} on Node: {NodeName}, Status: {Status}", 
                update.AppName, update.NodeName, update.Status);
            await _guiHubContext.Clients.All.ReceiveAppStatusUpdate(update);
        }

        public async Task NotifyPlanStatusUpdateAsync(SignalRPlanStatusUpdate update)
        {
            _logger.LogDebug("Notifying all GUI clients of PlanStatusUpdate for Plan: {PlanName}, Status: {Status}", update.PlanName, update.Status);
            await _guiHubContext.Clients.All.ReceivePlanStatusUpdate(update);
        }

        public async Task NotifySystemSoftwareStatusUpdateAsync(SignalRSystemSoftwareStatusUpdate update)
        {
            _logger.LogDebug("Notifying all GUI clients of SystemSoftwareStatusUpdate: {OverallStatus}", update.OverallStatus);
            await _guiHubContext.Clients.All.ReceiveSystemSoftwareStatusUpdate(update);
        }

        public async Task NotifyOperationProgressAsync(SignalROperationProgress progress)
        {
            _logger.LogDebug("Notifying all GUI clients of OperationProgress for OpId: {OperationId}, Status: {Status}, Percent: {ProgressPercent}", 
                progress.OperationId, progress.Status, progress.ProgressPercent);
            // TODO: Consider sending only to clients interested in this specific operation if user/group mapping is implemented.
            await _guiHubContext.Clients.All.ReceiveOperationProgress(progress);
        }

        public async Task NotifyOperationCompletedAsync(SignalROperationCompleted completed)
        {
            _logger.LogDebug("Notifying all GUI clients of OperationCompleted for OpId: {OperationId}, Status: {FinalStatus}", 
                completed.OperationId, completed.FinalStatus);
            // TODO: Consider sending only to clients interested in this specific operation.
            await _guiHubContext.Clients.All.ReceiveOperationCompleted(completed);
        }

        public async Task NotifyAuditLogEntryAddedAsync(AuditLogEntry entry)
        {
            _logger.LogDebug("Notifying all GUI clients of new AuditLogEntry: Id={AuditId}, OperationType={OperationType}, User={User}", 
                entry.Id, entry.OperationType, entry.User);
            await _guiHubContext.Clients.All.ReceiveAuditLogEntry(entry);
        }

        public async Task NotifyMasterGoingDownAsync(SignalRMasterGoingDown info)
        {
            _logger.LogInformation("Notifying all GUI clients that Master is going down. Reason: {Reason}, Message: {Message}", 
                info.Reason, info.Message);
            await _guiHubContext.Clients.All.ReceiveMasterGoingDown(info);
        }

        public async Task NotifyMasterReconnectedAsync(SignalRMasterReconnected reconnectedNotification)
        {
            _logger.LogInformation("Notifying all GUI clients that Master has reconnected. Message: {Message}", reconnectedNotification.Message);
            await _guiHubContext.Clients.All.MasterReconnected(reconnectedNotification);
        }

        public async Task NotifyEnvironmentManifestUpdatedAsync(PureManifest newManifest)
        {
            _logger.LogInformation("Notifying UI: Environment Manifest Updated for {EnvironmentName} to version {VersionId}", newManifest.EnvironmentName, newManifest.VersionId);
            await _guiHubContext.Clients.All.EnvironmentManifestUpdated(newManifest);
        }

        public async Task NotifyHealthCheckIssueFoundAsync(HealthCheckIssue issue)
        {
            _logger.LogWarning("Notifying UI (All): Health Check Issue Found. Source: {Source}, Check: {CheckName}, Severity: {Severity}", issue.Source, issue.CheckName, issue.Severity);
            await _guiHubContext.Clients.All.HealthCheckIssueFound(issue);
        }

        public async Task NotifyOperationLogEntryAsync(SlaveTaskLogEntry logEntry)
        {
            // The logEntry.ActionId from the slave IS the NodeActionId
            var nodeActionId = logEntry.ActionId;
            
            // Translate the NodeActionId to the parent MasterActionId
            var masterActionId = _actionIdTranslator.TranslateNodeActionIdToMasterActionId(nodeActionId);

            if (string.IsNullOrEmpty(masterActionId))
            {
                _logger.LogWarning("Could not translate NodeActionId '{NodeActionId}' to a MasterActionId. Dropping log message: {Message}", nodeActionId, logEntry.LogMessage);
                return;
            }

            _logger.LogTrace("Notifying UI: Op Log for MasterOp: {MasterOpId}, NodeOp: {NodeOpId}, Task: {TaskId}", masterActionId, nodeActionId, logEntry.TaskId);

            var dto = new SignalROperationLogEntry
            {
                OperationId = masterActionId, // Use the translated ID
                NodeActionId = nodeActionId, // Send the original NodeActionId for detailed context
                TaskId = logEntry.TaskId,
                NodeName = logEntry.NodeName,
                TimestampUtc = logEntry.TimestampUtc,
                LogLevel = logEntry.LogLevel,
                Message = logEntry.LogMessage
            };
            await _guiHubContext.Clients.All.ReceiveOperationLogEntry(dto);
        }

        public async Task SendTestResponseAsync(string connectionId, SignalRServerToClientTestResponse response)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
            {
                _logger.LogWarning("SendTestResponseAsync called with null or empty connectionId.");
                return;
            }
            _logger.LogDebug("Sending ServerToClientTestResponse to specific ConnectionId: {ConnectionId}", connectionId);
            await _guiHubContext.Clients.Client(connectionId).ReceiveServerToClientTestResponse(response);
        }
    }
} 