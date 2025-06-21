using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Operations
{
	/// <summary>
	/// Represents the detailed status of a specific asynchronous operation.
	/// This DTO is returned by the GET /api/v1/operations/{operationId} endpoint.
	/// </summary>
	/// <remarks>
	/// This DTO provides comprehensive information including the operation's ID, name, type, overall status (which could be an ongoing or completed status),
	/// start time, end time (if completed), progress, a list of target nodes with their individual task statuses, and recent log entries.
	/// Based on the OperationStatusResponse schema in `web api swagger.yaml` and detailed in
	/// "SiteKeeper - API - Detailed DTO Definitions.md".
	/// </remarks>
	public class OperationStatusResponse
    {
        /// <summary>
        /// The unique identifier of the operation.
        /// </summary>
        /// <example>"op-envverify-b3a4c1d2"</example>
        [Required]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// The user-friendly name of the operation (e.g., "Deploy new version").
        /// </summary>
        /// <example>"Environment Verification Task"</example>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The type of the operation.
        /// </summary>
        /// <example>OperationType.EnvVerify</example>
        [Required]
        public OperationType OperationType { get; set; }

        /// <summary>
        /// The current overall status of the operation (e.g., "InProgress", "Succeeded", "Failed").
        /// </summary>
        /// <example>"InProgress" or "Succeeded"</example>
        [Required]
        public string Status { get; set; } = string.Empty; // Could be OngoingOperationStatus or CompletedOperationFinalStatus or mapped OperationOverallStatus as string

        /// <summary>
        /// The overall progress of the operation, as a percentage. Null if not applicable.
        /// </summary>
        /// <example>75</example>
        [Range(0, 100)]
        public int? ProgressPercent { get; set; }

        /// <summary>
        /// The time the operation was initiated.
        /// </summary>
        /// <example>"2023-10-26T10:30:00Z"</example>
        [Required]
        public DateTime StartTime { get; set; }

        /// <summary>
        /// The time the operation completed. Null if it is still in progress.
        /// </summary>
        /// <example>"2023-10-26T10:45:00Z"</example>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// A dictionary of parameters that the operation was started with.
        /// </summary>
        public Dictionary<string, object>? Parameters { get; set; }

        /// <summary>
        /// A structured list of all stages that have executed as part of this operation.
        /// Replaces the previous flat list of NodeTasks.
        /// </summary>
        public List<StageStatusInfo> Stages { get; set; } = new List<StageStatusInfo>();

		/// <summary>
		/// The final result payload of the operation, if applicable.
		/// Must be convertible to json.
		/// For operations running a task on multiple nodes, this could be a summary of results.
		/// </summary>
		public object? ResultPayload { get; set; }

		/// <summary>
		/// A list of recent log messages related to the operation, useful for quick diagnostics.
		/// </summary>
		public List<string> RecentLogs { get; set; } = new List<string>();
    }


    /// <summary>
    /// Represents the status of a single stage within a larger operation.
    /// This DTO is part of the structured OperationStatusResponse.
    /// </summary>
    public class StageStatusInfo
    {
        [Required]
        public int StageIndex { get; set; }
        
        [Required]
        public string StageName { get; set; } = string.Empty;

        [Required]
        public bool IsSuccess { get; set; }

        /// <summary>
        /// The list of all tasks that were executed within this stage,
        /// including their final status and results.
        /// </summary>
        [Required]
        public List<OperationNodeTaskStatus> NodeTasks { get; set; } = new List<OperationNodeTaskStatus>();
    }
} 