using SiteKeeper.Shared.DTOs.SignalR;
using System.Threading.Tasks;

namespace SiteKeeper.Shared.Abstractions.GuiHub
{
    /// <summary>
    /// Defines the contract for methods that a GUI client can invoke on the Master's GuiHub (server).
    /// </summary>
    /// <remarks>
    /// The Master Agent's GuiHub will implement these methods to receive requests and commands from GUI clients.
    /// This interface is used for typed SignalR Hubs.
    /// Based on "SiteKeeper Minimal API & SignalR Hub Handlers.md" and typical GUI interaction patterns.
    /// </remarks>
    public interface IGuiHubClient
    {
        /// <summary>
        /// Allows a GUI client to send a test message to the server.
        /// The server is expected to respond via <see cref="IGuiHub.ReceiveServerToClientTestResponse"/>.
        /// </summary>
        /// <param name="request">The test request payload.</param>
        /// <returns>A task representing the asynchronous handling of the test request.</returns>
        Task ClientToServerTest(SignalRClientToServerTestRequest request);

        /// <summary>
        /// Allows a GUI client to request the current full environment status upon connection or refresh.
        /// The server would respond by sending various status update messages via <see cref="IGuiHub"/> methods.
        /// </summary>
        /// <returns>A task representing the asynchronous handling of the request.</returns>
        Task RequestFullEnvironmentStatus();

        // Other methods GUI might invoke:
        // - Subscribing/unsubscribing from specific types of notifications (if granular control is needed).
        // - Sending user-specific settings or preferences.
    }
} 