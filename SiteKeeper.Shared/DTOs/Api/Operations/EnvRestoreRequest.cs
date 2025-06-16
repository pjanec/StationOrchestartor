using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Operations
{
    /// <summary>
    /// Represents the request body for initiating an environment restore operation.
    /// </summary>
    /// <remarks>
    /// This DTO is used for the POST /operations/env-restore endpoint.
    /// It specifies the journal record (typically a backup record) from which to restore the environment.
    /// This class aligns with the 'EnvRestoreRequest' schema defined in `web api swagger.yaml`.
    /// </remarks>
    public class EnvRestoreRequest
    {
        /// <summary>
        /// Gets or sets the ID of the journal record (representing a backup) to restore from.
        /// This field is required.
        /// </summary>
        /// <example>"journal-backup-001"</example>
        [Required]
        [JsonPropertyName("journalRecordId")]
        public string JournalRecordId { get; set; } = string.Empty;
    }
} 