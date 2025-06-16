using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SiteKeeper.Shared.Enums; // Required for OfflineSourceType

namespace SiteKeeper.Shared.DTOs.API.OfflineUpdate
{
    /// <summary>
    /// Represents information about a single offline update source.
    /// Corresponds to the 'OfflineSource' schema in `web api swagger.yaml`.
    /// </summary>
    /// <remarks>
    /// This DTO is used to convey details about potential locations from which offline update packages can be retrieved.
    /// It is typically part of a list in <see cref="OfflineUpdateSourceListResponse"/>.
    /// </remarks>
    public class OfflineUpdateSourceInfo
    {
        /// <summary>
        /// A unique identifier for the source.
        /// </summary>
        /// <example>"D:"</example>
        [Required]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// User-friendly name for the source.
        /// </summary>
        /// <example>"Drive D:\ Removable Storage"</example>
        [Required]
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Type of the source location.
        /// </summary>
        /// <example>RemovableDrive</example>
        [Required]
        [JsonPropertyName("type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public OfflineSourceType Type { get; set; }

        // Removed 'description', 'lastChecked', 'status' to align with Swagger 'OfflineSource' schema (id, displayName, type)
    }
} 