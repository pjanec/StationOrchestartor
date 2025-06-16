using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
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

        public NLogTargetAssuranceService(ILogger<NLogTargetAssuranceService> logger)
        {
            _logger = logger;
        }

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
                config.AddTarget(masterTarget);

                // 2. Create a rule to send logs from "Executive.*" to this new target.
                var rule = new LoggingRule($"{SiteKeeperMasterBoundTarget.ExecutiveLogPrefix}.*", NLog.LogLevel.Debug, masterTarget);
                config.LoggingRules.Add(rule);

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

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // No-op for shutdown
            return Task.CompletedTask;
        }
    }
}
