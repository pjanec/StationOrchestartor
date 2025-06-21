using System;
using System.Threading;
using System.Threading.Tasks;
using SiteKeeper.Master.Model.InternalData;
using SiteKeeper.Master.Workflow.DTOs;

namespace SiteKeeper.Master.Abstractions.Workflow
{
    public interface INodeActionDispatcher
    {
        /// <summary>
        /// Executes the logic for this stage.
        /// </summary>
        /// <param name="input">The specific input data required for this stage.</param>
        /// <param name="context">The shared context of the parent Master Action, used for logging and state sharing.</param>
        /// <param name="progress">A reporter to send real-time progress updates (percentage and status message).</param>
        /// <param name="cancellationToken">A token to signal cancellation of the stage.</param>
        /// <returns>The result of the stage's execution.</returns>
        Task<NodeActionResult> ExecuteAsync(
            NodeAction input,
            MasterActionContext context,
            IProgress<StageProgress> progress,
            CancellationToken cancellationToken
        );
    }
} 