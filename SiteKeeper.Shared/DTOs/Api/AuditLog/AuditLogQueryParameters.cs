using System;

namespace SiteKeeper.Shared.DTOs.API.AuditLog
{
    /// <summary>
    /// Defines parameters for querying audit log entries, aligned with Swagger for /audit-log.
    /// </summary>
    /// <remarks>
    /// This DTO is typically used as query parameters for an API endpoint that retrieves audit logs.
    /// It allows filtering by date range, user, action type, and pagination.
    /// </remarks>
    public class AuditLogQueryParameters
    {
        /// <summary>
        /// Optional. Filter logs from this date (UTC).
        /// Corresponds to Swagger 'startDate' parameter.
        /// </summary>
        /// <example>"2023-10-01"</example>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Optional. Filter logs up to this date (UTC).
        /// Corresponds to Swagger 'endDate' parameter.
        /// </summary>
        /// <example>"2023-10-31"</example>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Optional. Filter entries by the user who performed the action.
        /// Corresponds to Swagger 'user' parameter.
        /// </summary>
        /// <example>"admin@example.com"</example>
        public string? User { get; set; }

        /// <summary>
        /// Optional. Filter entries by the type of operation performed.
        /// Corresponds to Swagger 'operationType' parameter.
        /// </summary>
        /// <example>"UserLogin"</example>
        public string? OperationType { get; set; }

        /// <summary>
        /// Optional. Text for fuzzy filtering across relevant fields.
        /// Corresponds to Swagger 'filterText' parameter.
        /// </summary>
        /// <example>"Server01"</example>
        public string? FilterText { get; set; }

        /// <summary>
        /// Optional. Page number for pagination (1-indexed).
        /// Corresponds to Swagger 'page' parameter.
        /// </summary>
        /// <example>1</example>
        public int? Page { get; set; }

        /// <summary>
        /// Optional. Number of items per page for pagination.
        /// Corresponds to Swagger 'pageSize' parameter.
        /// </summary>
        /// <example>20</example>
        public int? PageSize { get; set; }

        /// <summary>
        /// Optional. Specifies the field to sort by. 
        /// Common values: "Timestamp", "User", "OperationType".
        /// Corresponds to Swagger 'sortBy' parameter.
        /// </summary>
        /// <example>"Timestamp"</example>
        public string? SortBy { get; set; }

        /// <summary>
        /// Optional. Specifies the sort order. "asc" for ascending, "desc" for descending.
        /// Corresponds to Swagger 'sortOrder' parameter.
        /// </summary>
        /// <example>"desc"</example>
        public string? SortOrder { get; set; }
    }
} 