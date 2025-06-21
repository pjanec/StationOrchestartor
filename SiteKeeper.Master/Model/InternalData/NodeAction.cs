using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations; // For potential future use, not strictly required by MD

namespace SiteKeeper.Master.Model.InternalData
{
    /// <summary>
    /// Represents an multi-node action being managed or tracked by the Master Agent.
    /// </summary>
    /// <remarks>
    /// An action is a high-level task initiated as a response to user operation request.
    /// It typically involves coordinating tasks across one or more Slave Agents (nodes).
    /// This class stores the state, type, parameters, and associated tasks of an action.
    /// </remarks>
    public class NodeAction
    {
        /// <summary>
        /// Unique identifier for the action
        /// </summary>
        /// <example>"op-envupdate-abc123xyz"</example>
        [Required]
        public string Id { get; set; }

        /// <summary>
        /// Optional user-friendly name or description for the action.
        /// </summary>
        /// <example>"Deploy WebApp v1.2 to Production Servers"</example>
        public string? Name { get; set; }

        /// <summary>
        /// The current overall status of the action.
        /// </summary>
        [Required]
        public NodeActionOverallStatus OverallStatus { get; set; }

        /// <summary>
        /// Timestamp (UTC) when the action was created/queued.
        /// </summary>
        [Required]
        public DateTime CreationTime { get; set; }

        /// <summary>
        /// Timestamp (UTC) when the action actually started execution.
        /// Null if not yet started.
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Timestamp (UTC) when the action concluded (succeeded, failed, or was cancelled).
        /// Null if still ongoing.
        /// </summary>
        public DateTime? EndTime { get; set; }

		/// <summary>
		/// A dictionary of high-level contextual parameters for this action stage.
		/// It could be whethever to help understand the action's intent or to provide additional context.
		///   - master action id or other paremeters
		///   - stage id and name
		///   - parameters used to build node tasks
        ///   - etc.
		/// This data is NOT used for execution logic by the dispatcher; it is stored
		/// in the journal for auditing and debugging purposes to preserve the business intent.
		/// </summary>
		public IReadOnlyDictionary<string, object> AuditContext { get; set; }

        /// <summary>
        /// Identifier of the user or system component that initiated the action.
        /// </summary>
        /// <example>"admin@example.com" or "SystemScheduler"</example>
        public string? InitiatedBy { get; set; }

        /// <summary>
        /// List of tasks (<see cref="NodeTask"/>) that comprise this action, distributed across nodes.
        /// </summary>
        public List<NodeTask> NodeTasks { get; set; }

        /// <summary>
        /// Overall progress percentage of the action (0-100).
        /// Can be calculated based on the progress of its NodeTasks.
        /// </summary>
        public int ProgressPercent { get; set; }

        /// <summary>
        /// A brief message summarizing the current state or outcome of the action.
        /// </summary>
        public string? StatusMessage { get; set; }

        /// <summary>
        /// If the action completed with a final outcome, this stores it.
        /// </summary>
        public CompletedOperationFinalStatus? FinalOutcome { get; set; }

        /// <summary>
        /// Indicates if a cancellation has been requested for this action.
        /// </summary>
        public bool IsCancellationRequested { get; set; }

        /// <summary>
        /// Gets or sets the serialized result payload of the action, if any.
        /// For example, for an OfflineScanSources operation, this might store the JSON of OfflineBundlesResponse.
        /// Null if the action does not produce a direct payload or has not completed.
        /// </summary>
        public string? ResultPayload { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeAction"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for the action.</param>
        /// <param name="name">Optional user-friendly name for the action.</param>
        /// <param name="auditContext">arbitraty key-value for auditing purposes (for example action parameters).</param>
        /// <param name="initiatedBy">Identifier of the initiator.</param>
        public NodeAction(string id, string? name = null, IReadOnlyDictionary<string, object>? auditContext = null, string? initiatedBy = null)
        {
            Id = !string.IsNullOrWhiteSpace(id) ? id : throw new ArgumentNullException(nameof(id));
            Name = name;
            AuditContext = auditContext ?? new Dictionary<string, object>();
            InitiatedBy = initiatedBy;

            OverallStatus = NodeActionOverallStatus.PendingInitiation;
            CreationTime = DateTime.UtcNow;
            NodeTasks = new List<NodeTask>();
            ProgressPercent = 0;
            IsCancellationRequested = false;
        }
    }
} 