using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Defines the possible statuses of an individual task assigned to a slave node by the Master Agent.
    /// </summary>
    /// <remarks>
    /// This enum tracks the detailed lifecycle of a task on a slave, from the master's perspective,
    /// covering readiness checks, dispatch, execution, and various terminal states (success, failure, cancellation, errors).
    /// It is primarily used internally by the <c>OperationCoordinatorService</c> and for journaling the state of each <c>NodeTask</c>
    /// within an <c>Operation</c>.
    /// See "SiteKeeper - Master - Data Structures.md" and "SiteKeeper Master Slave - guidelines.md" for its role in operations.
    /// Slave agents also report their progress using statuses that map to these values (e.g., via <c>SlaveTaskProgressUpdate</c> DTOs).
    /// Serialization to string is handled by <see cref="JsonStringEnumConverter"/>.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum NodeTaskStatus
    {
        /// <summary>
        /// The status of the node task is unknown or not yet initialized.
        /// </summary>
        Unknown,

        /// <summary>
        /// The task has been created by the Master but not yet processed for dispatch (e.g., readiness checks not started).
        /// Initial state for a newly created NodeTask.
        /// </summary>
        Pending,

        /// <summary>
        /// The Master Agent is about to send a "PrepareForTask" instruction to the slave for this task.
        /// This is an initial state before communication for readiness begins.
        /// </summary>
        AwaitingReadiness,

        /// <summary>
        /// The Master Agent has sent the "PrepareForTask" instruction to the slave and is awaiting a readiness report.
        /// </summary>
        ReadinessCheckSent,

        // AwaitingReadinessAck, // This state was commented out in the source MD file, implying ReadinessCheckSent covers it.

        /// <summary>
        /// The slave has confirmed it is ready to execute the task.
        /// </summary>
        ReadyToExecute,

        /// <summary>
        /// The slave has reported that it is not ready to execute the task, possibly with a reason.
        /// </summary>
        NotReadyForTask,

        /// <summary>
        /// The slave did not report its readiness status within the allocated timeout period.
        /// </summary>
        ReadinessCheckTimedOut,

        /// <summary>
        /// The Master Agent failed to dispatch the "PrepareForTask" instruction to the slave (e.g., slave disconnected).
        /// </summary>
        DispatchFailed_Prepare,

        /// <summary>
        /// The Master Agent has dispatched the actual task instruction (<c>SlaveTaskInstruction</c>) to the slave.
        /// </summary>
        TaskDispatched,

        /// <summary>
        /// The slave has acknowledged receipt of the task instruction and is in the process of starting its execution.
        /// </summary>
        Starting,

        /// <summary>
        /// The slave is actively executing the task.
        /// </summary>
        InProgress,

        /// <summary>
        /// The slave is attempting to retry the task after a transient failure.
        /// </summary>
        Retrying,

        /// <summary>
        /// The slave has reported that the task completed successfully.
        /// </summary>
        Succeeded,

        /// <summary>
        /// The task completed with some non-critical errors or warnings.
        /// The primary objective of the task might still be met.
        /// </summary>
        SucceededWithIssues,

        /// <summary>
        /// The slave has reported that the task failed during execution.
        /// </summary>
        Failed,

        /// <summary>
        /// The Master has requested cancellation, and the slave is currently attempting to cancel the task.
        /// This is an intermediate status before <see cref="Cancelled"/> or <see cref="CancellationFailed"/>.
        /// </summary>
        Cancelling,

        /// <summary>
        /// The task was cancelled successfully, typically in response to a Master request.
        /// </summary>
        Cancelled,

        /// <summary>
        /// The Master Agent requested cancellation, but it failed or could not be confirmed by the slave.
        /// </summary>
        CancellationFailed,

        /// <summary>
        /// The Master Agent failed to dispatch the actual task instruction to the slave, even after the slave reported ready.
        /// (e.g., slave disconnected between readiness report and task dispatch).
        /// </summary>
        TaskDispatchFailed_Execute,

        /// <summary>
        /// The Master Agent lost contact with the slave, or the slave reported itself as going offline during task execution.
        /// </summary>
        NodeOfflineDuringTask,

        /// <summary>
        /// The task did not complete within its allocated timeout period.
        /// </summary>
        TimedOut
    }
} 