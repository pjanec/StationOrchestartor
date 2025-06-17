using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Targets;
using SiteKeeper.Master.Services.NLog2;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Services
{
    /// <summary>
    /// A hosted service responsible for programmatically configuring the custom NLog UILoggingTarget.
    /// It ensures that the target is registered with NLog's configuration when the application starts.
    /// It also acts as a singleton provider for the target instance, allowing other services
    /// to access it, for example, to call its FlushAsync method.
    /// </summary>
    public class MasterNLogSetupService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MasterNLogSetupService> _logger;
        private UILoggingTarget? _uiLoggingTarget;

        public MasterNLogSetupService(IServiceProvider serviceProvider, ILogger<MasterNLogSetupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Called when the application host is starting. This method sets up the NLog target.
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Ensuring NLog 'UILoggingTarget' is configured...");
            var config = LogManager.Configuration ?? new LoggingConfiguration();

            if (config.FindTargetByName("ui-forwarder") is not UILoggingTarget)
            {
                _logger.LogInformation("Programmatically adding 'UILoggingTarget' as 'ui-forwarder'.");

                _uiLoggingTarget = new UILoggingTarget(_serviceProvider) { Name = "ui-forwarder" };

                // setup MLDC captured properties
                _uiLoggingTarget.ContextProperties.Add(new TargetPropertyWithContext
                {
                    Name = "MasterActionId",
                    Layout = "${mdlc:item=MasterActionId}"
                });

                config.AddTarget(_uiLoggingTarget);

                // This rule sends all logs from any logger with level Info or higher to our UI target.
                var rule = new LoggingRule("*", NLog.LogLevel.Info, _uiLoggingTarget);
                config.LoggingRules.Add(rule);

                LogManager.Configuration = config;
            }
            else
            {
                _uiLoggingTarget = config.FindTargetByName<UILoggingTarget>("ui-forwarder");
                _logger.LogInformation("'UILoggingTarget' named 'ui-forwarder' was already present.");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the application host is performing a graceful shutdown.
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>
        /// Provides access to the singleton instance of the UILoggingTarget.
        /// </summary>
        /// <returns>The configured UILoggingTarget instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the target has not been initialized yet.</exception>
        public UILoggingTarget GetUiLoggingTarget()
        {
            if (_uiLoggingTarget == null)
                throw new InvalidOperationException("UILoggingTarget has not been initialized. Ensure MasterNLogSetupService has started.");
            return _uiLoggingTarget;
        }
    }
} 