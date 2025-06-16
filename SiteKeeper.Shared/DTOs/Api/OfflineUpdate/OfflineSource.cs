using SiteKeeper.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.OfflineUpdate
{
    /// <summary>
    /// Represents an available offline source for software updates (e.g., a USB drive, network share).
    /// </summary>
    /// <remarks>
    /// This DTO is used to list sources discovered by the Master Agent that may contain offline update bundles.
    /// It includes details like the source ID, name, type, and path.
    /// Based on the OfflineSource schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class OfflineSource
    {
        /// <summary>
        /// A unique identifier for this offline source, assigned by the Master Agent upon discovery.
        /// </summary>
        /// <example>"usb-drive-kingston-8gb"</example>
        [Required]
        public string SourceId { get; set; } = string.Empty;

        /// <summary>
        /// A user-friendly name for the offline source (e.g., drive label, share name).
        /// </summary>
        /// <example>"Kingston DataTraveler G3 (E:)"</example>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The type of the offline source.
        /// </summary>
        /// <example>OfflineSourceType.RemovableDrive</example>
        [Required]
        public OfflineSourceType Type { get; set; }

        /// <summary>
        /// The file system path to the root of the offline source (e.g., drive letter, UNC path).
        /// </summary>
        /// <example>"E:\\"</example>
        [Required]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Optional description or additional details about the source.
        /// </summary>
        /// <example>"Contains Q4 update bundle for production systems."</example>
        public string? Description { get; set; }
    }
} 