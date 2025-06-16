using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Defines the possible internal overall statuses of a master-coordinated operation.
    /// These are typically more granular than what is exposed directly via API responses for ongoing or completed operations.
    /// </summary>
    /// <remarks>
    /// This enum represents the detailed lifecycle states of an operation as managed by the <c>OperationCoordinatorService</c>
    /// in the Master Agent. It helps track the precise phase of an operation, from initiation through readiness checks,
    /// task dispatch, execution, result collection, and finalization (including cancellation).
    /// See "SiteKeeper - Master - Data Structures.md" and "SiteKeeper Master Slave - guidelines.md" for context on its usage.
    /// While primarily for internal Master Agent state, it's placed in Shared if DTOs or messages between Master/Slave
    /// might need to reference these detailed states, or if shared logic depends on them.
    /// For API communication, these states are typically mapped to simpler enums like <see cref="OngoingOperationStatus"/>
    /// and <see cref="CompletedOperationFinalStatus"/>.
    /// Serialization to string is handled by <see cref="JsonStringEnumConverter"/>.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OperationOverallStatus
    {
        /// <summary>
        /// The status of the operation is unknown or not yet initialized.
        /// </summary>
        Unknown,

        /// <summary>
        /// The operation has been requested but not yet processed by the coordinator service.
        /// Initial state before any processing by OperationCoordinatorService.
        /// </summary>
        PendingInitiation,

        /// <summary>
        /// The operation is created, and the system is about to start or is currently performing readiness checks with target slave nodes.
        /// (Corresponds to "PendingReadinessCheck" in "SiteKeeper - Master - Core Service Implementation Guidelines.md")
        /// </summary>
        PendingReadinessCheck,

        /// <summary>
        /// Readiness checks are actively being performed with slave nodes.
        /// </summary>
        CheckingReadiness,

        /// <summary>
        /// The Master has requested readiness from relevant slave nodes and is now waiting for them to report back their status for the specific task.
        /// This state implies that "PrepareForTask" instructions have been sent.
        /// </summary>
        AwaitingNodeReadiness,

        /// <summary>
        /// Readiness checks completed, but one or more critical slave nodes are not ready for the operation.
        /// The operation cannot proceed and will likely be marked as failed.
        /// </summary>
        FailedReadinessCheck,

        /// <summary>
        /// All critical slave nodes have reported ready. The Master Agent is preparing to dispatch the actual tasks.
        /// (Corresponds to "PreparingToDispatchTasks" mentioned in some internal logic flows).
        /// </summary>
        PreparingToDispatchTasks, // As per internal logic docs

        /// <summary>
        /// The Master Agent is actively dispatching tasks to the ready slave nodes.
        /// </summary>
        DispatchingTasks,

        /// <summary>
        /// Tasks have been dispatched, and the operation is now in progress, with slave nodes executing their assigned tasks.
        /// </summary>
        InProgress,

        /// <summary>
        /// All slave tasks have completed, and the Master Agent is collecting and processing the results.
        /// This might be a distinct phase if result aggregation is complex.
        /// </summary>
        CollectingResults,

        /// <summary>
        /// A cancellation request has been received, and the Master Agent is attempting to cancel the operation and its active tasks.
        /// </summary>
        Cancelling,

        /// <summary>
        /// The operation has completed successfully, and all objectives were met.
        /// This is a terminal state.
        /// </summary>
        Succeeded,

        /// <summary>
        /// The operation completed, but some non-critical tasks failed or encountered errors. The overall objective might still be met.
        /// This is a terminal state.
        /// </summary>
        SucceededWithErrors,

        /// <summary>
        /// The operation has failed due to critical task failures, readiness check failures, or other unrecoverable errors.
        /// This is a terminal state.
        /// </summary>
        Failed,

        /// <summary>
        /// The operation was successfully cancelled before completion.
        /// This is a terminal state.
        /// </summary>
        Cancelled
    }
} 