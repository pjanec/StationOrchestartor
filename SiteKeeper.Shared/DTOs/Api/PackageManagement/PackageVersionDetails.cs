using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.PackageManagement
{
    /// <summary>
    /// Provides detailed information about a specific version of a package.
    /// Based on the PackageVersionDetails schema in `web api swagger.yaml`.
    /// </summary>
    public class PackageVersionDetails
    {
        /// <summary>
        /// The specific version string of the package.
        /// </summary>
        /// <example>"1.5.1"</example>
        [Required]
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if this version is part of the current pure manifest for the package.
        /// </summary>
        /// <example>true</example>
        [JsonPropertyName("isCurrentPure")]
        public bool IsCurrentPure { get; set; }

        /// <summary>
        /// List of node names where this specific package version is currently installed.
        /// </summary>
        /// <example>["SIMSERVER", "IOS1"]</example>
        [JsonPropertyName("nodesInstalled")]
        public List<string> NodesInstalled { get; set; } = new List<string>();

        /// <summary>
        /// List of node names where this specific package version is defined in the manifest but not installed.
        /// </summary>
        /// <example>["IOS2"]</example>
        [JsonPropertyName("nodesNotInstalled")]
        public List<string> NodesNotInstalled { get; set; } = new List<string>();

        /// <summary>
        /// The date and time when this package version was uploaded or made available.
        /// Nullable if the information is not available.
        /// </summary>
        /// <example>"2025-05-10T14:30:00Z"</example>
        [JsonPropertyName("uploadDate")]
        public DateTime? UploadDate { get; set; }

        /// <summary>
        /// Optional release notes or description for this package version.
        /// </summary>
        /// <example>"Bug fixes and performance improvements."</example>
        [JsonPropertyName("releaseNotes")]
        public string? ReleaseNotes { get; set; }
    }
} 