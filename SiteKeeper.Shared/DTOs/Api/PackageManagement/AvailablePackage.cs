using SiteKeeper.Shared.DTOs.API.Environment;
using SiteKeeper.Shared.Enums;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.PackageManagement
{
    /// <summary>
    /// Represents a software package that is available for installation or management, often from a repository or manifest.
    /// </summary>
    /// <remarks>
    /// This DTO provides details about a package, including its name, available versions, description, type, and current installation status
    /// (if known for a specific context, e.g., when listing packages available to a specific node).
    /// Based on the AvailablePackage schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class AvailablePackage
    {
        /// <summary>
        /// Unique name or identifier of the package.
        /// </summary>
        /// <example>"MainApplicationSuite"</example>
        [Required]
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// A user-friendly description of the package.
        /// </summary>
        /// <example>"Core suite of applications for business operations."
        /// </example>
        public string? Description { get; set; }

        /// <summary>
        /// The general type of the package (e.g., Core, Optional, CoreSystem).
        /// </summary>
        /// <example>PackageType.Core</example>
        [Required]
        public PackageType Type { get; set; }

        /// <summary>
        /// A list of available versions for this package. Each version might have its own description or release notes.
        /// This would typically be populated from a package repository or manifest definitions.
        /// </summary>
        public List<PackageVersionInfo> AvailableVersions { get; set; } = new List<PackageVersionInfo>();

        /// <summary>
        /// The currently installed version of this package in the context where this DTO is used (e.g., on a specific node).
        /// Null if the package is not installed or if this information is not applicable to the current context.
        /// </summary>
        /// <example>"1.2.3"</example>
        public string? InstalledVersion { get; set; }

        /// <summary>
        /// The target version for this package as defined in the current active environment manifest.
        /// Null if the package is not in the manifest or no manifest is active.
        /// </summary>
        /// <example>"1.2.4"</example>
        public string? ManifestVersion { get; set; }
    }
} 