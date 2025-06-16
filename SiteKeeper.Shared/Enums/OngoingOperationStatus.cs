using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Represents the status of an asynchronous operation that is currently active or pending.
    /// </summary>
    /// <remarks>
    /// This enum is used in API responses (e.g., <c>OngoingOperationSummary</c> DTO) and SignalR messages
    /// to indicate the current state of an operation that has not yet reached a final terminal state.
    /// The value "Cancelling" was added for consistency with SignalR progress status as noted in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// Serialization to string is handled by <see cref="JsonStringEnumConverter"/>.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OngoingOperationStatus
    {
        /// <summary>
        /// The operation is actively being processed.
        /// </summary>
        InProgress,

        /// <summary>
        /// The operation has been initiated but is waiting for resources, readiness checks, or scheduling.
        /// </summary>
        Pending,

        /// <summary>
        /// A cancellation request has been issued for the operation, and it is in the process of stopping.
        /// </summary>
        Cancelling
    }
} 