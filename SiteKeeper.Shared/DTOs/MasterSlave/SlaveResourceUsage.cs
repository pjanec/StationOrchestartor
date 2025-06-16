using System;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.MasterSlave
{
    /// <summary>
    /// DTO used by a Slave Agent to report its current resource usage to the Master Agent.
    /// </summary>
    /// <remarks>
    /// This information can be used by the master for monitoring, load balancing decisions,
    /// or triggering alerts.
    /// </remarks>
    public class SlaveResourceUsage
    {
        /// <summary>
        /// The unique name of the slave agent reporting its resource usage.
        /// </summary>
        /// <example>"SlaveNode-01"</example>
        [Required]
        public string NodeName { get; set; } = string.Empty;

        /// <summary>
        /// The UTC timestamp when the resource usage was sampled by the slave.
        /// </summary>
        [Required]
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Current CPU usage percentage on the slave machine (0-100).
        /// </summary>
        /// <example>45.5</example>
        [Range(0, 100)]
        public double CpuUsagePercentage { get; set; }

        /// <summary>
        /// Currently used memory in bytes on the slave machine.
        /// </summary>
        /// <example>8589934592</example> // e.g., 8 GB
        public long MemoryUsageBytes { get; set; }

        /// <summary>
        /// Available disk space in Megabytes (MB) on the slave's primary monitored drive.
        /// </summary>
        /// <example>51200</example> // e.g., 50 GB
        public long AvailableDiskSpaceMb { get; set; }
    }
} 