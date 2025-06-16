using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Diagnostics
{
    /// <summary>
    /// Represents basic information about an application discoverable for diagnostics.
    /// </summary>
    /// <remarks>
    /// This DTO is used within the <see cref="AppListResponse"/> for the GET /diagnostics/apps endpoint.
    /// It provides a minimal set of details to identify an application.
    /// Corresponds to the 'AppInfo' schema in `web api swagger.yaml`.
    /// </remarks>
    public class AppInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier for the application.
        /// </summary>
        /// <remarks>
        /// This ID is used to refer to the app in other diagnostic operations, 
        /// e.g., when fetching data package types for this app.
        /// </remarks>
        /// <example>"app-main-svc"</example>
        [Required]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user-friendly name of the application.
        /// </summary>
        /// <example>"MainAppService"</example>
        [Required]
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional description of the application.
        /// </summary>
        /// <example>"Core application service for primary system functions."</example>
        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
} 