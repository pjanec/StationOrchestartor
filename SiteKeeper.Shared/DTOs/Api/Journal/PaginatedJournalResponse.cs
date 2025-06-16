using SiteKeeper.Shared.DTOs.API.Environment; // For JournalEntrySummary
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Journal
{
    /// <summary>
    /// Represents a paginated response for a list of journal entries.
    /// Aligns with the PaginatedJournalResponse schema in `web api swagger.yaml`.
    /// </summary>
    /// <remarks>
    /// This DTO is used as the standard response format for API endpoints that return a list of journal entries
    /// with pagination support, such as the GET /journal endpoint.
    /// It includes metadata about the pagination (total items, page size, current page, total pages)
    /// and the list of <see cref="JournalEntrySummary"/> items for the current page.
    /// </remarks>
    public class PaginatedJournalResponse
    {
        /// <summary>
        /// Gets or sets the total number of journal entries available across all pages.
        /// Corresponds to the 'totalItems' field in Swagger.
        /// </summary>
        /// <example>100</example>
        [Required]
        public int TotalItems { get; set; }

        /// <summary>
        /// Gets or sets the total number of pages available based on the PageSize.
        /// Corresponds to the 'totalPages' field in Swagger.
        /// </summary>
        /// <example>5</example>
        [Required]
        public int TotalPages { get; set; }

        /// <summary>
        /// Gets or sets the current page number (1-indexed).
        /// Corresponds to the 'currentPage' field in Swagger.
        /// </summary>
        /// <example>1</example>
        [Required]
        public int CurrentPage { get; set; }

        /// <summary>
        /// Gets or sets the number of items included on each page.
        /// Corresponds to the 'pageSize' field in Swagger.
        /// </summary>
        /// <example>20</example>
        [Required]
        public int PageSize { get; set; }

        /// <summary>
        /// Gets or sets the list of journal entry summaries for the current page.
        /// Corresponds to the 'items' field in Swagger, which is an array of JournalEntrySummary.
        /// </summary>
        [Required]
        public List<JournalEntrySummary> Items { get; set; } = new List<JournalEntrySummary>();
    }
} 