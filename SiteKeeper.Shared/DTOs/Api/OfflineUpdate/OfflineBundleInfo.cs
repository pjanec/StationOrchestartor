using SiteKeeper.Shared.DTOs.API.Environment; // For PackageVersionInfo
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.OfflineUpdate
{
    /// <summary>
    /// Provides information about an offline update bundle discovered on an offline source.
    /// </summary>
    /// <remarks>
    /// This DTO describes a potential update package found by the system, including its name, the target environment version
    /// it represents, a description, and a list of software packages contained within the bundle.
    /// Based on the OfflineBundleInfo schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class OfflineBundleInfo
    {
        /// <summary>
        /// A unique identifier for this specific bundle, possibly derived from its content or metadata file.
        /// </summary>
        /// <example>"bundle-prod-q4-2023-v1.2.4"</example>
        [Required]
        public string BundleId { get; set; } = string.Empty;

        /// <summary>
        /// A user-friendly name for the update bundle.
        /// </summary>
        /// <example>"Production Environment Q4 2023 Update (v1.2.4)"</example>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Information about the target environment version this bundle is intended for.
        /// </summary>
        [Required]
        public PackageVersionInfo TargetVersionInfo { get; set; } = new PackageVersionInfo();

        /// <summary>
        /// A description of the update bundle, its contents, or purpose.
        /// </summary>
        /// <example>"Cumulative update including all patches and new features for Q4 2023."
        /// </example>
        public string? Description { get; set; }

        /// <summary>
        /// The identifier of the offline source where this bundle was found.
        /// Links back to an <see cref="OfflineSource.SourceId"/>.
        /// </summary>
        /// <example>"usb-drive-kingston-8gb"</example>
        [Required]
        public string SourceId { get; set; } = string.Empty;

        /// <summary>
        /// List of package names and their versions included in this offline update bundle.
        /// This provides a manifest of what the bundle contains.
        /// </summary>
        public List<PackageInManifest> ContainedPackages { get; set; } = new List<PackageInManifest>();
    }
} 