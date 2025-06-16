using System;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.SignalR
{
    /// <summary>
    /// DTO for a test response sent from the SignalR server to a client.
    /// </summary>
    /// <remarks>
    /// This DTO is sent by a SignalR hub (e.g., <c>GuiHub</c>) in response to a client's test request.
    /// It includes the original message, a response message, server timestamp, and processing duration.
    /// Corresponds to the <c>ServerToClientTestResponse</c> schema in `web api swagger.yaml`.
    /// </remarks>
    public class SignalRServerToClientTestResponse
    {
        /// <summary>
        /// The original message received from the client in the test request.
        /// </summary>
        /// <example>"Hello Server, this is a test!"</example>
        [Required]
        public string OriginalRequestMessage { get; set; } = string.Empty;

        /// <summary>
        /// A response message from the server.
        /// </summary>
        /// <example>"Hello Client, test received and processed!"</example>
        [Required]
        public string ResponseMessage { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp (UTC) when the server processed the request and generated this response.
        /// </summary>
        /// <example>"2023-10-27T11:00:01Z"</example>
        [Required]
        public DateTime ServerTimestamp { get; set; }

        /// <summary>
        /// The time in milliseconds it took for the server to process the test request.
        /// </summary>
        /// <example>15</example>
        [Required]
        public long ProcessingDurationMs { get; set; }
    }
} 