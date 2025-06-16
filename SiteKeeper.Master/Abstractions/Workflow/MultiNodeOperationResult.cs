using SiteKeeper.Master.Model.InternalData;

namespace SiteKeeper.Master.Abstractions.Workflow
{
    /// <summary>
    /// Defines the result of a multi-node operation stage.
    /// This DTO encapsulates the outcome of a distributed operation, including
    /// its success status and the final state of the operation model.
    /// </summary>
    public class MultiNodeOperationResult
    {
        /// <summary>
        /// Indicates whether the multi-node operation as a whole was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// The final, complete state of the Operation object after the multi-node
        /// stage has finished. This includes the status of all individual slave tasks.
        /// </summary>
        public Operation FinalOperationState { get; set; }
    }
} 