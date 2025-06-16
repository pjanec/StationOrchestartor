using SiteKeeper.Shared.DTOs.API.AuditLog; // Now for AuditLogEntry
using SiteKeeper.Shared.DTOs.SignalR;
using System.Threading.Tasks;
using SiteKeeper.Shared.DTOs.API.Diagnostics;
using SiteKeeper.Shared.DTOs.API.Environment;

namespace SiteKeeper.Shared.Abstractions.GuiHub
{
    /// <summary>
    /// Defines the contract for methods that the Master Hub (server) can invoke on a connected GUI client.
    /// </summary>
    /// <remarks>
    /// GUI clients implement or subscribe to these methods to receive real-time updates from the Master.
    /// This interface is used for typed SignalR Hubs (GuiHub).
    /// Based on "SiteKeeper Minimal API & SignalR Hub Handlers.md".
    /// </remarks>
    public interface IGuiHub
    {
        /// <summary>
        /// Notifies the client about a change in a specific node's status.
        /// </summary>
        Task ReceiveNodeStatusUpdate(SignalRNodeStatusUpdate update);

        /// <summary>
        /// Notifies the client about a change in a specific application's status.
        /// </summary>
        Task ReceiveAppStatusUpdate(SignalRAppStatusUpdate update);

        /// <summary>
        /// Notifies the client about a change in an application plan's status.
        /// </summary>
        Task ReceivePlanStatusUpdate(SignalRPlanStatusUpdate update);

        /// <summary>
        /// Notifies the client about a change in the overall system software status.
        /// </summary>
        Task ReceiveSystemSoftwareStatusUpdate(SignalRSystemSoftwareStatusUpdate update);

        /// <summary>
        /// Sends progress updates for an ongoing operation to the client.
        /// </summary>
        Task ReceiveOperationProgress(SignalROperationProgress progress);

        /// <summary>
        /// Notifies the client that an operation has completed.
        /// </summary>
        Task ReceiveOperationCompleted(SignalROperationCompleted completed);

        /// <summary>
        /// Notifies the client that a new audit log entry has been added.
        /// The client will receive an AuditLogEntry DTO.
        /// </summary>
        Task ReceiveAuditLogEntry(AuditLogEntry entry);

        /// <summary>
        /// Notifies the client that the Master service is shutting down.
        /// </summary>
        Task ReceiveMasterGoingDown(SignalRMasterGoingDown info);

        /// <summary>
        /// Sent by the server shortly after it comes back online.
        /// Helps clients confirm re-established full communication.
        /// </summary>
        Task MasterReconnected(SignalRMasterReconnected reconnectedNotification);

        /// <summary>
        /// Informs the client that the active "pure" environment manifest has changed.
        /// </summary>
        Task EnvironmentManifestUpdated(PureManifest newManifest);

        /// <summary>
        /// Pushes individual health check issues as they are found during a diagnostic run.
        /// </summary>
        Task HealthCheckIssueFound(HealthCheckIssue issue);

        /// <summary>
        /// Pushes a log entry related to an ongoing operation to the client.
        /// </summary>
        Task ReceiveOperationLogEntry(SignalROperationLogEntry logEntry);

        /// <summary>
        /// Sends a response from the server to a client-initiated test message.
        /// </summary>
        Task ReceiveServerToClientTestResponse(SignalRServerToClientTestResponse response);
    }
} 