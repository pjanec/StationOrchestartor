using SiteKeeper.Shared.DTOs.API.AuditLog; // Now for AuditLogEntry
using SiteKeeper.Shared.DTOs.SignalR;
using System.Threading.Tasks;
using SiteKeeper.Shared.DTOs.API.Diagnostics;
using SiteKeeper.Shared.DTOs.API.Environment;

namespace SiteKeeper.Shared.Abstractions.GuiHub
{
    /// <summary>
    /// Defines the contract for methods that the Master Hub (server-side) can invoke on connected GUI clients (client-side).
    /// GUI clients implement or subscribe to these methods to receive real-time updates, notifications, and data pushes from the Master.
    /// </summary>
    /// <remarks>
    /// This interface is primarily used for SignalR communication with the GuiHub, enabling a reactive user interface.
    /// Documentation based on "SiteKeeper Minimal API & SignalR Hub Handlers.md".
    /// </remarks>
    public interface IGuiHub
    {
        /// <summary>
        /// Called by the Master to notify connected GUI clients about a change in a specific node's (Agent's) status.
        /// </summary>
        /// <param name="update">Data transfer object containing the updated node status information.</param>
        Task ReceiveNodeStatusUpdate(SignalRNodeStatusUpdate update);

        /// <summary>
        /// Called by the Master to notify connected GUI clients about a change in a specific managed application's status.
        /// </summary>
        /// <param name="update">Data transfer object containing the updated application status information.</param>
        Task ReceiveAppStatusUpdate(SignalRAppStatusUpdate update);

        /// <summary>
        /// Called by the Master to notify connected GUI clients about a change in an application deployment plan's status.
        /// </summary>
        /// <param name="update">Data transfer object containing the updated plan status information.</param>
        Task ReceivePlanStatusUpdate(SignalRPlanStatusUpdate update);

        /// <summary>
        /// Called by the Master to notify connected GUI clients about a change in the overall system software's status (e.g., Master, Agents).
        /// </summary>
        /// <param name="update">Data transfer object containing the updated system software status.</param>
        Task ReceiveSystemSoftwareStatusUpdate(SignalRSystemSoftwareStatusUpdate update);

        /// <summary>
        /// Called by the Master to send progress updates for an ongoing, long-running operation to connected GUI clients.
        /// </summary>
        /// <param name="progress">Data transfer object containing details about the operation's progress.</param>
        Task ReceiveOperationProgress(SignalROperationProgress progress);

        /// <summary>
        /// Called by the Master to notify connected GUI clients that a specific operation has completed.
        /// </summary>
        /// <param name="completed">Data transfer object containing information about the completed operation, including its final status.</param>
        Task ReceiveOperationCompleted(SignalROperationCompleted completed);

        /// <summary>
        /// Called by the Master to push a new audit log entry to connected GUI clients in real-time.
        /// </summary>
        /// <param name="entry">Data transfer object representing the audit log entry.</param>
        Task ReceiveAuditLogEntry(AuditLogEntry entry);

        /// <summary>
        /// Called by the Master to notify connected GUI clients that the Master service is shutting down.
        /// </summary>
        /// <param name="info">Data transfer object containing information about the shutdown, if any.</param>
        Task ReceiveMasterGoingDown(SignalRMasterGoingDown info);

        /// <summary>
        /// Called by the Master shortly after it (re)starts and comes online.
        /// This helps GUI clients to confirm re-established communication and potentially trigger re-synchronization.
        /// </summary>
        /// <param name="reconnectedNotification">Data transfer object indicating the Master is back online.</param>
        Task MasterReconnected(SignalRMasterReconnected reconnectedNotification);

        /// <summary>
        /// Called by the Master to inform connected GUI clients that the active "pure" environment manifest has been updated.
        /// Clients might use this to refresh their display or understanding of the environment structure.
        /// </summary>
        /// <param name="newManifest">The new pure environment manifest.</param>
        Task EnvironmentManifestUpdated(PureManifest newManifest);

        /// <summary>
        /// Called by the Master to push individual health check issues to connected GUI clients as they are identified during a diagnostic run.
        /// </summary>
        /// <param name="issue">Data transfer object describing a specific health check issue.</param>
        Task HealthCheckIssueFound(HealthCheckIssue issue);

        /// <summary>
        /// Called by the Master to push a log entry related to an ongoing operation to connected GUI clients.
        /// This allows real-time log streaming for operations monitored by the GUI.
        /// </summary>
        /// <param name="logEntry">Data transfer object containing the operation log entry details.</param>
        Task ReceiveOperationLogEntry(SignalROperationLogEntry logEntry);

        /// <summary>
        /// Called by the Master to send a response to a test message initiated by a GUI client (e.g., via `SendClientToServerTestMessage` on `IGuiHubClient`).
        /// </summary>
        /// <param name="response">Data transfer object containing the server's response to the client's test message.</param>
        Task ReceiveServerToClientTestResponse(SignalRServerToClientTestResponse response);
    }
} 