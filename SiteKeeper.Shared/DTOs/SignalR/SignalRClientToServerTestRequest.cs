using System;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.SignalR
{
    /// <summary>
    /// DTO for a test request sent from a SignalR client to the server.
    /// </summary>
    /// <remarks>
    /// This DTO is used by clients (e.g., GUI) to invoke a test method on a SignalR hub (e.g., <c>GuiHub</c>)
    /// to verify connectivity and basic request-response functionality.
    /// Corresponds to the <c>ClientToServerTestRequest</c> schema in `web api swagger.yaml`.
    /// </remarks>
    public class SignalRClientToServerTestRequest
    {
        /// <summary>
        /// A message payload from the client for the test request.
        /// </summary>
        /// <example>"Hello Server, this is a test!"</example>
        [Required]
        public string RequestMessage { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp (UTC) when the client generated this test request.
        /// </summary>
        /// <example>"2023-10-27T11:00:00Z"</example>
        [Required]
        public DateTime RequestTimestamp { get; set; }
    }
} 