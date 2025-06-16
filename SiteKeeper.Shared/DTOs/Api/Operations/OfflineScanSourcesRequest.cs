using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Operations
{
    /// <summary>
    /// Represents the request to scan selected offline sources for update bundles.
    /// </summary>
    /// <remarks>
    /// This DTO is used for the POST /operations/offline-update/scan-sources endpoint.
    /// It aligns with the 'OfflineScanSourcesRequest' schema in `web api swagger.yaml`.
    /// </remarks>
    public class OfflineScanSourcesRequest
    {
        /// <summary>
        /// Gets or sets the list of source IDs (e.g., drive letters, share identifiers) to be scanned.
        /// This field is required.
        /// </summary>
        /// <example>["D:", "E_Updates"]</example>
        [Required]
        [JsonPropertyName("selectedSourceIds")]
        public List<string> SelectedSourceIds { get; set; } = new List<string>();
    }
} 