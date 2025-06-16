using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Diagnostics
{
    /// <summary>
    /// Represents the response for a request to get available data package types for a specific application.
    /// </summary>
    /// <remarks>
    /// This DTO is returned by the GET /diagnostics/apps/{appId}/data-package-types endpoint.
    /// It includes metadata about the application and a list of <see cref="AppDataPackageType"/> that can be collected.
    /// Corresponds to the 'AppDataPackageTypesResponse' schema in `web api swagger.yaml`.
    /// </remarks>
    public class AppDataPackageTypesResponse
    {
        /// <summary>
        /// Gets or sets the unique identifier of the application for which data package types are listed.
        /// </summary>
        /// <example>"app-main-svc"</example>
        [Required]
        [JsonPropertyName("appId")]
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the application.
        /// </summary>
        /// <example>"MainAppService"</example>
        [Required]
        [JsonPropertyName("appName")]
        public string AppName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of available data package types for the application.
        /// </summary>
        [Required]
        [JsonPropertyName("dataPackageTypes")]
        public List<AppDataPackageType> DataPackageTypes { get; set; } = new List<AppDataPackageType>();
    }
} 