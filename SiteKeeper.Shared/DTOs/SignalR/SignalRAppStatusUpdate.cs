using SiteKeeper.Shared.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.SignalR
{
    /// <summary>
    /// DTO for sending real-time updates about a specific application's status via SignalR.
    /// As defined in swagger: #/components/schemas/SignalRAppStatusUpdate
    /// </summary>
    /// <remarks>
    /// The Master Agent's <c>GuiHub</c> would send messages of this type to clients to inform them
    /// about changes in an application's operational status (e.g., Running, Stopped, Error) on a particular node.
    /// Based on the SignalRAppStatusUpdate schema in `web api swagger.yaml`.
    /// </remarks>
    public class SignalRAppStatusUpdate
    {
        /// <summary>
        /// The globally unique identifier for the application instance.
        /// Expected format is "NodeName.AppName".
        /// </summary>
        /// <example>"SIMSERVER.MainAppService"</example>
        [Required]
        [JsonPropertyName("appId")]
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// The name of the application whose status is being updated.
        /// </summary>
        /// <example>"MainAppService"</example>
        [Required]
        [JsonPropertyName("appName")]
        public string AppName { get; set; } = string.Empty;

        /// <summary>
        /// The name of the node where this application instance resides and whose status changed.
        /// </summary>
        /// <example>"SIMSERVER"</example>
        [Required]
        [JsonPropertyName("nodeName")]
        public string NodeName { get; set; } = string.Empty;

        /// <summary>
        /// The new operational status of the application.
        /// </summary>
        /// <example>AppOperationalStatus.Stopped</example>
        [Required]
        [JsonPropertyName("newStatus")]
        public AppOperationalStatus Status { get; set; }

        /// <summary>
        /// The name of the plan this app belongs to, if any.
        /// </summary>
        /// <example>"CoreServices"</example>
        [JsonPropertyName("planName")]
        public string? PlanName { get; set; }

        /// <summary>
        /// How long (in seconds) this status has been reported.
        /// </summary>
        /// <example>10</example>
        [JsonPropertyName("statusAgeSeconds")]
        public int StatusAgeSeconds { get; set; }

        /// <summary>
        /// Last exit code if the app stopped or failed.
        /// </summary>
        /// <example>"0"</example>
        [JsonPropertyName("exitCode")]
        public string? ExitCode { get; set; }
    }
} 