using SiteKeeper.Master.Model.InternalData;

namespace SiteKeeper.Master.Abstractions.Workflow
{
    /// <summary>
    /// Defines the input for a generalized multi-node operation stage.
    /// This DTO encapsulates the necessary information to initiate a distributed
    /// operation that was previously handled by the MultiNodeOperationCoordinatorService.
    /// </summary>
    public class MultiNodeOperationInput
    {
        /// <summary>
        /// The fully constructed Operation object, complete with its pre-filled list of NodeTasks,
        /// that the stage handler should execute.
        /// </summary>
        public Operation OperationToExecute { get; set; }
    }
} 