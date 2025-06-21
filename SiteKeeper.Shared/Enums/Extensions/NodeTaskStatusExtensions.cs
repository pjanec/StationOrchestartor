using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiteKeeper.Shared.Enums.Extensions
{
    /// <summary>
    /// Provides extension methods for task status enums.
    /// This should ideally be in its own file within the project structure (e.g., SiteKeeper.Shared\Enums\Extensions).
    /// </summary>
    public static class NodeTaskStatusExtensions
    {
        /// <summary>
        /// Determines if the task status represents a final, terminal state.
        /// </summary>
        /// <param name="status">The node task status.</param>
        /// <returns><c>true</c> if the status is a terminal state; otherwise, <c>false</c>.</returns>
        public static bool IsTerminal(this NodeTaskStatus status)
        {
            switch (status)
            {
                // Early terminal states (before execution)
                case NodeTaskStatus.NotReadyForTask:
                case NodeTaskStatus.ReadinessCheckTimedOut:
                case NodeTaskStatus.DispatchFailed_Prepare:
                
                // Post-execution terminal states
                case NodeTaskStatus.Succeeded:
                case NodeTaskStatus.SucceededWithIssues:
                case NodeTaskStatus.Failed:
                case NodeTaskStatus.Cancelled:
                case NodeTaskStatus.CancellationFailed:
                case NodeTaskStatus.TaskDispatchFailed_Execute:
                case NodeTaskStatus.NodeOfflineDuringTask:
                case NodeTaskStatus.TimedOut:
                    return true;
                
                // Non-terminal states
                case NodeTaskStatus.Unknown:
                case NodeTaskStatus.Pending:
                case NodeTaskStatus.AwaitingReadiness:
                case NodeTaskStatus.ReadinessCheckSent:
                case NodeTaskStatus.ReadyToExecute:
                case NodeTaskStatus.TaskDispatched:
                case NodeTaskStatus.Starting:
                case NodeTaskStatus.InProgress:
                case NodeTaskStatus.Retrying:
                case NodeTaskStatus.Cancelling:
                default:
                    return false;
            }
        }
    }
}
