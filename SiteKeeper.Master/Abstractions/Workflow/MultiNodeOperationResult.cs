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
    /// This DTO encapsulates the overall outcome of a distributed operation. It is typically returned by a stage
    /// that processes a <see cref="MultiNodeOperationInput"/>. The <see cref="IsSuccess"/> flag usually reflects
    /// whether all critical underlying node tasks completed successfully. The <see cref="FinalOperationState"/>
    /// provides the detailed status of the entire operation, including individual task outcomes.
    /// </remarks>
    public class MultiNodeOperationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the multi-node operation as a whole was successful.
        /// For example, this would be true if all critical tasks within the operation succeeded, and false otherwise.
        /// </summary>
        /// <example>true</example>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the final, complete state of the <see cref="SiteKeeper.Master.Model.InternalData.Operation"/> object
        /// after the multi-node stage has finished execution and all node tasks have reached a terminal state.
        /// This includes the status and results of all individual slave tasks.
        /// </summary>
        [Required]
        public Operation FinalOperationState { get; set; } = null!; // Initialized to null! with [Required] to indicate it must be provided.
    }
} 