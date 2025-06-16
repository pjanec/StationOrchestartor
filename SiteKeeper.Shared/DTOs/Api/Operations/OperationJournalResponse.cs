using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Operations
{
    /// <summary>
    /// Represents the response for a query to the operation journal.
    /// Contains a list of journal entries and the total count for pagination purposes.
    /// Aligns with the OperationJournalResponse schema in `web api swagger.yaml`.
    /// </summary>
    public class OperationJournalResponse
    {
        /// <summary>
        /// The list of operation journal entries for the current page/query.
        /// </summary>
        [JsonPropertyName("journalEntries")]
        public List<OperationJournalEntry> JournalEntries { get; set; } = new List<OperationJournalEntry>();

        /// <summary>
        /// The total number of journal entries available that match the query criteria (ignoring pagination).
        /// </summary>
        /// <example>150</example>
        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }
    }
} 