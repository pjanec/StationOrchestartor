using Microsoft.AspNetCore.Mvc;
using System;

namespace SiteKeeper.Master.Web.Apis.QueryParameters // Or a more general DTOs location if preferred
{
    /// <summary>
    /// Represents the query parameters for retrieving operation journal entries.
    /// Aligns with parameters defined for GET /operations/journal in `web api swagger.yaml`.
    /// </summary>
    public class OperationJournalQueryParameters
    {
        /// <summary>
        /// Optional. The start of the time range for journal entries (inclusive).
        /// Format: ISO 8601 DateTime (e.g., 2023-01-01T00:00:00Z).
        /// </summary>
        [FromQuery(Name = "from")]
        public DateTime? From { get; set; }

        /// <summary>
        /// Optional. The end of the time range for journal entries (inclusive).
        /// Format: ISO 8601 DateTime (e.g., 2023-01-31T23:59:59Z).
        /// </summary>
        [FromQuery(Name = "to")]
        public DateTime? To { get; set; }

        /// <summary>
        /// Optional. A general text filter to apply to journal entry messages, event types, or operation IDs.
        /// The specific fields searched are implementation-dependent on the service layer.
        /// </summary>
        /// <example>"Failed login"</example>
        [FromQuery(Name = "filter")]
        public string? Filter { get; set; }

        /// <summary>
        /// Optional. The number of entries to skip (for pagination).
        /// Defaults to 0 if not specified.
        /// </summary>
        /// <example>0</example>
        [FromQuery(Name = "offset")]
        public int? Offset { get; set; } = 0;

        /// <summary>
        /// Optional. The maximum number of entries to return.
        /// Defaults to 50 if not specified.
        /// </summary>
        /// <example>50</example>
        [FromQuery(Name = "limit")]
        public int? Limit { get; set; } = 50;
    }
} 