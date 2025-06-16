using SiteKeeper.Shared.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Operations
{
    /// <summary>
    /// Represents a potential source location for offline update bundles.
    /// </summary>
    /// <remarks>
    /// This DTO is used to list available sources like removable drives or network shares 
    /// that can be scanned for update packages. It aligns with the 'OfflineSource' schema 
    /// defined in `web api swagger.yaml`.
    /// </remarks>
    public class OfflineSource
    {
        /// <summary>
        /// Gets or sets a unique identifier for the source.
        /// </summary>
        /// <example>"D:"</example>
        [Required]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user-friendly display name for the source.
        /// </summary>
        /// <example>"Drive D:\\ Removable Storage"</example>
        [Required]
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the source location.
        /// </summary>
        /// <example>OfflineSourceType.RemovableDrive</example>
        [Required]
        [JsonPropertyName("type")]
        public OfflineSourceType Type { get; set; }
    }
} 