using SiteKeeper.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.MasterSlave
{
    /// <summary>
    /// DTO sent from Master to Slave to instruct it to execute a specific task.
    /// This is sent after a successful readiness check (if applicable).
    /// </summary>
    public class SlaveTaskInstruction
    {
        /// <summary>
        /// The unique identifier of the overall node action this task belongs to.
        /// </summary>
        [Required]
        public string ActionId { get; set; } = string.Empty; // Renamed from OperationId

        /// <summary>
        /// The unique identifier for this specific task to be executed.
        /// </summary>
        [Required]
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// The type of task the Slave Agent should execute.
        /// </summary>
        [Required]
        public SlaveTaskType TaskType { get; set; }

        /// <summary>
        /// A JSON string containing parameters specific to the <see cref="TaskType"/>.
        /// The slave's IExecutiveCodeExecutor for the given TaskType will parse this JSON.
        /// </summary>
        /// <example>{"packageName": "MyWebApp", "version": "1.2.0", "sourceUrl": "http://repo/MyWebApp.zip"}</example>
        public string? ParametersJson { get; set; } // Can be null if task type requires no params

        /// <summary>
        /// Optional. Specifies the maximum time in seconds the agent should allow for this task to complete.
        /// If the task exceeds this timeout, the agent should report a timeout failure.
        /// If not provided, a default timeout defined on the agent or master might apply.
        /// </summary>
        /// <example>300</example>
        public int? TimeoutSeconds { get; set; }
    }
} 