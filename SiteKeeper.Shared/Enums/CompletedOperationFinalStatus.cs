using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Represents the final outcome of an asynchronous operation that has finished processing.
    /// </summary>
    /// <remarks>
    /// This enum is used in API responses (e.g., <c>CompletedOperationSummary</c> DTO) and SignalR messages
    /// (e.g., <c>SignalROperationCompleted</c>) to indicate the definitive result of an operation.
    /// It corresponds to the <c>OperationFinalOutcome</c> enum used internally by the Master Agent
    /// (see "SiteKeeper - Master - Data Structures.md").
    /// Serialization to string is handled by <see cref="JsonStringEnumConverter"/> as per guidelines in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CompletedOperationFinalStatus
    {
        /// <summary>
        /// The operation completed successfully, and all its objectives were met.
        /// </summary>
        Success,

        /// <summary>
        /// The operation failed to complete successfully due to errors or other issues.
        /// This may include scenarios where critical tasks failed or the operation could not achieve its primary goal.
        /// </summary>
        Failure,

        /// <summary>
        /// The operation was cancelled before it could complete naturally.
        /// </summary>
        Cancelled
    }
} 