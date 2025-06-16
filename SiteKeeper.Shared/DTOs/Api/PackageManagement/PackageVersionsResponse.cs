using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.PackageManagement
{
    /// <summary>
    /// Represents a list of available versions for a specific package.
    /// This DTO aligns with the 'PackageVersionsResponse' schema defined in `web api swagger.yaml`.
    /// It contains the package name and a list of its available version strings.
    /// </summary>
    public class PackageVersionsResponse
    {
        /// <summary>
        /// The name of the package for which versions are listed.
        /// </summary>
        /// <example>"CoreApp-bin"</example>
        [Required]
        [JsonPropertyName("packageName")]
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// A list of available version strings for the package.
        /// </summary>
        /// <example>["1.0.0", "1.0.1", "1.1.0"]</example>
        [Required] // Assuming versions list is required if the package itself is found
        [JsonPropertyName("versions")]
        public List<string> Versions { get; set; } = new List<string>(); // Changed from List<PackageVersionDetails>
    }
} 