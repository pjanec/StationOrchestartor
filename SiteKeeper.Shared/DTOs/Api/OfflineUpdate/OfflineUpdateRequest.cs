using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.OfflineUpdate
{
    /// <summary>
    /// Represents the request body for initiating an environment update using an offline bundle.
    /// </summary>
    /// <remarks>
    /// This DTO is used to trigger an <c>OperationType.EnvUpdateOffline</c> operation.
    /// It requires the ID of the offline source and the ID of the specific bundle on that source to be used for the update.
    /// Based on the OfflineUpdateRequest schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class OfflineUpdateRequest
    {
        /// <summary>
        /// The identifier of the offline source (e.g., USB drive, network share) from which to perform the update.
        /// This corresponds to an <see cref="OfflineSource.SourceId"/>.
        /// </summary>
        /// <example>"usb-drive-kingston-8gb"</example>
        [Required]
        public string SourceId { get; set; } = string.Empty;

        /// <summary>
        /// The identifier of the specific offline update bundle on the source to be used for the update.
        /// This corresponds to an <see cref="OfflineBundleInfo.BundleId"/>.
        /// </summary>
        /// <example>"bundle-prod-q4-2023-v1.2.4"</example>
        [Required]
        public string BundleId { get; set; } = string.Empty;
    }
} 