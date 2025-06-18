using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.DTOs.SignalR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Services
{
    /// <summary>
    /// An <see cref="IHostedService"/> that triggers notifications at key points in the Master application's lifecycle,
    /// primarily to inform GUI clients about the Master's availability.
    /// </summary>
    /// <remarks>
    /// The main responsibility of this service upon application startup (<see cref="StartAsync"/>) is to send a
    /// <see cref="SignalRMasterReconnected"/> message via the <see cref="IGuiNotifierService"/>. This allows
    /// connected or connecting GUI clients to know that the Master Agent is online and ready.
    /// </remarks>
    public class MasterLifecycleNotifierService : IHostedService
    {
        private readonly IGuiNotifierService _guiNotifierService;
        private readonly ILogger<MasterLifecycleNotifierService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MasterLifecycleNotifierService"/> class.
        /// </summary>
        /// <param name="guiNotifierService">The service responsible for sending notifications to GUI clients via SignalR.</param>
        /// <param name="logger">The logger for recording lifecycle events and service activity.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="guiNotifierService"/> or <paramref name="logger"/> is null.</exception>
        public MasterLifecycleNotifierService(IGuiNotifierService guiNotifierService, [FromServices] ILogger<MasterLifecycleNotifierService> logger)
        {
            _guiNotifierService = guiNotifierService ?? throw new ArgumentNullException(nameof(guiNotifierService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Called by the application host when the service is starting.
        /// This implementation logs the startup and sends a <see cref="SignalRMasterReconnected"/> notification
        /// to all connected GUI clients via the <see cref="IGuiNotifierService"/>.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to indicate if startup should be aborted.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous start operation.</returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("MasterLifecycleNotifierService started. Master WebApplication is up.");
            // Send MasterReconnected message to inform UIs that the server is online.
            var reconnectDto = new SignalRMasterReconnected { Message = "SiteKeeper Master Agent successfully (re)started and is online." };
            await _guiNotifierService.NotifyMasterReconnectedAsync(reconnectDto);
        }

        /// <summary>
        /// Called by the application host when the service is stopping, during a graceful shutdown.
        /// This implementation currently only logs the stopping event.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to indicate if shutdown should be quick or if long operations should be cancelled.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous stop operation.</returns>
        /// <remarks>
        /// A <see cref="SignalRMasterGoingDown"/> message could potentially be sent here for very graceful shutdowns,
        /// however, such messages are often handled more reliably by `IApplicationLifetime.ApplicationStopping`
        /// if an immediate broadcast before shutdown is critical.
        /// </remarks>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("MasterLifecycleNotifierService stopping.");
            return Task.CompletedTask;
        }
    }
} 