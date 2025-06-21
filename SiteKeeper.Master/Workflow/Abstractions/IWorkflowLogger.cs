using Microsoft.Extensions.Logging;

namespace SiteKeeper.Master.Abstractions.Workflow
{
    /// <summary>
    /// Defines a contract for a logger that is aware of the Master Action workflow context.
    /// This logger is registered as a scoped service, with one instance per running Master Action.
    /// </summary>
    public interface IWorkflowLogger : ILogger
    {
        /// <summary>
        /// Initializes the logger with the top-level context for a new Master Action.
        /// </summary>
        /// <param name="masterActionId">The unique ID of the Master Action.</param>
        void SetContext(string masterActionId);

        /// <summary>
        /// Updates the logger's context to the current stage of the workflow.
        /// </summary>
        /// <param name="stageIndex">The index of the current stage.</param>
        /// <param name="stageName">The name of the current stage.</param>
        void SetStage(int stageIndex, string stageName);
    }
}
