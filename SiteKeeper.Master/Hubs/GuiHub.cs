using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Services; // For IGuiNotifierService if used directly, or other services
using SiteKeeper.Shared.Abstractions.GuiHub;
using SiteKeeper.Shared.DTOs.SignalR;
using System; // For DateTime, ArgumentNullException
using System.Threading.Tasks;

namespace SiteKeeper.Master.Hubs
{
    /// <summary>
    /// SignalR Hub for real-time communication between the SiteKeeper Master Agent and connected GUI clients.
    /// This hub implements <see cref="IGuiHubClient"/> for methods callable by GUI clients,
    /// and uses <see cref="IGuiHub"/> (via <see cref="IHubContext{THub, TClient}"/>, often abstracted by <see cref="IGuiNotifierService"/>)
    /// to call methods on GUI clients.
    /// </summary>
    /// <remarks>
    /// GUI clients connect to this hub to receive real-time updates about system status, operation progress,
    /// audit logs, and other events. They can also invoke methods on this hub to request actions or information.
    /// Authentication (TODO: via [Authorize]) is expected for this hub.
    /// It interacts with services like <see cref="IGuiNotifierService"/> to dispatch messages to clients
    /// or to handle client requests that require backend processing.
    /// </remarks>
    // TODO: Add [Authorize] attribute once JWT authentication is fully set up for SignalR.
    // [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] 
    public class GuiHub : Hub<IGuiHub>, IGuiHubClient
    {
        private readonly ILogger<GuiHub> _logger;
        // private readonly IEnvironmentQueryService _environmentQueryService; // Example: For RequestFullEnvironmentStatus, if directly called.
        private readonly IGuiNotifierService _guiNotifierService; // Used here for SendTestResponseAsync to ensure consistent message sending.

        /// <summary>
        /// Initializes a new instance of the <see cref="GuiHub"/> class.
        /// </summary>
        /// <param name="logger">Logger for hub activities.</param>
        /// <param name="guiNotifierService">Service used for sending notifications; here, specifically for sending test responses consistently.</param>
        /// <exception cref="ArgumentNullException">Thrown if logger or guiNotifierService is null.</exception>
        public GuiHub(
            ILogger<GuiHub> logger,
            IGuiNotifierService guiNotifierService
            )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _guiNotifierService = guiNotifierService ?? throw new ArgumentNullException(nameof(guiNotifierService));
            // _environmentQueryService = environmentQueryService; // If directly used
        }

        /// <summary>
        /// Called when a new GUI client connects to the hub.
        /// Logs the connection event including the connection ID and authenticated user identifier.
        /// </summary>
        /// <returns>A <see cref="Task"/> that represents the asynchronous connect event.</returns>
        /// <remarks>
        /// TODO: Implement addition of the connection to relevant SignalR groups if group-based messaging
        /// (e.g., for specific roles or subscriptions) is implemented.
        /// </remarks>
        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier ?? "anonymous"; // UserIdentifier typically comes from IUserIdProvider (e.g., ClaimTypes.NameIdentifier)
            _logger.LogInformation("GUI Client connected: ConnectionId={ConnectionId}, UserId={UserId}", Context.ConnectionId, userId);
            // TODO: Add user to any relevant groups if using group-based messaging for specific users/roles.
            // Example: await Groups.AddToGroupAsync(Context.ConnectionId, "Administrators");
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when a GUI client disconnects from the hub.
        /// Logs the disconnection event including the connection ID, authenticated user identifier, and any exception that occurred.
        /// </summary>
        /// <param name="exception">The <see cref="Exception"/> that occurred during disconnect, if any (null for normal disconnects).</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous disconnect event.</returns>
        /// <remarks>
        /// TODO: Implement removal of the connection from any SignalR groups it was part of.
        /// </remarks>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier ?? "anonymous";
            _logger.LogInformation("GUI Client disconnected: ConnectionId={ConnectionId}, UserId={UserId}. Reason: {ExceptionMessage}", 
                Context.ConnectionId, userId, exception?.Message ?? "Normal disconnect");
            // TODO: Remove user from any groups.
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Handles a test request sent from a GUI client.
        /// This method constructs a <see cref="SignalRServerToClientTestResponse"/> and uses the
        /// <see cref="IGuiNotifierService"/> to send this response back to the specific calling client.
        /// </summary>
        /// <param name="request">The <see cref="SignalRClientToServerTestRequest"/> DTO containing the client's test message and timestamp.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous processing of the test request.</returns>
        public async Task ClientToServerTest(SignalRClientToServerTestRequest request)
        {
            _logger.LogInformation("ClientToServerTest received from ConnectionId={ConnectionId}, UserId={UserIdentifier}. Message: '{RequestMessage}' at {RequestTimestamp}",
                Context.ConnectionId, Context.UserIdentifier ?? "anonymous", request.RequestMessage, request.RequestTimestamp);

            var response = new SignalRServerToClientTestResponse
            {
                OriginalRequestMessage = request.RequestMessage,
                ResponseMessage = $"Server received your message: '{request.RequestMessage}' successfully at {DateTime.UtcNow:O}!",
                ServerTimestamp = DateTime.UtcNow,
                ProcessingDurationMs = (long)(DateTime.UtcNow - request.RequestTimestamp).TotalMilliseconds
            };

            // Using IGuiNotifierService ensures consistent message dispatch logic, even for caller-specific responses.
            await _guiNotifierService.SendTestResponseAsync(Context.ConnectionId, response);

            _logger.LogInformation("Sent ServerToClientTestResponse to ConnectionId={ConnectionId}", Context.ConnectionId);
        }

        /// <summary>
        /// Handles a request from a GUI client to receive a full snapshot of the current environment status.
        /// </summary>
        /// <remarks>
        /// This method is intended to trigger a series of messages back to the calling client, providing a comprehensive
        /// overview of the environment. This would involve fetching data from various services (e.g., environment status,
        /// node statuses, application statuses, plan statuses, ongoing operations).
        /// The current placeholder implementation only sends a basic <see cref="SignalRSystemSoftwareStatusUpdate"/>
        /// with an "Unknown" status and logs a warning that the feature is not fully implemented.
        /// </remarks>
        /// <returns>A <see cref="Task"/> representing the asynchronous processing of the request.</returns>
        public async Task RequestFullEnvironmentStatus()
        {
            var userId = Context.UserIdentifier ?? "anonymous";
            _logger.LogInformation("RequestFullEnvironmentStatus received from ConnectionId={ConnectionId}, UserId={UserId}", Context.ConnectionId, userId);

            // TODO: Implement the logic to gather all necessary environment status information.
            // This would involve calling various services (e.g., IEnvironmentService, IOperationCoordinatorService, INodeHealthMonitorService)
            // and then sending multiple specific update messages to the Clients.Caller using IGuiHub methods like:
            // await Clients.Caller.ReceiveSystemSoftwareStatusUpdate(systemStatus);
            // foreach (var node in nodeList) { await Clients.Caller.ReceiveNodeStatusUpdate(node); }
            // foreach (var app in appList) { await Clients.Caller.ReceiveAppStatusUpdate(app); }
            // ... and so on for operations, plans.

            _logger.LogWarning("RequestFullEnvironmentStatus is not fully implemented. Caller {ConnectionId} will not receive a full update yet.", Context.ConnectionId);
            // Simulate sending a basic acknowledgement or a placeholder message if desired.
            await Clients.Caller.ReceiveSystemSoftwareStatusUpdate(new SignalRSystemSoftwareStatusUpdate { 
                OverallStatus = Shared.Enums.SystemSoftwareOverallStatus.Unknown, 
                // Consider adding a Message property to SignalRSystemSoftwareStatusUpdate if sending text like below is desired:
                // Message = "Full status refresh requested, but not yet fully implemented."
            });
        }
    }
} 