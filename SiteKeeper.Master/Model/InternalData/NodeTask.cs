using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations; // For potential future use

namespace SiteKeeper.Master.Model.InternalData
{
    /// <summary>
    /// Represents a specific task assigned to a node as part of a larger <see cref="NodeAction"/>.
    /// </summary>
    /// <remarks>
    /// Each action is broken down into one or more node tasks, each targeted at a specific Slave Agent.
    /// This class tracks the lifecycle, payload, and status of an individual task.
    /// </remarks>
    public class NodeTask
    {
        /// <summary>
        /// Unique identifier for this task, typically unique within the scope of its parent action.
        /// Could be, for example, "{ActionId}-{NodeName}-{TaskSequence}".
        /// </summary>
        /// <example>"op-deploy-webapp-123-AppServer01-1"</example>
        [Required]
        public string TaskId { get; set; }

        /// <summary>
        /// Identifier of the <see cref="NodeAction"/> this task belongs to.
        /// </summary>
        [Required]
        public string ActionId { get; set; }

        /// <summary>
        /// The name of the node (Slave Agent) this task is targeted at.
        /// </summary>
        /// <example>"AppServer01"</example>
        [Required]
        public string NodeName { get; set; }

        /// <summary>
        /// The type of task to be executed by the Slave Agent.
        /// </summary>
        [Required]
        public SlaveTaskType TaskType { get; set; }

        /// <summary>
        /// Current status of this task.
        /// </summary>
        [Required]
        public NodeTaskStatus Status { get; set; }

        /// <summary>
        /// The payload containing data required by the Slave Agent to execute this task.
        /// Structure depends on the <see cref="TaskType"/>.
        /// </summary>
        /// <example>{"packageName": "MyWebApp", "version": "1.2.0"}</example>
        public Dictionary<string, object> TaskPayload { get; set; }

        /// <summary>
        /// Timestamp (UTC) when this task was created and queued.
        /// </summary>
        public DateTime CreationTime { get; set; }

        /// <summary>
        /// Timestamp (UTC) when this task was actually sent to the agent or started processing locally.
        /// Null if not yet started.
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Timestamp (UTC) when this task concluded (succeeded, failed, cancelled).
        /// Null if still ongoing.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Timestamp (UTC) of the last status update received from the agent for this task.
        /// </summary>
        public DateTime? LastUpdateTime { get; set; }

        /// <summary>
        /// Current progress percentage (0-100) of this task, as reported by the agent.
        /// </summary>
        public int ProgressPercent { get; set; }

        /// <summary>
        /// A brief message summarizing the current status or outcome of the task.
        /// Often sourced from agent updates.
        /// </summary>
        public string? StatusMessage { get; set; }

        /// <summary>
        /// Number of times this task has been retried due to failure.
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Optional. Payload containing results or output from the task execution, reported by the agent.
        /// </summary>
        public Dictionary<string, object>? ResultPayload { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeTask"/> class.
        /// </summary>
        /// <param name="taskId">Unique ID for the task.</param>
        /// <param name="actionId">Parent action ID.</param>
        /// <param name="nodeName">Target node name.</param>
        /// <param name="taskType">Type of slave task.</param>
        /// <param name="taskPayload">Payload for the task.</param>
        public NodeTask(string taskId, string actionId, string nodeName, SlaveTaskType taskType, Dictionary<string, object> taskPayload)
        {
            TaskId = !string.IsNullOrWhiteSpace(taskId) ? taskId : throw new ArgumentNullException(nameof(taskId));
            ActionId = !string.IsNullOrWhiteSpace(actionId) ? actionId : throw new ArgumentNullException(nameof(actionId));
            NodeName = !string.IsNullOrWhiteSpace(nodeName) ? nodeName : throw new ArgumentNullException(nameof(nodeName));
            TaskType = taskType;
            TaskPayload = taskPayload ?? throw new ArgumentNullException(nameof(taskPayload));

            Status = NodeTaskStatus.Pending;
            CreationTime = DateTime.UtcNow;
            LastUpdateTime = CreationTime;
            ProgressPercent = 0;
            RetryCount = 0;
        }
    }
} 