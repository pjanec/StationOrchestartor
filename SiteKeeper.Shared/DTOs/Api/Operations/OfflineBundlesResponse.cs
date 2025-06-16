using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Operations
{
    /// <summary>
    /// Represents the response containing a list of offline update bundles found after a scan.
    /// </summary>
    /// <remarks>
    /// This DTO is returned by endpoints like POST /operations/offline-update/scan-sources (if synchronous)
    /// or GET /operations/offline-update/bundles.
    /// It aligns with the 'OfflineBundlesResponse' schema in `web api swagger.yaml`.
    /// </remarks>
    public class OfflineBundlesResponse
    {
        /// <summary>
        /// Gets or sets the list of found offline update bundles.
        /// </summary>
        [Required]
        [JsonPropertyName("bundles")]
        public List<OfflineBundleInfo> Bundles { get; set; } = new List<OfflineBundleInfo>();
    }
} 