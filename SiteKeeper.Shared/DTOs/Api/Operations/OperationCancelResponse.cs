using SiteKeeper.Shared.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Operations
{
    /// <summary>
    /// Represents the status of an operation cancellation request.
    /// Used in <see cref="OperationCancelResponse"/>.
    /// Aligns with the status enum in the 'OperationCancelResponse' schema in `web api swagger.yaml`.
    /// </summary>
    /// <remarks>
    /// The values `CancellationAccepted` and `Failed` were removed to conform to the Swagger definition.
    /// Logic previously using `CancellationAccepted` should likely now use `CancellationPending` or be re-evaluated.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OperationCancellationRequestStatus
    {
        /// <summary>
        /// The cancellation request has been accepted and is being processed (or is pending processing).
        /// </summary>
        CancellationPending,

        /// <summary>
        /// The operation had already completed by the time the cancellation was requested.
        /// </summary>
        AlreadyCompleted,

        /// <summary>
        /// The operation ID was not found.
        /// </summary>
        NotFound,

        /// <summary>
        /// The operation does not support cancellation.
        /// </summary>
        CancellationNotSupported
    }

    /// <summary>
    /// Represents the response to a request to cancel an ongoing operation.
    /// </summary>
    /// <remarks>
    /// This DTO is returned by the API endpoint that handles operation cancellation requests (e.g., POST /operations/{operationId}/cancel).
    /// It indicates the outcome of the cancellation attempt, such as whether the cancellation is pending,
    /// if the operation was already completed, or if it could not be found or cancelled.
    /// Based on the OperationCancelResponse schema in `web api swagger.yaml`.
    /// </remarks>
    public class OperationCancelResponse
    {
        /// <summary>
        /// Gets or sets the identifier of the operation for which cancellation was requested.
        /// Corresponds to the 'operationId' property in the Swagger schema.
        /// </summary>
        /// <example>"op-envupdate-abc123"</example>
        [Required]
        [JsonPropertyName("operationId")]
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the status of the cancellation request.
        /// Uses the conformant <see cref="OperationCancellationRequestStatus"/> enum.
        /// </summary>
        /// <example>OperationCancellationRequestStatus.CancellationPending</example>
        [Required]
        [JsonPropertyName("status")]
        public OperationCancellationRequestStatus Status { get; set; }

        /// <summary>
        /// Gets or sets a human-readable message providing more details about the cancellation status.
        /// Corresponds to the 'message' property in the Swagger schema.
        /// </summary>
        /// <example>"Cancellation request accepted. Operation is now being terminated."</example>
        [Required]
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
} 