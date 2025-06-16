using SiteKeeper.Shared.Enums;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Workflow
{
    /// <summary>
    /// Represents the "Workflow-as-Code" for a complete, high-level business process.
    /// Each implementation defines the sequence, logic, and error handling for a specific Master Action.
    /// </summary>
    public interface IMasterActionHandler
    {
        /// <summary>
        /// Gets the specific API operation type that this handler is responsible for.
        /// This is used by the coordinator to route incoming API requests to the correct handler.
        /// </summary>
        OperationType Handles { get; }

        /// <summary>
        /// Executes the entire workflow defined by this handler.
        /// This method contains the core logic, such as calling various stages in sequence,
        /// handling conditional logic (if/else), and implementing error recovery (try/catch).
        /// </summary>
        /// <param name="context">The shared context for this Master Action run, containing the logger, cancellation token, and other shared state.</param>
        Task ExecuteAsync(MasterActionContext context);
    }
} 