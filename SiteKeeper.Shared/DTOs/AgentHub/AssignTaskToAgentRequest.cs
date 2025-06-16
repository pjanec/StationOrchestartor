using SiteKeeper.Shared.Enums;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.AgentHub
{
    /// <summary>
    /// DTO used by the Master to assign a specific task to a connected Slave Agent.
    /// </summary>
    /// <remarks>
    /// This message is sent from the Master's AgentHub to a specific Slave Agent.
    /// It details the operation and task ID, the type of task the slave needs to perform,
    /// the payload necessary for task execution, and an optional timeout.
    /// Corresponds to the `AssignTaskToAgentRequest` schema in `web api swagger.yaml`.
    /// The `SlaveTaskType` enum is defined in `SiteKeeper.Shared.Enums` and comes from `SiteKeeper - Slave - data structures.md`.
    /// </remarks>
    public class AssignTaskToAgentRequest
    {
        /// <summary>
        /// The unique identifier of the overall operation this task belongs to.
        /// </summary>
        /// <example>"op-deploy-webapp-123"</example>
        [Required]
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// The unique identifier for this specific task assigned to the agent.
        /// </summary>
        /// <example>"task-nodeA-deploy-pkgX"</example>
        [Required]
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// The type of task the Slave Agent should execute.
        /// </summary>
        /// <example>SlaveTaskType.InstallPackage</example>
        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SlaveTaskType TaskType { get; set; }

        /// <summary>
        /// The payload containing data required for the task. The structure of this object
        /// depends on the <see cref="TaskType"/>.
        /// For example, for InstallPackage, it might include package name, version, and source URL.
        /// Serialized as a JSON string or a dictionary before sending, and deserialized by the agent.
        /// </summary>
        /// <example>{"packageName": "MyWebApp", "version": "1.2.0", "sourceUrl": "http://repo/MyWebApp.zip"}</example>
        [Required]
        public Dictionary<string, object> TaskPayload { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Optional. Specifies the maximum time in seconds the agent should allow for this task to complete.
        /// If the task exceeds this timeout, the agent should report a timeout failure.
        /// If not provided, a default timeout defined on the agent or master might apply.
        /// </summary>
        /// <example>300</example>
        public int? TaskTimeoutSeconds { get; set; }
    }
} 