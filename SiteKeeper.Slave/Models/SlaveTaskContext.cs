// In SiteKeeper.Slave project (or SiteKeeper.Shared if widely used by master for context)
using SiteKeeper.Shared.DTOs.MasterSlave; // For SlaveTaskInstruction
using System;
using System.Threading;
using System.Text.Json.Serialization; // For JsonIgnore

namespace SiteKeeper.Slave.Models // Or SiteKeeper.Shared.Models
{
    /// <summary>
    /// Defines the execution status of a task currently being processed by the slave (slave's internal view).
    /// </summary>
    public enum SlaveLocalTaskExecutionStatus
    {
        Unknown,
        PendingAcceptance,      // Received PrepareForTask, slave is deciding on readiness
        ReadyAcknowledged,      // Slave has sent IsReady=true to master for this task's prepare phase
        NotReadyAcknowledged,   // Slave has sent IsReady=false to master for this task's prepare phase
        AwaitingExecution,      // Slave is ready and has received the actual SlaveTaskInstruction, about to start
        Starting,               // Task execution logic is being initiated
        InProgress,             // Task execution logic is actively running
        Completing,             // Task logic finished, slave is about to send final status to master
        Succeeded,              // Task logic completed successfully, final success reported to master
        Failed,                 // Task logic failed or an error occurred, final failure reported to master
        Cancelling,             // Cancellation request received from master, slave is attempting to stop task
        Cancelled               // Task execution was successfully cancelled, final cancellation reported to master
    }

    /// <summary>
    /// Represents the context of a specific task assigned by the master and currently
    /// being managed or executed by the slave agent.
    /// </summary>
    /// <remarks>
    /// This class holds all relevant information for a task's lifecycle on the slave,
    /// including the initial instruction, current status, cancellation token, and results.
    /// It is used by the <see cref="Services.SlaveCommandsHandler"/> and potentially by the <see cref="Abstractions.IExecutiveCodeExecutor"/>.
    /// </remarks>
    public class SlaveTaskContext
    {
        /// <summary>
        /// The instruction received from the master that initiated this task.
        /// Contains OperationId, TaskId, TaskType, ParametersJson.
        /// </summary>
        public SlaveTaskInstruction Instruction { get; private set; }

        /// <summary>
        /// The current local execution status of this task on the slave.
        /// </summary>
        public SlaveLocalTaskExecutionStatus CurrentLocalStatus { get; set; }

        /// <summary>
        /// Timestamp when this task context was created or the task was initiated on the slave.
        /// </summary>
        public DateTime TaskStartTimeUtc { get; private set; }

        /// <summary>
        /// Timestamp of the last significant state change or update for this task.
        /// </summary>
        public DateTime LastStatusUpdateUtc { get; set; }

        /// <summary>
        /// Token source to signal cancellation for this specific task execution.
        /// This is triggered when the master requests cancellation for this TaskId.
        /// </summary>
        [JsonIgnore] // Avoid serializing this if the context itself is ever serialized
        public CancellationTokenSource CancellationTokenSource { get; private set; }

        /// <summary>
        /// Reference to the actual asynchronous Task running the executive code.
        /// Can be used to await completion if needed, though often tasks are managed
        /// with status reporting via SignalR.
        /// </summary>
        [JsonIgnore]
        public Task? ExecutionTask { get; set; }

        /// <summary>
        /// Stores the current overall progress percentage for this task (0-100).
        /// This might be updated by the <see cref="Abstractions.IExecutiveCodeExecutor"/>.
        /// </summary>
        public int? CurrentProgressPercent { get; set; }

        /// <summary>
        /// Stores the last progress percentage that was reported to the master.
        /// Used by <see cref="ShouldSendProgressUpdate"/> to throttle updates.
        /// </summary>
        public int LastReportedProgressPercent { get; set; } = 0;

        private const int ProgressUpdateThresholdPercent = 10; // Send update if progress changes by at least this much
        private static readonly TimeSpan MinTimeBetweenProgressUpdates = TimeSpan.FromSeconds(5); // Or send if at least this much time has passed

        /// <summary>
        /// Stores the final result (JSON string) of this task if it has completed (Succeeded/Failed).
        /// This is typically set by the <see cref="Abstractions.IExecutiveCodeExecutor"/>.
        /// </summary>
        public string? FinalResultJson { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SlaveTaskContext"/> class.
        /// </summary>
        /// <param name="instruction">The task instruction from the master.</param>
        public SlaveTaskContext(SlaveTaskInstruction instruction)
        {
            Instruction = instruction ?? throw new ArgumentNullException(nameof(instruction));
            TaskStartTimeUtc = DateTime.UtcNow;
            LastStatusUpdateUtc = DateTime.UtcNow;
            CurrentLocalStatus = SlaveLocalTaskExecutionStatus.AwaitingExecution; 
            CancellationTokenSource = new CancellationTokenSource();
            LastReportedProgressPercent = 0; // Initialize explicitly
            CurrentProgressPercent = 0;
        }

        /// <summary>
        /// Determines if a progress update should be sent to the master based on the current progress.
        /// </summary>
        /// <param name="currentProgress">The current progress percentage (0-100) of the task.</param>
        /// <returns><c>true</c> if an update should be sent; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// This method implements a throttling logic to avoid overwhelming the master with progress updates.
        /// An update is typically sent if:
        /// 1. The progress has changed by a significant amount (e.g., >= 10%) since the last report.
        /// 2. A certain amount of time has passed since the last report (e.g., >= 5 seconds), and there's any progress change.
        /// 3. Progress reaches 100% (to ensure the final InProgress update before Succeeded/Failed).
        /// </remarks>
        public bool ShouldSendProgressUpdate(int currentProgress)
        {
            if (currentProgress < 0) currentProgress = 0;
            if (currentProgress > 100) currentProgress = 100;

            // Ensure final progress (100%) is always reported if it hasn't been
            if (currentProgress == 100 && LastReportedProgressPercent < 100) 
            {
                return true;
            }

            // Update if progress changed by threshold OR if enough time passed and there's any change
            bool significantProgressChange = (currentProgress - LastReportedProgressPercent) >= ProgressUpdateThresholdPercent;
            bool minTimePassed = (DateTime.UtcNow - LastStatusUpdateUtc) >= MinTimeBetweenProgressUpdates;

            // Only send if there is an actual change in progress to report when minTimePassed is the trigger
            if (minTimePassed && currentProgress > LastReportedProgressPercent)
            {
                return true;
            }

            return significantProgressChange;
        }
    }
} 