using SiteKeeper.Shared.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization; // Required for JsonPropertyName

namespace SiteKeeper.Shared.DTOs.API.SoftwareControl
{
    /// <summary>
    /// Provides high-level information about a defined application plan, including its aggregated status.
    /// An application plan groups multiple applications that function as a logical unit.
    /// </summary>
    /// <remarks>
    /// This DTO is used to list available plans and their general status. 
    /// It aligns with the PlanInfo schema in `web api swagger.yaml` for the GET /plans endpoint.
    /// Individual application statuses within a plan might be retrieved via a more specific endpoint or through other means if necessary.
    /// </remarks>
    public class PlanInfo
    {
        /// <summary>
        /// The unique identifier of the application plan.
        /// </summary>
        /// <example>"plan-core-services"</example>
        [Required]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// The user-friendly name of the application plan.
        /// </summary>
        /// <example>"CoreServices"</example>
        [Required]
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty; // Renamed from PlanName

        /// <summary>
        /// A user-friendly description of the application plan.
        /// </summary>
        /// <example>"Essential background services for the platform."</example>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// The aggregated operational status of the entire plan.
        /// This is typically derived from the statuses of the individual applications within the plan.
        /// The string values should align with: NotRunning, Starting, Running, Stopping, Failing, PartiallyRunning, Unknown.
        /// </summary>
        /// <example>"Running"</example>
        [Required]
        [JsonPropertyName("status")]
        public PlanOperationalStatus Status { get; set; }

        // Removed Applications list to strictly align with Swagger for GET /plans
        // public List<AppStatusInfo> Applications { get; set; } = new List<AppStatusInfo>();
    }
} 