using NLog;
using NLog.Config;
using NLog.Targets;

namespace SiteKeeper.IntegrationTests
{
    /// <summary>
    /// A helper class to programmatically configure NLog for integration tests.
    /// This removes the dependency on an external nlog.config file, which can be
    /// unreliable in different test execution contexts.
    /// </summary>
    public static class NLogTestConfiguration
    {
        /// <summary>
        /// Configures NLog to route all log messages to the attached debugger's output window.
        /// </summary>
        public static void ConfigureNLogToDebug()
        {
            // Create a new logging configuration object
            var config = LogManager.Configuration;

            // Create the target that writes to the debugger.
            var debuggerTarget = new DebuggerTarget("debugger")
            {
                // Define the layout of the log message
                Layout = "${longdate}|${level:uppercase=true}|${logger:shortName=true}|${message} ${exception:format=tostring}"
            };

            // Add the new target to the configuration
            config.AddTarget(debuggerTarget);

            // Create a rule to route all logs (logger name="*") from the Trace level
            // and higher to our new debugger target.
            // Using "Trace" ensures maximum verbosity during debugging.
            var rule = new LoggingRule("*", LogLevel.Trace, debuggerTarget);
            config.LoggingRules.Add(rule);

            // Apply the new configuration to the NLog LogManager.
            // This is the crucial step that activates our in-memory configuration.
            LogManager.Configuration = config;
        }
    }
}
