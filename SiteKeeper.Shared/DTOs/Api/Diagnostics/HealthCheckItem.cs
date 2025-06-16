using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Diagnostics
{
    /// <summary>
    /// Represents a single health check item, potentially part of a hierarchy.
    /// As defined in swagger: #/components/schemas/HealthCheckItem
    /// </summary>
    public class HealthCheckItem
    {
        /// <summary>
        /// Unique identifier for the health check.
        /// </summary>
        /// <example>"disk.space.critical"</example>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// User-friendly name of the health check.
        /// </summary>
        /// <example>"Critical Disk Space Check"</example>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Optional description of the health check.
        /// </summary>
        /// <example>"Checks if any drive is above 90% utilization."</example>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// ID of the parent check for tree structure, if any.
        /// </summary>
        /// <example>"disk.space"</example>
        [JsonPropertyName("parentId")]
        public string? ParentId { get; set; }

        /// <summary>
        /// Child health checks, for hierarchical display.
        /// Null if this is a leaf node or if children are not applicable.
        /// </summary>
        [JsonPropertyName("children")]
        public List<HealthCheckItem>? Children { get; set; }
    }
} 