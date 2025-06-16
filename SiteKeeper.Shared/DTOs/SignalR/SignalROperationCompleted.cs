using SiteKeeper.Shared.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.SignalR
{
    /// <summary>
    /// DTO for sending a notification via SignalR when an asynchronous operation has completed.
    /// As defined in swagger: #/components/schemas/SignalROperationCompleted
    /// </summary>
    /// <remarks>
    /// The Master Agent's <c>GuiHub</c> sends messages of this type to clients to inform them about the final outcome
    /// of an operation (Success, Failure, Cancelled).
    /// This is distinct from <see cref="SignalROperationProgress"/> which provides intermediate updates.
    /// Based on the SignalROperationCompleted schema in `web api swagger.yaml`.
    /// </remarks>
    public class SignalROperationCompleted
    {
        /// <summary>
        /// The unique identifier of the operation that completed.
        /// </summary>
        /// <example>"op-envupdate-abc123"</example>
        [Required]
        [JsonPropertyName("operationId")]
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// The final status/outcome of the operation.
        /// </summary>
        /// <example>CompletedOperationFinalStatus.Success</example>
        [Required]
        [JsonPropertyName("finalStatus")]
        public CompletedOperationFinalStatus FinalStatus { get; set; }

        /// <summary>
        /// An optional final message or summary related to the operation's completion, as a JSON string.
        /// This might include a success message, error details, or a summary of results.
        /// </summary>
        /// <example>"{ \"message\": \"Environment successfully updated to version 1.2.5.\" }"</example>
        [JsonPropertyName("resultDetailsJson")]
        public string? ResultDetailsJson { get; set; }
    }
} 