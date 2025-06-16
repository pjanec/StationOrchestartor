using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.MasterSlave
{
    /// <summary>
    /// DTO used by a Slave Agent to register itself with the Master Agent.
    /// </summary>
    /// <remarks>
    /// This request is sent when the slave connects (or reconnects) to the master.
    /// It provides essential information about the slave agent to the master.
    /// </remarks>
    public class SlaveRegistrationRequest
    {
        /// <summary>
        /// The unique name of the slave agent.
        /// This should be configured on the slave and must be unique within the master's scope.
        /// </summary>
        /// <example>"SlaveNode-01"</example>
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string AgentName { get; set; } = string.Empty;

        /// <summary>
        /// The version of the slave agent software.
        /// </summary>
        /// <example>"1.0.0"</example>
        [Required]
        public string AgentVersion { get; set; } = string.Empty;

        /// <summary>
        /// A description of the operating system on which the slave agent is running.
        /// </summary>
        /// <example>"Microsoft Windows 10.0.19045"</example>
        public string? OsDescription { get; set; }

        /// <summary>
        /// A description of the .NET runtime/framework the slave agent is using.
        /// </summary>
        /// <example>".NET 8.0.0"</example>
        public string? FrameworkDescription { get; set; }

        /// <summary>
        /// The maximum number of concurrent tasks the slave agent is configured to handle.
        /// </summary>
        /// <example>4</example>
        [Range(1, 128)]
        public int MaxConcurrentTasks { get; set; } = 1;

        /// <summary>
        /// The hostname of the machine where the slave agent is running.
        /// </summary>
        /// <example>"PROD-WEB-SVR01"</example>
        public string? Hostname { get; set; }

        // Potential future additions:
        // public List<string> Tags { get; set; } = new List<string>();
        // public string? MachineSid { get; set; }
        // public Dictionary<string, string> AgentSpecificCapabilities { get; set; } = new Dictionary<string, string>();
    }
} 