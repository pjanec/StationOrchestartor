using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Services; // For IGuiNotifierService if used directly, or other services
using SiteKeeper.Shared.Abstractions.GuiHub;
using SiteKeeper.Shared.DTOs.SignalR;
using System; // For DateTime, ArgumentNullException
using System.Threading.Tasks;

namespace SiteKeeper.Master.Hubs
{
    // TODO: Add [Authorize] attribute once JWT authentication is fully set up for SignalR.
    // [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] 
    public class GuiHub : Hub<IGuiHub>, IGuiHubClient
    {
        private readonly ILogger<GuiHub> _logger;
        // private readonly IEnvironmentQueryService _environmentQueryService; // Example: For RequestFullEnvironmentStatus
        private readonly IGuiNotifier _guiNotifierService; // To send the test response

        public GuiHub(
            ILogger<GuiHub> logger,
            // IEnvironmentQueryService environmentQueryService,
            IGuiNotifier guiNotifierService
            )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            // _environmentQueryService = environmentQueryService ?? throw new ArgumentNullException(nameof(environmentQueryService));
            _guiNotifierService = guiNotifierService ?? throw new ArgumentNullException(nameof(guiNotifierService));
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier ?? "anonymous"; // UserIdentifier comes from IUserIdProvider, often mapped from ClaimsPrincipal
            _logger.LogInformation("GUI Client connected: ConnectionId={ConnectionId}, UserId={UserId}", Context.ConnectionId, userId);
            // TODO: Add user to any relevant groups if using group-based messaging for specific users/roles.
            // Example: await Groups.AddToGroupAsync(Context.ConnectionId, "Administrators");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier ?? "anonymous";
            _logger.LogInformation("GUI Client disconnected: ConnectionId={ConnectionId}, UserId={UserId}. Reason: {ExceptionMessage}", 
                Context.ConnectionId, userId, exception?.Message ?? "Normal disconnect");
            // TODO: Remove user from any groups.
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Handles a test request from a GUI client and sends a response back.
        /// </summary>
        public async Task ClientToServerTest(SignalRClientToServerTestRequest request)
        {
            _logger.LogInformation("ClientToServerTest received from ConnectionId={ConnectionId}, UserId={UserIdentifier}. Message: '{RequestMessage}' at {RequestTimestamp}",
                Context.ConnectionId, Context.UserIdentifier ?? "anonymous", request.RequestMessage, request.RequestTimestamp);

            var response = new SignalRServerToClientTestResponse
            {
                OriginalRequestMessage = request.RequestMessage,
                ResponseMessage = $"Server received your message: '{request.RequestMessage}' successfully!",
                ServerTimestamp = DateTime.UtcNow,
                ProcessingDurationMs = (long)(DateTime.UtcNow - request.RequestTimestamp).TotalMilliseconds // Simplified, actual processing time is minimal here
            };

            // Send response back to the specific caller using the IGuiNotifierService
            // This ensures a consistent way of sending messages, even if it's just to the caller.
            // Alternatively, could use: await Clients.Caller.ReceiveServerToClientTestResponse(response);
            await _guiNotifierService.SendTestResponseAsync(Context.ConnectionId, response);

            _logger.LogInformation("Sent ServerToClientTestResponse to ConnectionId={ConnectionId}", Context.ConnectionId);
        }

        /// <summary>
        /// Handles a request from a GUI client to receive the full current environment status.
        /// </summary>
        public async Task RequestFullEnvironmentStatus()
        {
            var userId = Context.UserIdentifier ?? "anonymous";
            _logger.LogInformation("RequestFullEnvironmentStatus received from ConnectionId={ConnectionId}, UserId={UserId}", Context.ConnectionId, userId);

            // TODO: Implement the logic to gather all necessary environment status information.
            // This would involve calling various services (e.g., MasterActionCoordinatorService, NodeHealthMonitorService,
            // potentially a dedicated EnvironmentSummaryService) and then sending multiple specific update messages
            // to the Clients.Caller (e.g., using IGuiHub methods like ReceiveNodeStatusUpdate, ReceiveOperationCompleted etc.).
            // For example:
            // var environmentStatus = await _environmentQueryService.GetCurrentEnvironmentSummaryAsync();
            // await Clients.Caller.ReceiveSystemSoftwareStatusUpdate(environmentStatus.SystemSoftwareStatus);
            // foreach (var node in environmentStatus.Nodes) { await Clients.Caller.ReceiveNodeStatusUpdate(node); }
            // ... and so on for operations, apps, plans.

            _logger.LogWarning("RequestFullEnvironmentStatus is not fully implemented. Caller {ConnectionId} will not receive a full update yet.", Context.ConnectionId);
            // Simulate sending a basic acknowledgement or a placeholder message if desired.
            await Clients.Caller.ReceiveSystemSoftwareStatusUpdate(new SignalRSystemSoftwareStatusUpdate { 
                OverallStatus = Shared.Enums.SystemSoftwareOverallStatus.Unknown, 
                //Message = "Full status refresh requested, but not yet fully implemented."
            });
        }
    }
} 