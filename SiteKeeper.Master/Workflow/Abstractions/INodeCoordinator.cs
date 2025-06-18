using System;
using System.Threading;
using System.Threading.Tasks;
using SiteKeeper.Master.Workflow.DTOs; // DTOs might still be needed for StageProgress or others
using SiteKeeper.Master.Model.InternalData; // For Operation

namespace SiteKeeper.Master.Abstractions.Workflow
{
    /// <summary>
    /// Represents a coordinator responsible for executing an operation across multiple nodes
    /// and returning a consolidated result.
    /// </summary>
    /// <typeparam name="TOutput">The strongly-typed result produced by this coordinator.</typeparam>
    public interface INodeCoordinator<TOutput>
    {
        /// <summary>
        /// Executes the node coordination logic for a given operation.
        /// </summary>
        /// <param name="action">The operation to be executed across nodes. This will be renamed to NodeAction later.</param>
        /// <param name="context">The shared context of the parent Master Action, used for logging and state sharing.</param>
        /// <param name="progress">A reporter to send real-time progress updates (percentage and status message).</param>
        /// <param name="cancellationToken">A token to signal cancellation of the coordination.</param>
        /// <returns>The result of the node coordination.</returns>
        Task<TOutput> ExecuteAsync(
            Operation action, // Will be NodeAction later
            MasterActionContext context,
            IProgress<StageProgress> progress,
            CancellationToken cancellationToken
        );
    }
}