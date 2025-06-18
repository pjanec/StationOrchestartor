using SiteKeeper.Master.Model.InternalData;

namespace SiteKeeper.Master.Abstractions.Workflow
{
using System.ComponentModel.DataAnnotations; // For Required attribute

namespace SiteKeeper.Master.Abstractions.Workflow
{
    /// <summary>
    /// Defines the standardized result of a workflow stage that executes and monitors an operation across multiple nodes.
    /// </summary>
    /// <remarks>
    /// This DTO encapsulates the overall outcome of a distributed node action. It is typically returned by a coordinator
    /// (like <see cref="SiteKeeper.Master.Workflow.StageHandlers.NodeCoordinator"/>) that processes a <see cref="NodeAction"/>
    /// across multiple nodes. The <see cref="IsSuccess"/> flag usually reflects
    /// whether all critical underlying node tasks completed successfully. The <see cref="FinalActionState"/>
    /// provides the detailed status of the entire node action, including individual task outcomes.
    /// </remarks>
    public class NodeActionResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the multi-node operation as a whole was successful.
        /// For example, this would be true if all critical tasks within the operation succeeded, and false otherwise.
        /// </summary>
        /// <example>true</example>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the final, complete state of the <see cref="NodeAction"/> object
        /// after the node coordination stage has finished execution and all node tasks have reached a terminal state.
        /// This includes the status and results of all individual slave tasks.
        /// </summary>
        [Required]
        public NodeAction FinalActionState { get; set; } = null!; // Initialized to null! with [Required] to indicate it must be provided.
    }
}