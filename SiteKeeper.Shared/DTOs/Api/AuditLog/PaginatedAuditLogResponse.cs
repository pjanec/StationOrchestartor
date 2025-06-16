using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.AuditLog
{
    /// <summary>
    /// Represents a paginated response for audit log entries.
    /// Corresponds to the 'PaginatedAuditLogResponse' schema in `web api swagger.yaml`.
    /// </summary>
    public class PaginatedAuditLogResponse
    {
        /// <summary>
        /// Total number of audit log items matching the query.
        /// </summary>
        [Required]
        [JsonPropertyName("totalItems")]
        public int TotalItems { get; set; }

        /// <summary>
        /// Total number of pages available based on the pageSize and totalItems.
        /// </summary>
        [Required]
        [JsonPropertyName("totalPages")]
        public int TotalPages { get; set; }

        /// <summary>
        /// The current page number (1-indexed).
        /// </summary>
        [Required]
        [JsonPropertyName("currentPage")]
        public int CurrentPage { get; set; }

        /// <summary>
        /// The number of items per page.
        /// </summary>
        [Required]
        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        /// <summary>
        /// The list of audit log entries for the current page.
        /// </summary>
        [Required]
        [JsonPropertyName("items")]
        public List<AuditLogEntry> Items { get; set; } = new List<AuditLogEntry>();
    }
} 