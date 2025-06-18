using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Targets;
using System.Threading;
using System.Threading.Tasks;

namespace SiteKeeper.Slave.Services.NLog2
{
    /// <summary>
    /// An IHostedService that runs at application startup to ensure the 
    /// SiteKeeperMasterBoundTarget is configured in NLog.
    /// This approach guarantees the target is available in all run modes
    /// (console, service, integration test) without duplicating code.
    /// </summary>
    public class NLogTargetAssuranceService : IHostedService
    {
        private readonly ILogger<NLogTargetAssuranceService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NLogTargetAssuranceService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for this service, used for logging its actions.</param>
        public NLogTargetAssuranceService(ILogger<NLogTargetAssuranceService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Called when the application host is starting. This method ensures that the
        /// <see cref="SiteKeeperMasterBoundTarget"/> is configured in NLog.
        /// </summary>
        /// <remarks>
        /// The method performs the following steps:
        /// 1. Checks if NLog configuration exists.
        /// 2. Verifies if a target named "masterBoundTarget" of type <see cref="SiteKeeperMasterBoundTarget"/>
        ///    is already registered in the NLog configuration.
        /// 3. If the target is not found:
        ///    a. Creates a new instance of <see cref="SiteKeeperMasterBoundTarget"/>, setting its name to "masterBoundTarget"
        ///       and defining a layout (e.g., "${longdate}|${level:uppercase=true}|[SlaveTaskLog] ${message}").
        ///    b. Configures the target to include Mapped Diagnostics Logical Context (MDLC) properties:
        ///       "SK-OperationId", "SK-TaskId", and "SK-NodeName", which are crucial for contextualizing logs sent to the master.
        ///    c. Adds the newly created target to the NLog configuration.
        ///    d. Creates a new <see cref="LoggingRule"/> that directs logs from sources starting with
        ///       <see cref="SiteKeeperMasterBoundTarget.ExecutiveLogPrefix"/> (e.g., "Executive.*")
        ///       at <see cref="NLog.LogLevel.Debug"/> or higher to the "masterBoundTarget".
        ///    e. Inserts this rule at the beginning of the logging rules collection to ensure it's processed with high priority.
        ///    f. Applies the changes to the NLog configuration by calling <see cref="LogManager.ReconfigExistingLoggers()"/>.
        /// 4. Logs whether the target was added or if it was already present.
        /// </remarks>
        /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous Start operation.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Ensuring NLog 'SiteKeeperMasterBoundTarget' is configured...");

            var config = LogManager.Configuration;
            if (config == null)
            {
                _logger.LogWarning("NLog configuration is null. Cannot ensure master-bound target exists.");
                return Task.CompletedTask;
            }

            // Check if a target with this name already exists.
            if (config.FindTargetByName<SiteKeeperMasterBoundTarget>("masterBoundTarget") == null)
            {
                _logger.LogInformation("Programmatically adding 'SiteKeeperMasterBoundTarget' as it was not found in the loaded configuration.");

                // 1. Create an instance of our custom target.
                var masterTarget = new SiteKeeperMasterBoundTarget
                {
                    Name = "masterBoundTarget",
                    Layout = "${longdate}|${level:uppercase=true}|[SlaveTaskLog] ${message}"
                };

                // setup MLDC captured properties

                masterTarget.ContextProperties.Add(new TargetPropertyWithContext
                {
                    Name = "SK-OperationId",
                    Layout = "${mdlc:item=SK-OperationId}"
                });
                masterTarget.ContextProperties.Add(new TargetPropertyWithContext
                {
                    Name = "SK-TaskId",
                    Layout = "${mdlc:item=SK-TaskId}"
                });
                masterTarget.ContextProperties.Add(new TargetPropertyWithContext
                {
                    Name = "SK-NodeName",
                    Layout = "${mdlc:item=SK-NodeName}"
                });

                config.AddTarget(masterTarget);

                // 2. Create a rule to send logs from "Executive.*" to this new target.
                var rule = new LoggingRule($"{SiteKeeperMasterBoundTarget.ExecutiveLogPrefix}.*", NLog.LogLevel.Debug, masterTarget);

                // Insert the rule at the beginning (index 0) to ensure it is evaluated 
                // before any other potentially "final" rules.
                config.LoggingRules.Insert(0, rule);

                // 3. Re-apply the modified configuration to the current LogManager.
                LogManager.ReconfigExistingLoggers();
                _logger.LogInformation("'SiteKeeperMasterBoundTarget' has been successfully added and configured.");
            }
            else
            {
                _logger.LogInformation("'SiteKeeperMasterBoundTarget' was already present in the configuration.");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the application host is performing a graceful shutdown.
        /// This service does not require any specific actions during shutdown.
        /// </summary>
        /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous Stop operation.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            // No-op for shutdown
            return Task.CompletedTask;
        }
    }
}
