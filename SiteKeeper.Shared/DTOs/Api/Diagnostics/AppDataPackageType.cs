using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Diagnostics
{
    /// <summary>
    /// Represents a type of data package that can be collected for an application (e.g., error logs, full dumps).
    /// </summary>
    /// <remarks>
    /// This DTO is used within the <see cref="AppDataPackageTypesResponse"/> for the GET /diagnostics/apps/{appId}/data-package-types endpoint.
    /// Corresponds to the 'AppDataPackageType' schema in `web api swagger.yaml`.
    /// </remarks>
    public class AppDataPackageType
    {
        /// <summary>
        /// Gets or sets the unique identifier for this data package type.
        /// </summary>
        /// <example>"logs-error"</example>
        [Required]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user-friendly name of the data package type.
        /// </summary>
        /// <example>"Error Logs"</example>
        [Required]
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional description of what this data package type includes.
        /// </summary>
        /// <example>"Collects recent error log files from the application."</example>
        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
} 