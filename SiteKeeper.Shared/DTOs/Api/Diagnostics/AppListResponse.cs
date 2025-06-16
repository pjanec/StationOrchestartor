using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Diagnostics
{
    /// <summary>
    /// Represents the response for a request to list discoverable applications for diagnostics.
    /// </summary>
    /// <remarks>
    /// This DTO is returned by the GET /diagnostics/apps endpoint.
    /// It contains a list of <see cref="AppInfo"/> objects.
    /// Corresponds to the 'AppListResponse' schema in `web api swagger.yaml`.
    /// </remarks>
    public class AppListResponse
    {
        /// <summary>
        /// Gets or sets the list of discoverable applications.
        /// </summary>
        [Required]
        [JsonPropertyName("apps")]
        public List<AppInfo> Apps { get; set; } = new List<AppInfo>();
    }
} 