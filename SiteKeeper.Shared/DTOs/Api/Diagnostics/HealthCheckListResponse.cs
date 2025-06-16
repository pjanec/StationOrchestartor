using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Diagnostics
{
    /// <summary>
    /// Represents a list of available health checks.
    /// Corresponds to the 'HealthCheckListResponse' schema in `web api swagger.yaml`.
    /// </summary>
    public class HealthCheckListResponse
    {
        /// <summary>
        /// A list of available health check definitions.
        /// </summary>
        [Required]
        [JsonPropertyName("healthChecks")] // Aligned with Swagger schema
        public List<HealthCheckItem> HealthChecks { get; set; } = new List<HealthCheckItem>();
    }
} 