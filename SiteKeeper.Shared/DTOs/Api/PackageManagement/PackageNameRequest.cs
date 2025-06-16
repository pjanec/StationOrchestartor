using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.PackageManagement
{
    /// <summary>
    /// Represents a request that primarily identifies a package by its name.
    /// Used for operations like installing or uninstalling optional packages where only the package name is required in the request body.
    /// Based on the PackageNameRequest schema in `web api swagger.yaml`.
    /// </summary>
    public class PackageNameRequest
    {
        /// <summary>
        /// The name of the package.
        /// </summary>
        /// <example>"CoreApp-conf"</example>
        [Required]
        [JsonPropertyName("packageName")]
        public string PackageName { get; set; } = string.Empty;
    }
} 