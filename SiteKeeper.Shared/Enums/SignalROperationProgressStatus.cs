using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Represents the status conveyed in SignalR messages for ongoing operation progress.
    /// </summary>
    /// <remarks>
    /// This enum is specifically used in the <c>SignalROperationProgress</c> DTO, which is sent from the Master Agent
    /// to UI clients via SignalR to provide real-time updates on an operation's state before it reaches a final outcome.
    /// The values are aligned with UI expectations for displaying progress, including a "Retrying" state.
    /// The "Cancelling" value was added as per discussion for UI clarity, as noted in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// Serialization to string is handled by <see cref="JsonStringEnumConverter"/>.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SignalROperationProgressStatus
    {
        /// <summary>
        /// The operation or a part of it is actively being processed.
        /// </summary>
        InProgress,

        /// <summary>
        /// The operation or a part of it is pending, awaiting resources or a previous step to complete.
        /// </summary>
        Pending,

        /// <summary>
        /// The operation or a part of it is currently being retried after a previous attempt failed.
        /// </summary>
        Retrying
    }
} 