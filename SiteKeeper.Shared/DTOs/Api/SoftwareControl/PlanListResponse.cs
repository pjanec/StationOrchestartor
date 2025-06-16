using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.SoftwareControl
{
    /// <summary>
    /// DTO for the response when listing application plans.
    /// As defined in swagger: #/components/schemas/PlanListResponse
    /// </summary>
    public class PlanListResponse
    {
        /// <summary>
        /// List of application plans.
        /// </summary>
        [JsonPropertyName("plans")]
        public List<PlanInfo> Plans { get; set; }

        public PlanListResponse()
        {
            Plans = new List<PlanInfo>();
        }
    }
} 