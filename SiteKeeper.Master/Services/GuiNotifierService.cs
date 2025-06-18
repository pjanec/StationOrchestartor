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
    /// Service responsible for sending real-time notifications to connected GUI clients via SignalR's <see cref="GuiHub"/>.
    /// </summary>
    /// <remarks>
    /// This service implements <see cref="IGuiNotifierService"/> and acts as an abstraction layer over the
    /// <see cref="IHubContext{THub,TClient}"/> for the <see cref="GuiHub"/>. It allows other Master services
    /// (e.g., <see cref="IMasterActionCoordinatorService"/>, <see cref="INodeHealthMonitorService"/>, <see cref="IAuditLogService"/>)
    /// to broadcast typed messages and updates to all or specific GUI clients without directly interacting with SignalR hub contexts.
    /// The methods in this service directly correspond to client-callable methods defined in <see cref="IGuiHub"/>.
    /// </remarks>
    public class GuiNotifierService : IGuiNotifierService
    {
        private readonly IHubContext<GuiHub, IGuiHub> _guiHubContext;
        private readonly ILogger<GuiNotifierService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GuiNotifierService"/> class.
        /// </summary>
        /// <param name="guiHubContext">The SignalR hub context for the <see cref="GuiHub"/>, used to interact with connected GUI clients.</param>
        /// <param name="logger">The logger for recording service activity, such as notification dispatches.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="guiHubContext"/> or <paramref name="logger"/> is null.</exception>
        public GuiNotifierService(
            IHubContext<GuiHub, IGuiHub> guiHubContext,
            ILogger<GuiNotifierService> logger)
        {
            _guiHubContext = guiHubContext ?? throw new ArgumentNullException(nameof(guiHubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Notifies all connected GUI clients about a change in a specific node's status.
        /// This method invokes the <c>ReceiveNodeStatusUpdate</c> method on all clients.
        /// </summary>
        /// <param name="update">The <see cref="SignalRNodeStatusUpdate"/> DTO containing the new status information for the node.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of sending the notification.</returns>
        public async Task NotifyNodeStatusUpdateAsync(SignalRNodeStatusUpdate update)
        {
            _logger.LogDebug("Notifying all GUI clients of NodeStatusUpdate for Node: {NodeName}, AgentStatus: {AgentStatus}, HealthSummary: {HealthSummary}", 
                update.NodeName, update.AgentStatus, update.HealthSummary);
            await _guiHubContext.Clients.All.ReceiveNodeStatusUpdate(update);
        }

        /// <summary>
        /// Notifies all connected GUI clients about a change in a specific application's status.
        /// This method invokes the <c>ReceiveAppStatusUpdate</c> method on all clients.
        /// </summary>
        /// <param name="update">The <see cref="SignalRAppStatusUpdate"/> DTO containing the new status information for the application.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of sending the notification.</returns>
        public async Task NotifyAppStatusUpdateAsync(SignalRAppStatusUpdate update)
        {
            _logger.LogDebug("Notifying all GUI clients of AppStatusUpdate for App: {AppName} on Node: {NodeName}, Status: {Status}", 
                update.AppName, update.NodeName, update.Status);
            await _guiHubContext.Clients.All.ReceiveAppStatusUpdate(update);
        }

        /// <summary>
        /// Notifies all connected GUI clients about a change in an application plan's status.
        /// This method invokes the <c>ReceivePlanStatusUpdate</c> method on all clients.
        /// </summary>
        /// <param name="update">The <see cref="SignalRPlanStatusUpdate"/> DTO containing the new status information for the plan.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of sending the notification.</returns>
        public async Task NotifyPlanStatusUpdateAsync(SignalRPlanStatusUpdate update)
        {
            _logger.LogDebug("Notifying all GUI clients of PlanStatusUpdate for Plan: {PlanName}, Status: {Status}", update.PlanName, update.Status);
            await _guiHubContext.Clients.All.ReceivePlanStatusUpdate(update);
        }

        /// <summary>
        /// Notifies all connected GUI clients about a change in the overall system software status.
        /// This method invokes the <c>ReceiveSystemSoftwareStatusUpdate</c> method on all clients.
        /// </summary>
        /// <param name="update">The <see cref="SignalRSystemSoftwareStatusUpdate"/> DTO containing the new overall system status.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of sending the notification.</returns>
        public async Task NotifySystemSoftwareStatusUpdateAsync(SignalRSystemSoftwareStatusUpdate update)
        {
            _logger.LogDebug("Notifying all GUI clients of SystemSoftwareStatusUpdate: {OverallStatus}", update.OverallStatus);
            await _guiHubContext.Clients.All.ReceiveSystemSoftwareStatusUpdate(update);
        }

        /// <summary>
        /// Sends progress updates for an ongoing operation to all connected GUI clients.
        /// This method invokes the <c>ReceiveOperationProgress</c> method on all clients.
        /// </summary>
        /// <param name="progress">The <see cref="SignalROperationProgress"/> DTO containing the progress details.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of sending the notification.</returns>
        /// <remarks>
        /// For scalability, consider sending updates only to clients specifically subscribed to or interested in this operation,
        /// if user/group mapping or per-operation subscription is implemented in the SignalR hub.
        /// </remarks>
        public async Task NotifyOperationProgressAsync(SignalROperationProgress progress)
        {
            _logger.LogDebug("Notifying all GUI clients of OperationProgress for OpId: {OperationId}, Status: {Status}, Percent: {ProgressPercent}", 
                progress.OperationId, progress.Status, progress.ProgressPercent);
            // TODO: Consider sending only to clients interested in this specific operation if user/group mapping is implemented.
            await _guiHubContext.Clients.All.ReceiveOperationProgress(progress);
        }

        /// <summary>
        /// Notifies all connected GUI clients that a specific operation has completed.
        /// This method invokes the <c>ReceiveOperationCompleted</c> method on all clients.
        /// </summary>
        /// <param name="completed">The <see cref="SignalROperationCompleted"/> DTO containing details about the completed operation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of sending the notification.</returns>
        /// <remarks>
        /// Similar to progress updates, consider targeted notifications for scalability if applicable.
        /// </remarks>
        public async Task NotifyOperationCompletedAsync(SignalROperationCompleted completed)
        {
            _logger.LogDebug("Notifying all GUI clients of OperationCompleted for OpId: {OperationId}, Status: {FinalStatus}", 
                completed.OperationId, completed.FinalStatus);
            // TODO: Consider sending only to clients interested in this specific operation.
            await _guiHubContext.Clients.All.ReceiveOperationCompleted(completed);
        }

        /// <summary>
        /// Notifies all connected GUI clients that a new audit log entry has been added.
        /// This method invokes the <c>ReceiveAuditLogEntry</c> method on all clients.
        /// </summary>
        /// <param name="entry">The <see cref="AuditLogEntry"/> DTO that was added.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of sending the notification.</returns>
        public async Task NotifyAuditLogEntryAddedAsync(AuditLogEntry entry)
        {
            _logger.LogDebug("Notifying all GUI clients of new AuditLogEntry: Id={AuditId}, OperationType={OperationType}, User={User}", 
                entry.Id, entry.OperationType, entry.User);
            await _guiHubContext.Clients.All.ReceiveAuditLogEntry(entry);
        }

        /// <summary>
        /// Notifies all connected GUI clients that the Master service is about to shut down.
        /// This method invokes the <c>ReceiveMasterGoingDown</c> method on all clients.
        /// </summary>
        /// <param name="info">The <see cref="SignalRMasterGoingDown"/> DTO containing the reason and details for the shutdown.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of sending the notification.</returns>
        public async Task NotifyMasterGoingDownAsync(SignalRMasterGoingDown info)
        {
            _logger.LogInformation("Notifying all GUI clients that Master is going down. Reason: {Reason}, Message: {Message}", 
                info.Reason, info.Message);
            await _guiHubContext.Clients.All.ReceiveMasterGoingDown(info);
        }

        /// <summary>
        /// Notifies all connected GUI clients that the Master service has reconnected (e.g., after a restart).
        /// This method invokes the <c>MasterReconnected</c> method on all clients.
        /// </summary>
        /// <param name="reconnectedNotification">The <see cref="SignalRMasterReconnected"/> DTO with the reconnection message.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of sending the notification.</returns>
        public async Task NotifyMasterReconnectedAsync(SignalRMasterReconnected reconnectedNotification)
        {
            _logger.LogInformation("Notifying all GUI clients that Master has reconnected. Message: {Message}", reconnectedNotification.Message);
            await _guiHubContext.Clients.All.MasterReconnected(reconnectedNotification);
        }

        /// <summary>
        /// Notifies all connected GUI clients that the active environment manifest has been updated.
        /// This method invokes the <c>EnvironmentManifestUpdated</c> method on all clients.
        /// </summary>
        /// <param name="newManifest">The new <see cref="PureManifest"/> DTO.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of sending the notification.</returns>
        public async Task NotifyEnvironmentManifestUpdatedAsync(PureManifest newManifest)
        {
            _logger.LogInformation("Notifying UI: Environment Manifest Updated for {EnvironmentName} to version {VersionId}", newManifest.EnvironmentName, newManifest.VersionId);
            await _guiHubContext.Clients.All.EnvironmentManifestUpdated(newManifest);
        }

        /// <summary>
        /// Notifies all connected GUI clients about a newly found health check issue.
        /// This method invokes the <c>HealthCheckIssueFound</c> method on all clients.
        /// </summary>
        /// <param name="issue">The <see cref="HealthCheckIssue"/> DTO describing the problem.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of sending the notification.</returns>
        public async Task NotifyHealthCheckIssueFoundAsync(HealthCheckIssue issue)
        {
            _logger.LogWarning("Notifying UI (All): Health Check Issue Found. Source: {Source}, Check: {CheckName}, Severity: {Severity}", issue.Source, issue.CheckName, issue.Severity);
            await _guiHubContext.Clients.All.HealthCheckIssueFound(issue);
        }

        /// <summary>
        /// Notifies all connected GUI clients about a new log entry related to an ongoing operation.
        /// This method constructs a <see cref="SignalROperationLogEntry"/> from the provided <see cref="SlaveTaskLogEntry"/>
        /// and then invokes the <c>ReceiveOperationLogEntry</c> method on all clients.
        /// </summary>
        /// <param name="logEntry">The <see cref="SlaveTaskLogEntry"/> containing the log details from a slave agent.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of sending the notification.</returns>
        public async Task NotifyOperationLogEntryAsync(SlaveTaskLogEntry logEntry)
        {
            _logger.LogTrace("Notifying UI: Operation Log Entry received for OpId: {OperationId}, TaskId: {TaskId}", logEntry.OperationId, logEntry.TaskId);
            var dto = new SignalROperationLogEntry // Mapping from SlaveTaskLogEntry to SignalROperationLogEntry for GUI clients
            {
                OperationId = logEntry.OperationId,
                TaskId = logEntry.TaskId,
                NodeName = logEntry.NodeName,
                TimestampUtc = logEntry.TimestampUtc,
                LogLevel = logEntry.LogLevel,
                Message = logEntry.LogMessage
            };
            await _guiHubContext.Clients.All.ReceiveOperationLogEntry(dto);
        }

        /// <summary>
        /// Sends a test response message back to a specific GUI client, identified by their SignalR connection ID.
        /// This method invokes the <c>ReceiveServerToClientTestResponse</c> method on the specified client.
        /// </summary>
        /// <param name="connectionId">The SignalR connection ID of the client that sent the initial test request.</param>
        /// <param name="response">The <see cref="SignalRServerToClientTestResponse"/> DTO to send.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of sending the notification.</returns>
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