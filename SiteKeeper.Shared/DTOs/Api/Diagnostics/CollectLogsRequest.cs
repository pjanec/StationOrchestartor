using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Diagnostics
{
    /// <summary>
    /// Represents the request to collect logs or other data packages for a specific application.
    /// </summary>
    /// <remarks>
    /// This DTO is used as the request body for the POST /operations/diagnostics/collect-logs endpoint.
    /// It specifies the application, the type of data to collect, and the target nodes.
    /// Corresponds to the 'CollectLogsRequest' schema in `web api swagger.yaml`.
    /// </remarks>
    public class CollectLogsRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier of the application for which to collect logs.
        /// </summary>
        /// <example>"app-main-svc"</example>
        [Required]
        [JsonPropertyName("appId")]
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique identifier of the data package type to collect (e.g., "logs-error").
        /// </summary>
        /// <example>"logs-error"</example>
        [Required]
        [JsonPropertyName("dataPackageTypeId")]
        public string DataPackageTypeId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional list of specific node names to collect logs from.
        /// If <see cref="AllNodes"/> is true, this list might be ignored or used as a filter.
        /// </summary>
        /// <example>["IOS1"]</example>
        [JsonPropertyName("nodeNames")]
        public List<string>? NodeNames { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether logs should be collected from all applicable nodes where the app runs.
        /// If true, <see cref="NodeNames"/> might be ignored.
        /// </summary>
        /// <example>false</example>
        [JsonPropertyName("allNodes")]
        public bool? AllNodes { get; set; } = false;
    }
} 