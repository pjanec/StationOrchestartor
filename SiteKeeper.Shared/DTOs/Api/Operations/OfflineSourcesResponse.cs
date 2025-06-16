using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Operations // Confirmed uppercase API
{
    /// <summary>
    /// Represents the response containing a list of available offline update sources.
    /// </summary>
    /// <remarks>
    /// This DTO is returned by the GET /operations/offline-update/sources endpoint.
    /// It aligns with the 'OfflineSourcesResponse' schema defined in `web api swagger.yaml`.
    /// </remarks>
    public class OfflineSourcesResponse
    {
        /// <summary>
        /// Gets or sets the list of available offline update sources.
        /// </summary>
        [Required]
        [JsonPropertyName("sources")]
        public List<OfflineSource> Sources { get; set; } = new List<OfflineSource>();
    }
} 