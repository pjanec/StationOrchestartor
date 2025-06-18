using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Diagnostics
{
    /// <summary>
    /// Represents a single health check item, which can be part of a hierarchical structure of available diagnostic checks.
    /// As defined in swagger: #/components/schemas/HealthCheckItem
    /// </summary>
    /// <remarks>
    /// This DTO is typically used to list available health checks that can be performed, often returned in a list by an endpoint
    /// like GET /diagnostics/health-checks (see <see cref="HealthCheckListResponse"/>).
    /// The <see cref="ParentId"/> and <see cref="Children"/> properties allow constructing a tree-like view in a UI.
    /// Note that this DTO describes the health check itself, not its execution status or any issues found, which are
    /// typically represented by other DTOs like <see cref="HealthCheckIssue"/> or within a <see cref="NodeDiagnosticsReport"/>.
    /// </remarks>
    public class HealthCheckItem
    {
        /// <summary>
        /// Gets or sets the unique identifier for this health check. This ID might be used to request execution of this specific check.
        /// </summary>
        /// <example>"disk.space.critical"</example>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user-friendly, display name of the health check.
        /// </summary>
        /// <example>"Critical Disk Space Check"</example>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional detailed description of what this health check does or verifies.
        /// </summary>
        /// <example>"Checks if any critical disk drive is above 90% utilization."</example>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the parent health check, if this item is part of a hierarchy.
        /// This allows for grouping related checks (e.g., "disk.space" could be a parent for specific drive checks).
        /// Null or empty if this is a top-level health check item.
        /// </summary>
        /// <example>"disk.space"</example>
        [JsonPropertyName("parentId")]
        public string? ParentId { get; set; }

        /// <summary>
        /// Gets or sets a list of child health check items. This is used to build a hierarchical (tree) structure of health checks.
        /// The list is null or empty if this health check item is a leaf node (has no sub-checks).
        /// </summary>
        [JsonPropertyName("children")]
        public List<HealthCheckItem>? Children { get; set; }
    }
} 