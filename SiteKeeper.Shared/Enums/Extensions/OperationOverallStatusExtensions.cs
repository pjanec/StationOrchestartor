using SiteKeeper.Shared.Enums;

namespace SiteKeeper.Shared.Enums.Extensions
{
    /// <summary>
    /// Provides extension methods for the <see cref="NodeActionOverallStatus"/> enum.
    /// </summary>
    public static class OperationOverallStatusExtensions
    {
        /// <summary>
        /// Determines if the operation status represents a completed (terminal) state.
        /// </summary>
        /// <param name="status">The operation overall status.</param>
        /// <returns><c>true</c> if the status is Succeeded, SucceededWithErrors, Failed, or Cancelled; otherwise, <c>false</c>.</returns>
        public static bool IsCompleted(this NodeActionOverallStatus status)
        {
            return status == NodeActionOverallStatus.Succeeded ||
                   status == NodeActionOverallStatus.SucceededWithErrors ||
                   status == NodeActionOverallStatus.Failed ||
                   status == NodeActionOverallStatus.Cancelled;
        }
    }
} 