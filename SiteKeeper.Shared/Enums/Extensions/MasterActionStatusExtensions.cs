using SiteKeeper.Shared.Enums;

namespace SiteKeeper.Shared.Enums.Extensions
{
    public static class MasterActionStatusExtensions
    {
        /// <summary>
        /// Determines if the MasterAction status represents a completed (terminal) state.
        /// </summary>
        public static bool IsCompleted(this MasterActionStatus status)
        {
            return status == MasterActionStatus.Succeeded ||
                   status == MasterActionStatus.Failed ||
                   status == MasterActionStatus.Cancelled;
        }
    }
}