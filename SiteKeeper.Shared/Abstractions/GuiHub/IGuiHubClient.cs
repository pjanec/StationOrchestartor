using SiteKeeper.Shared.DTOs.SignalR;
using System.Threading.Tasks;

namespace SiteKeeper.Shared.Abstractions.GuiHub
{
    /// <summary>
    /// Defines the contract for methods that a GUI client (client-side) can invoke on the Master's GuiHub (server-side).
    /// The Master's GuiHub implements this interface to receive requests, commands, or messages from connected GUI clients.
    /// </summary>
    /// <remarks>
    /// This interface is primarily used for SignalR communication, where GUI clients initiate actions or requests to the Master.
    /// Documentation based on "SiteKeeper Minimal API & SignalR Hub Handlers.md" and typical GUI interaction patterns.
    /// </remarks>
    public interface IGuiHubClient
    {
        /// <summary>
        /// Called by a GUI client to send a test message to the Master's GuiHub.
        /// The Master is expected to process this message and respond via the <see cref="IGuiHub.ReceiveServerToClientTestResponse"/> method on the calling client.
        /// </summary>
        /// <param name="request">Data transfer object containing the payload for the test message.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous invocation of the method on the server.</returns>
        Task ClientToServerTest(SignalRClientToServerTestRequest request);

        /// <summary>
        /// Called by a GUI client, typically upon initial connection or when a manual refresh is requested,
        /// to ask the Master for the current, comprehensive status of the entire managed environment.
        /// The Master would then respond by invoking various methods on <see cref="IGuiHub"/> (e.g.,
        /// <see cref="IGuiHub.ReceiveNodeStatusUpdate"/>, <see cref="IGuiHub.ReceiveAppStatusUpdate"/>, etc.)
        /// on the calling client to provide this information.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous invocation of the method on the server.</returns>
        Task RequestFullEnvironmentStatus();

        // Other methods GUI might invoke:
        // - Subscribing/unsubscribing from specific types of notifications (if granular control is needed).
        // - Sending user-specific settings or preferences.
    }
} 