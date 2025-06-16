using System;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.MasterSlave
{
    /// <summary>
    /// DTO sent from Master to Slave to command it to adjust its system time.
    /// </summary>
    public class AdjustSystemTimeCommand
    {
        /// <summary>
        /// The Master's authoritative UTC timestamp that the Slave should sync to.
        /// </summary>
        [Required]
        public DateTime AuthoritativeUtcTimestamp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the slave should force the time adjustment,
        /// bypassing any configured thresholds for maximum allowed difference.
        /// Defaults to false.
        /// </summary>
        public bool ForceAdjustment { get; set; } = false;
    }
} 