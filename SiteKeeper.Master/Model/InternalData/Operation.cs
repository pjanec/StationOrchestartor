using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations; // For potential future use, not strictly required by MD

namespace SiteKeeper.Master.Model.InternalData
{
    /// <summary>
    /// Represents an operation being managed or tracked by the Master Agent.
    /// </summary>
    /// <remarks>
    /// An operation is a high-level task initiated either by a user (e.g., via API) or internally by the system.
    /// It typically involves coordinating actions across one or more Slave Agents (nodes).
    /// This class stores the state, type, parameters, and associated tasks of an operation.
    ///
    /// Key aspects based on "SiteKeeper - Master - Data Structures.md":
    /// - Unique ID and optional user-friendly name.
    /// - Type of operation (e.g., EnvUpdateOnline, NodeRestart).
    /// - Overall status (e.g., Pending, InProgress, Succeeded, Failed).
    /// - Timestamps for creation, start, and end.
    /// - Parameters used to initiate the operation.
    /// - A list of <see cref="NodeTask"/> objects representing sub-tasks on individual nodes.
    /// - Information about who initiated the operation.
    /// - Concurrency control mechanisms (e.g., a way to ensure only one conflicting operation runs).
    /// - Progress tracking.
    /// </remarks>
    public class Operation
    {
        /// <summary>
        /// Unique identifier for the operation.
        /// </summary>
        /// <example>"op-envupdate-abc123xyz"</example>
        [Required]
        public string Id { get; set; }

        /// <summary>
        /// Optional user-friendly name or description for the operation.
        /// </summary>
        /// <example>"Deploy WebApp v1.2 to Production Servers"</example>
        public string? Name { get; set; }

        /// <summary>
        /// The type of operation being performed.
        /// </summary>
        [Required]
        public OperationType Type { get; set; }

        /// <summary>
        /// The current overall status of the operation.
        /// </summary>
        [Required]
        public OperationOverallStatus OverallStatus { get; set; }

        /// <summary>
        /// Timestamp (UTC) when the operation was created/queued.
        /// </summary>
        [Required]
        public DateTime CreationTime { get; set; }

        /// <summary>
        /// Timestamp (UTC) when the operation actually started execution.
        /// Null if not yet started.
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Timestamp (UTC) when the operation concluded (succeeded, failed, or was cancelled).
        /// Null if still ongoing.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// A dictionary of high-level contextual parameters for this operation stage.
        /// This data is NOT used for execution logic by the dispatcher; it is stored
        /// in the journal for auditing and debugging purposes to preserve the business intent.
        /// </summary>
        public IReadOnlyDictionary<string, object> AuditContext { get; set; }

        /// <summary>
        /// Identifier of the user or system component that initiated the operation.
        /// </summary>
        /// <example>"admin@example.com" or "SystemScheduler"</example>
        public string? InitiatedBy { get; set; }

        /// <summary>
        /// List of tasks (<see cref="NodeTask"/>) that comprise this operation, distributed across nodes.
        /// </summary>
        public List<NodeTask> NodeTasks { get; set; }

        /// <summary>
        /// Overall progress percentage of the operation (0-100).
        /// Can be calculated based on the progress of its NodeTasks.
        /// </summary>
        public int ProgressPercent { get; set; }

        /// <summary>
        /// A brief message summarizing the current state or outcome of the operation.
        /// </summary>
        public string? StatusMessage { get; set; }

        /// <summary>
        /// If the operation completed with a final outcome, this stores it.
        /// </summary>
        public CompletedOperationFinalStatus? FinalOutcome { get; set; }

        /// <summary>
        /// Indicates if a cancellation has been requested for this operation.
        /// </summary>
        public bool IsCancellationRequested { get; set; }

        /// <summary>
        /// Gets or sets the serialized result payload of the operation, if any.
        /// For example, for an OfflineScanSources operation, this might store the JSON of OfflineBundlesResponse.
        /// Null if the operation does not produce a direct payload or has not completed.
        /// </summary>
        public string? ResultPayload { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Operation"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for the operation.</param>
        /// <param name="type">The type of operation.</param>
        /// <param name="name">Optional user-friendly name for the operation.</param>
        /// <param name="auditContext">Parameters for the operation.</param>
        /// <param name="initiatedBy">Identifier of the initiator.</param>
        public Operation(string id, OperationType type, string? name = null, IReadOnlyDictionary<string, object>? auditContext = null, string? initiatedBy = null)
        {
            Id = !string.IsNullOrWhiteSpace(id) ? id : throw new ArgumentNullException(nameof(id));
            Type = type;
            Name = name;
            AuditContext = auditContext ?? new Dictionary<string, object>();
            InitiatedBy = initiatedBy;

            OverallStatus = OperationOverallStatus.PendingInitiation;
            CreationTime = DateTime.UtcNow;
            NodeTasks = new List<NodeTask>();
            ProgressPercent = 0;
            IsCancellationRequested = false;
        }
    }
} 