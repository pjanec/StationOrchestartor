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
    /// An <see cref="IHostedService"/> responsible for programmatically configuring the custom NLog <see cref="UILoggingTarget"/>.
    /// </summary>
    /// <remarks>
    /// This service ensures that the <see cref="UILoggingTarget"/>, which forwards logs to GUI clients via SignalR,
    /// is registered with NLog's configuration when the application starts. It also acts as a singleton provider
    /// for the <see cref="UILoggingTarget"/> instance, allowing other services (such as <see cref="MasterActionCoordinatorService"/>)
    /// to access it, for example, to call its <see cref="UILoggingTarget.FlushAsync(System.Action{System.Exception})"/> method.
    /// This setup is crucial for contextual logging within Master Actions.
    /// </remarks>
    public class MasterNLogSetupService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MasterNLogSetupService> _logger;
        private UILoggingTarget? _uiLoggingTarget;

        /// <summary>
        /// Initializes a new instance of the <see cref="MasterNLogSetupService"/> class.
        /// </summary>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to resolve services, particularly for the <see cref="UILoggingTarget"/> constructor.</param>
        /// <param name="logger">The logger for recording service activities and potential configuration errors.</param>
        public MasterNLogSetupService(IServiceProvider serviceProvider, ILogger<MasterNLogSetupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Called by the application host when the service is starting.
        /// This method ensures the <see cref="UILoggingTarget"/> (named "ui-forwarder") is configured in NLog.
        /// If the target does not exist, it is created, configured with context properties (like "MasterActionId" from MDLC),
        /// and added to the <see cref="LogManager.Configuration"/> along with a rule to direct logs to it.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to indicate if startup should be aborted (not currently used by this method).</param>
        /// <returns>A <see cref="Task"/> that represents the completion of the NLog setup.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Ensuring NLog 'UILoggingTarget' is configured...");
            var config = LogManager.Configuration ?? new LoggingConfiguration();

            if (config.FindTargetByName("ui-forwarder") is not UILoggingTarget)
            {
                _logger.LogInformation("Programmatically adding 'UILoggingTarget' as 'ui-forwarder'.");

                _uiLoggingTarget = new UILoggingTarget(_serviceProvider) { Name = "ui-forwarder" };

                // setup MLDC captured properties, e.g., MasterActionId, to be included with log events sent to UI.
                _uiLoggingTarget.ContextProperties.Add(new TargetPropertyWithContext
                {
                    Name = "MasterActionId", // This name must match the key used with MappedDiagnosticsLogicalContext.SetScoped
                    Layout = "${mdlc:item=MasterActionId}"
                });
                // Add other MDLC properties if needed, e.g., "${mdlc:item=StageName}"

                config.AddTarget(_uiLoggingTarget);

                // This rule sends all logs from any logger with level Info or higher to our UI target.
                // Adjust log level as needed (e.g., LogLevel.Debug for more verbose UI logging).
                var rule = new LoggingRule("*", NLog.LogLevel.Info, _uiLoggingTarget);
                config.LoggingRules.Add(rule);

                LogManager.Configuration = config;
                _logger.LogInformation("'UILoggingTarget' configured and rule added.");
            }
            else
            {
                _uiLoggingTarget = config.FindTargetByName<UILoggingTarget>("ui-forwarder");
                _logger.LogInformation("'UILoggingTarget' named 'ui-forwarder' was already present in NLog configuration.");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Called by the application host when the service is stopping, during a graceful shutdown.
        /// This implementation currently performs no specific actions.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to indicate if shutdown should be quick.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous stop operation (currently, an already completed task).</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("MasterNLogSetupService stopping. No specific NLog teardown actions implemented.");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Provides access to the singleton instance of the configured <see cref="UILoggingTarget"/>.
        /// This allows other services to interact with the target directly, for instance, to trigger a log flush.
        /// </summary>
        /// <returns>The configured <see cref="UILoggingTarget"/> instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if this method is called before the <see cref="StartAsync"/> method has completed
        /// and the <see cref="UILoggingTarget"/> has not been initialized.</exception>
        /// <remarks>
        /// This method is used by services like <see cref="MasterActionCoordinatorService"/> to obtain a reference
        /// to the target for flushing logs at critical points in workflow execution.
        /// </remarks>
        public UILoggingTarget GetUiLoggingTarget()
        {
            if (_uiLoggingTarget == null)
                throw new InvalidOperationException("UILoggingTarget has not been initialized. Ensure MasterNLogSetupService.StartAsync has been called and completed.");
            return _uiLoggingTarget;
        }
    }
}
