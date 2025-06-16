using SiteKeeper.Shared.DTOs.API.AuditLog;
using SiteKeeper.Shared.DTOs.SignalR;
using System.Threading.Tasks;
using SiteKeeper.Shared.DTOs.API.Environment;
using SiteKeeper.Shared.DTOs.API.Diagnostics;
using SiteKeeper.Shared.DTOs.AgentHub;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Service interface for sending real-time notifications to connected GUI clients.
    /// </summary>
    /// <remarks>
    /// This service abstracts the underlying SignalR communication (via GuiHub) and provides
    /// strongly-typed methods for other Master services (e.g., OperationCoordinatorService,
    /// NodeHealthMonitorService, AuditLogService) to broadcast updates to the UI.
    /// The methods generally correspond to client-side methods defined in <see cref="Shared.Abstractions.GuiHub.IGuiHub"/>.
    /// </remarks>
    public interface IGuiNotifierService
    {
        /// <summary>
        /// Notifies all GUI clients about a change in a specific node's status.
        /// </summary>
        Task NotifyNodeStatusUpdateAsync(SignalRNodeStatusUpdate update);

        /// <summary>
        /// Notifies all GUI clients about a change in a specific application's status.
        /// </summary>
        Task NotifyAppStatusUpdateAsync(SignalRAppStatusUpdate update);

        /// <summary>
        /// Notifies all GUI clients about a change in an application plan's status.
        /// </summary>
        Task NotifyPlanStatusUpdateAsync(SignalRPlanStatusUpdate update);

        /// <summary>
        /// Notifies all GUI clients about a change in the overall system software status.
        /// </summary>
        Task NotifySystemSoftwareStatusUpdateAsync(SignalRSystemSoftwareStatusUpdate update);

        /// <summary>
        /// Sends progress updates for an ongoing operation to relevant GUI clients.
        /// (Could be all clients, or clients subscribed to the specific operation).
        /// </summary>
        Task NotifyOperationProgressAsync(SignalROperationProgress progress);

        /// <summary>
        /// Notifies relevant GUI clients that an operation has completed.
        /// </summary>
        Task NotifyOperationCompletedAsync(SignalROperationCompleted completed);

        /// <summary>
        /// Notifies all GUI clients that a new audit log entry has been added.
        /// </summary>
        Task NotifyAuditLogEntryAddedAsync(AuditLogEntry entry);

        /// <summary>
        /// Notifies all GUI clients that the Master service is shutting down.
        /// </summary>
        Task NotifyMasterGoingDownAsync(SignalRMasterGoingDown info);

        /// <summary>
        /// Notifies all GUI clients that the Master service has reconnected.
        /// </summary>
        Task NotifyMasterReconnectedAsync(SignalRMasterReconnected reconnectedNotification);

        /// <summary>
        /// Notifies all GUI clients that the environment manifest has been updated.
        /// </summary>
        Task NotifyEnvironmentManifestUpdatedAsync(PureManifest newManifest);

        /// <summary>
        /// Notifies GUI clients about a new health check issue.
        /// </summary>
        Task NotifyHealthCheckIssueFoundAsync(HealthCheckIssue issue);

        /// <summary>
        /// Notifies GUI clients about a new log entry for an ongoing operation.
        /// </summary>
        Task NotifyOperationLogEntryAsync(SlaveTaskLogEntry logEntry);

        /// <summary>
        /// Sends a test response message back to a specific GUI client.
        /// </summary>
        /// <param name="connectionId">The connection ID of the client that sent the test request.</param>
        /// <param name="response">The response DTO.</param>
        Task SendTestResponseAsync(string connectionId, SignalRServerToClientTestResponse response);
    }
} 