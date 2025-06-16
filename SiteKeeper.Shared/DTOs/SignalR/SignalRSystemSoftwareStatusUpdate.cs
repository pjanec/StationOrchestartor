using SiteKeeper.Shared.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.SignalR
{
    /// <summary>
    /// DTO for sending real-time updates about the overall status of the managed system software via SignalR.
    /// As defined in swagger: #/components/schemas/SignalRSystemSoftwareStatusUpdate
    /// </summary>
    /// <remarks>
    /// The Master Agent's <c>GuiHub</c> sends messages of this type to clients to inform them about changes
    /// in the collective status of the entire software suite (e.g., Running, Stopped, Error).
    /// Based on the SignalRSystemSoftwareStatusUpdate schema in `web api swagger.yaml`.
    /// </remarks>
    public class SignalRSystemSoftwareStatusUpdate
    {
        /// <summary>
        /// The new overall status of the system software.
        /// </summary>
        /// <example>SystemSoftwareOverallStatus.PartiallyRunning</example>
        [Required]
        [JsonPropertyName("overallStatus")]
        public SystemSoftwareOverallStatus OverallStatus { get; set; }

        /// <summary>
        /// The number of currently running applications.
        /// </summary>
        /// <example>5</example>
        [JsonPropertyName("appsRunning")]
        public int AppsRunning { get; set; }

        /// <summary>
        /// The total number of manageable applications in the system.
        /// </summary>
        /// <example>36</example>
        [JsonPropertyName("appsTotal")]
        public int AppsTotal { get; set; }
    }
} 