using System; // For Environment.ProcessorCount
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Slave.Configuration // Or SiteKeeper.Shared.Configuration
{
    /// <summary>
    /// Configuration settings for the SiteKeeper Slave Agent.
    /// </summary>
    /// <remarks>
    /// This class typically loads its values from a configuration file (e.g., appsettings.json) or environment variables.
    /// It defines crucial parameters for the slave's operation, such as its identity, connection to the master,
    /// intervals for routine tasks, and paths for local storage.
    /// Based on the SlaveConfig.cs skeleton in "SiteKeeper - Slave - Core Service & Component Implementation.md".
    /// </remarks>
    public class SlaveConfig
    {
        /// <summary>
        /// Gets or sets the hostname or IP address of the SiteKeeper Master Agent.
        /// </summary>
        public string MasterHost { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets the port on which the Master Agent's AgentHub is listening.
        /// </summary>
        public int MasterAgentPort { get; set; } = 5002;

        /// <summary>
        /// Gets or sets the interval (in seconds) to wait before retrying to connect to the Master if the connection fails.
        /// </summary>
        public int MasterConnectionRetryIntervalSeconds { get; set; } = 15;

        /// <summary>
        /// Gets or sets a value indicating whether the connection to the Master Agent should use HTTPS.
        /// </summary>
        public bool UseHttpsForMasterConnection { get; set; } = false;

        /// <summary>
        /// Gets or sets the unique name for this slave agent.
        /// This name should be consistent and ideally match the Common Name (CN)
        /// or a Subject Alternative Name (SAN) in its client certificate if used.
        /// Defaults to "SlaveNode-" followed by the machine name.
        /// </summary>
        public string AgentName { get; set; } = $"SlaveNode-{Environment.MachineName}";

        /// <summary>
        /// Gets or sets the path to this slave's client certificate PFX file.
        /// Required if <see cref="UseHttpsForMasterConnection"/> is true and the master requires client certificates.
        /// Path can be absolute or relative to the application's base directory.
        /// </summary>
        public string? ClientCertPath { get; set; }

        /// <summary>
        /// Gets or sets the password for the client certificate PFX file specified in <see cref="ClientCertPath"/>.
        /// </summary>
        public string? ClientCertPassword { get; set; }

        /// <summary>
        /// Gets or sets the path to the CA certificate file (.crt, .cer, .pem) used to validate the Master Agent's server certificate.
        /// Important if the master uses a self-signed certificate or a certificate from a private CA.
        /// Path can be absolute or relative to the application's base directory.
        /// </summary>
        public string? MasterCaCertPath { get; set; }

        /// <summary>
        /// Gets or sets the interval (in seconds) for sending periodic heartbeat status updates to the Master.
        /// </summary>
        public int HeartbeatIntervalSeconds { get; set; } = 5;

        /// <summary>
        /// Gets or sets the maximum time adjustment (in minutes) the slave will accept directly from the master
        /// via <c>SetSystemTime</c> without a special 'force' flag or additional checks.
        /// This is a safety precaution against massive, unintended clock changes.
        /// </summary>
        public int MaxTimeAdjustmentMinutesWithoutForce { get; set; } = 5;

        /// <summary>
        /// Gets or sets the NLog configuration file name (e.g., "nlog.Slave.config" or "nlog.config").
        /// NLog will typically load this from the application's base directory.
        /// </summary>
        public string NLogConfigFileName { get; set; } = "nlog.Slave.config";

        /// <summary>
        /// Gets or sets the maximum number of concurrent tasks the slave will attempt to execute.
        /// This helps manage resources on the slave. Defaults to the number of processors.
        /// </summary>
        public int MaxConcurrentTasks { get; set; } = Environment.ProcessorCount > 0 ? Environment.ProcessorCount : 1;

        /// <summary>
        /// Interval in seconds at which the Slave Agent should monitor local resources (CPU, memory, disk).
        /// </summary>
        /// <example>60</example>
        [Range(10, 600)] // Sensible range for resource monitoring: 10 seconds to 10 minutes
        public int ResourceMonitorIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets the specific drive to monitor for available disk space (e.g., "C", "D:").
        /// If null or empty, a default drive (e.g., the one hosting the application or system drive) might be assumed by the monitoring logic.
        /// </summary>
        /// <example>"C"</example>
        public string? MonitoredDriveForDiskSpace { get; set; }

        /// <summary>
        /// Base file system path where downloaded software packages are stored by the agent.
        /// </summary>
        /// <example>"C:\SiteKeeper\Slave\Packages"</example>
        [Required(AllowEmptyStrings = false)]
        public string PackagesBasePath { get; set; } = string.Empty;

        /// <summary>
        /// Base file system path where logs for executed tasks are stored by the agent.
        /// </summary>
        /// <example>"C:\SiteKeeper\Slave\Logs\Tasks"</example>
        [Required(AllowEmptyStrings = false)]
        public string LogsBasePath { get; set; } = string.Empty;

        /// <summary>
        /// Default timeout in seconds for tasks if not overridden by a specific task assignment from the Master.
        /// </summary>
        /// <example>600</example>
        [Range(30, 3600)] // Sensible range for default task timeout: 30 seconds to 1 hour
        public int DefaultTaskTimeoutSeconds { get; set; } = 600;
    }
} 