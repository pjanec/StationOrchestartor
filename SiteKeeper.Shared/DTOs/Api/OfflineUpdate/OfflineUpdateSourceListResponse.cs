using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.OfflineUpdate
{
    /// <summary>
    /// Represents a list of available offline update sources.
    /// Corresponds to the 'OfflineSourcesResponse' schema in `web api swagger.yaml`.
    /// </summary>
    /// <remarks>
    /// This DTO is returned by the API when querying for potential locations (like USB drives or network shares)
    /// that might contain offline update packages.
    /// </remarks>
    public class OfflineUpdateSourceListResponse
    {
        /// <summary>
        /// A list of offline update sources.
        /// </summary>
        [Required]
        [JsonPropertyName("sources")]
        public List<OfflineUpdateSourceInfo> Sources { get; set; } = new List<OfflineUpdateSourceInfo>();
    }
} 