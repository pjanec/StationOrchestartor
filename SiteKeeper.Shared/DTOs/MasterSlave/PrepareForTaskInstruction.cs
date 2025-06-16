using SiteKeeper.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.MasterSlave
{
    /// <summary>
    /// DTO sent from Master to Slave to instruct it to prepare for an upcoming task.
    /// This is part of the readiness check flow.
    /// </summary>
    public class PrepareForTaskInstruction
    {
        /// <summary>
        /// The unique identifier of the overall operation this task belongs to.
        /// </summary>
        [Required]
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// The unique identifier for the specific task the slave should prepare for.
        /// </summary>
        [Required]
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// The expected type of task the Master intends to send if the slave is ready.
        /// </summary>
        [Required]
        public SlaveTaskType ExpectedTaskType { get; set; }

        /// <summary>
        /// Optional: A JSON string containing any parameters relevant to the readiness check itself.
        /// For example, required disk space, specific software versions to check for, etc.
        /// The slave's OperationHandler for the preparation phase would parse this.
        /// </summary>
        public string? PreparationParametersJson { get; set; }

        /// <summary>
        /// Optional: Specifies a target resource for the readiness check, such as a drive letter ("C:")
        /// or a specific path. This can be used by the slave to perform context-specific checks,
        /// for example, checking disk space on a particular drive.
        /// </summary>
        public string? TargetResource { get; set; }
    }
} 