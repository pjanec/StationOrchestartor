using SiteKeeper.Master.Model.InternalData;

namespace SiteKeeper.Master.Abstractions.Workflow
{
using System.ComponentModel.DataAnnotations; // For Required attribute

namespace SiteKeeper.Master.Abstractions.Workflow
{
    /// <summary>
    /// Defines the input for a generalized multi-node operation stage within a Master Action workflow.
    /// </summary>
    /// <remarks>
    /// This DTO encapsulates a pre-configured <see cref="SiteKeeper.Master.Model.InternalData.Operation"/> object,
    /// which includes a list of <see cref="SiteKeeper.Master.Model.InternalData.NodeTask"/>s ready for dispatch.
    /// It is typically constructed by a preceding stage or the main workflow handler and passed to a stage
    /// responsible for executing tasks across multiple nodes. The receiving stage will iterate through
    /// <see cref="OperationToExecute"/>'s NodeTasks and dispatch them to the respective slave agents,
    /// often using services like <see cref="Services.IAgentConnectionManagerService"/>.
    /// This structure standardizes the input for common multi-node execution patterns.
    /// </remarks>
    public class MultiNodeOperationInput
    {
        /// <summary>
        /// Gets or sets the fully constructed <see cref="SiteKeeper.Master.Model.InternalData.Operation"/> object,
        /// complete with its pre-filled list of <see cref="SiteKeeper.Master.Model.InternalData.NodeTask"/>s,
        /// that the stage handler should execute.
        /// </summary>
        [Required]
        public Operation OperationToExecute { get; set; } = null!; // Initialized to null! with [Required] to indicate it must be provided.
    }
} 