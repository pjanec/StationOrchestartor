using Microsoft.AspNetCore.Mvc;
using System;

namespace SiteKeeper.Master.Web.Apis.QueryParameters
{
    /// <summary>
    /// Represents the query parameters for retrieving journal entries, aligning with the GET /journal endpoint in Swagger.
    /// </summary>
    /// <remarks>
    /// This DTO is used to capture and pass filter, sort, and pagination criteria when querying journal entries.
    /// Each property corresponds to a query parameter defined in the `web api swagger.yaml` for the journal listing operation.
    /// The `[FromQuery]` attribute specifies the exact query parameter name expected in the HTTP request.
    /// This class is designed to be used with the `[AsParameters]` attribute in Minimal API endpoint definitions.
    /// </remarks>
    public class JournalQueryParameters
    {
        /// <summary>
        /// Gets or sets the start date for filtering journal entries.
        /// Only entries with a timestamp on or after this date will be included.
        /// Corresponds to the 'startDate' query parameter in Swagger.
        /// </summary>
        /// <example>"2023-01-01"</example>
        [FromQuery(Name = "startDate")]
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Gets or sets the end date for filtering journal entries.
        /// Only entries with a timestamp on or before this date will be included.
        /// Corresponds to the 'endDate' query parameter in Swagger.
        /// </summary>
        /// <example>"2023-01-31"</example>
        [FromQuery(Name = "endDate")]
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Gets or sets the type of operation to filter journal entries by.
        /// For example, "EnvironmentBackup", "NodeRestart".
        /// Corresponds to the 'operationType' query parameter in Swagger.
        /// </summary>
        /// <example>"EnvironmentBackup"</example>
        [FromQuery(Name = "operationType")]
        public string? OperationType { get; set; }

        /// <summary>
        /// Gets or sets the text used for fuzzy filtering across relevant fields of journal entries.
        /// The specific fields searched are implementation-dependent.
        /// Corresponds to the 'filterText' query parameter in Swagger.
        /// </summary>
        /// <example>"failed"</example>
        [FromQuery(Name = "filterText")]
        public string? FilterText { get; set; }

        /// <summary>
        /// Gets or sets the column name to sort the journal entries by.
        /// Corresponds to the 'sortBy' query parameter in Swagger.
        /// </summary>
        /// <example>"timestamp"</example>
        [FromQuery(Name = "sortBy")]
        public string? SortBy { get; set; }

        /// <summary>
        /// Gets or sets the sort order for the journal entries.
        /// Allowed values are "asc" or "desc". Defaults to "asc" if not specified (or handled by service layer default).
        /// Corresponds to the 'sortOrder' query parameter in Swagger.
        /// </summary>
        /// <example>"desc"</example>
        [FromQuery(Name = "sortOrder")]
        public string? SortOrder { get; set; }

        /// <summary>
        /// Gets or sets the page number for pagination (1-indexed).
        /// Defaults to 1 as per Swagger.
        /// Corresponds to the 'page' query parameter in Swagger.
        /// </summary>
        /// <example>1</example>
        [FromQuery(Name = "page")]
        public int? Page { get; set; }

        /// <summary>
        /// Gets or sets the number of items per page for pagination.
        /// Defaults to 20 as per Swagger.
        /// Corresponds to the 'pageSize' query parameter in Swagger.
        /// </summary>
        /// <example>20</example>
        [FromQuery(Name = "pageSize")]
        public int? PageSize { get; set; }
    }
} 