using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.AgentHub
{
    /// <summary>
    /// DTO used by the Master to request a Slave Agent to cancel an ongoing task.
    /// </summary>
    /// <remarks>
    /// This message is sent from the Master's AgentHub to a specific Slave Agent.
    /// It specifies the operation and task ID that needs to be cancelled.
    /// An optional reason for cancellation can be provided.
    /// Corresponds to the `CancelTaskOnAgentRequest` schema in `web api swagger.yaml`.
    /// </remarks>
    public class CancelTaskOnAgentRequest
    {
        /// <summary>
        /// The unique identifier of the overall operation the task belongs to.
        /// </summary>
        /// <example>"op-deploy-webapp-123"</example>
        [Required]
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// The unique identifier of the task to be cancelled.
        /// </summary>
        /// <example>"task-nodeA-deploy-pkgX"</example>
        [Required]
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Optional. A brief reason explaining why the task cancellation is requested.
        /// </summary>
        /// <example>"Operation aborted by user."</example>
        public string? Reason { get; set; }
    }
} 