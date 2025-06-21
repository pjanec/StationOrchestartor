using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Defines the high-level lifecycle status of a top-level MasterAction workflow.
    /// This is distinct from the more granular NodeActionOverallStatus used for internal stages.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MasterActionStatus
    {
        /// <summary>
        /// The action has been created and is pending execution.
        /// </summary>
        Initiated,

        /// <summary>
        /// The action is actively being processed. This is the general state for any running stage.
        /// </summary>
        InProgress,

        /// <summary>
        /// A cancellation request has been received, and the workflow is terminating.
        /// </summary>
        Cancelling,

        /// <summary>
        /// The action completed successfully. (Terminal State)
        /// </summary>
        Succeeded,

        /// <summary>
        /// The action failed due to an error. (Terminal State)
        /// </summary>
        Failed,

        /// <summary>
        /// The action was cancelled by a user or system request. (Terminal State)
        /// </summary>
        Cancelled
    }
}
