using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Operations
{
	/// <summary>
	/// Represents the status of a single task performed on a specific node
	/// as part of a larger operation.
	/// </summary>
	/// <remarks>
	/// This DTO is used within <see cref="OperationStatusResponse"/> to detail the progress
	/// and outcome of an operation on a per-node basis.
	/// </remarks>
	public class OperationNodeTaskStatus
    {
        /// <summary>
        /// The name of the node where the task was executed.
        /// </summary>
        /// <example>"AppServer01"</example>
        [Required]
        public string NodeName { get; set; } = string.Empty;

		/// <summary>
		/// What node operation this task is associated with.
		/// </summary>
		public string ActionId { get; set; } = string.Empty;

		/// <summary>
		/// What action name (human readable and informative) this task is associated with.
		/// </summary>
		public string? ActionName { get; set; } = string.Empty;

        /// <summary>
        /// Unique identifier for this task, typically unique within the scope of its parent action.
        /// Could be, for example, "{ActionId}-{NodeName}-{TaskSequence}".
        /// </summary>
        /// <example>"op-deploy-webapp-123-AppServer01-1"</example>
        [Required]
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// The type of task. See SlaveTaskType enum.
        /// </summary>
        [Required]
        public string TaskType { get; set; } = string.Empty;

		/// <summary>
		/// The status of this specific task (e.g., "Running", "Succeeded", "Failed").
		/// </summary>
		/// <example>"InProgress" or "Succeeded"</example>
		[Required]
        public string TaskStatus { get; set; } = string.Empty; // Represents NodeTaskStatus as string

        /// <summary>
        /// An optional message providing more details about the task's status (e.g., an error message).
        /// </summary>
        /// <example>"Package verification completed successfully."</example>
        public string? Message { get; set; }

        /// <summary>
        /// The time the task started execution.
        /// </summary>
        public DateTime? TaskStartTime { get; set; }

        /// <summary>
        /// The time the task finished execution. Null if it is still running.
        /// </summary>
        public DateTime? TaskEndTime { get; set; }

        /// <summary>
        /// A JSON object representing the detailed results or output from the task execution.
        /// The structure of this object is task-specific.
        /// </summary>
		public Dictionary<string, object>? ResultPayload { get; set; }

    }
} 