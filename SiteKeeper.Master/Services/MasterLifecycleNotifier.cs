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
    /// An IHostedService that triggers notifications at key points in the Master application's lifecycle.
    /// </summary>
    public class MasterLifecycleNotifier : IHostedService
    {
        private readonly IGuiNotifier _guiNotifierService;
        private readonly ILogger<MasterLifecycleNotifier> _logger;

        public MasterLifecycleNotifier(IGuiNotifier guiNotifierService, [FromServices] ILogger<MasterLifecycleNotifier> logger)
        {
            _guiNotifierService = guiNotifierService ?? throw new ArgumentNullException(nameof(guiNotifierService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Called when the application host is ready to start the service.
        /// Sends the MasterReconnected SignalR message.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("MasterLifecycleNotifierService started. Master WebApplication is up.");
            // Send MasterReconnected message to inform UIs that the server is online.
            var reconnectDto = new SignalRMasterReconnected { Message = "SiteKeeper Master Agent successfully (re)started and is online." };
            await _guiNotifierService.NotifyMasterReconnectedAsync(reconnectDto);
        }

        /// <summary>
        /// Triggered when the application host is performing a graceful shutdown.
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("MasterLifecycleNotifierService stopping.");
            return Task.CompletedTask;
        }
    }
} 