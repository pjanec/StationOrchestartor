using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Operations
{
    /// <summary>
    /// Standard response for initiating an asynchronous operation.
    /// Corresponds to the 'OperationInitiationResponse' schema in `web api swagger.yaml`.
    /// </summary>
    /// <remarks>
    /// This DTO is typically returned with a 202 Accepted status. The client can use the 
    /// <see cref="OperationId"/> to track the progress of the operation, often via SignalR notifications
    /// or by querying a journal/status endpoint.
    /// </remarks>
    public class OperationInitiationResponse
    {
        /// <summary>
        /// Unique ID for the initiated operation, used to track progress.
        /// </summary>
        /// <example>"op-generic-xyz123"</example>
        [Required]
        [JsonPropertyName("operationId")]
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// A human-readable message confirming the initiation.
        /// </summary>
        /// <example>"Operation initiated successfully."</example>
        [Required]
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
} 