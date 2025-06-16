using SiteKeeper.Shared.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.SignalR
{
    /// <summary>
    /// DTO for sending a notification via SignalR that the Master service is going down.
    /// </summary>
    /// <remarks>
    /// The Master Agent's <c>GuiHub</c> would send this message to all connected clients
    /// to inform them of an impending shutdown of the Master service.
    /// This allows clients to take appropriate action, such as displaying a notification to the user.
    /// </remarks>
    public class SignalRMasterGoingDown
    {
        /// <summary>
        /// The reason why the Master service is shutting down.
        /// </summary>
        /// <example>MasterGoingDownReason.PlannedMaintenance</example>
        [Required]
        [JsonPropertyName("reason")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MasterGoingDownReason Reason { get; set; }

        /// <summary>
        /// An optional message providing more details about the shutdown.
        /// </summary>
        /// <example>"The system will be unavailable for approximately 30 minutes for scheduled maintenance."</example>
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
} 