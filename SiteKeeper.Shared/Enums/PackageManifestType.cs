using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Defines the type of a package as specified within an environment manifest file.
    /// </summary>
    /// <remarks>
    /// This enum categorizes packages listed in a manifest, which dictates how they are treated during deployments
    /// and environment management. For example, 'Core' packages might be mandatory, while 'Optional' packages
    /// can be installed or omitted based on specific needs.
    /// This enum is used in DTOs like <c>PackageInManifest</c>.
    /// Serialization to string is handled by <see cref="JsonStringEnumConverter"/> as per guidelines in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PackageManifestType
    {
        /// <summary>
        /// Represents a core package that is essential for the basic functionality of the system or application suite.
        /// These are typically always installed and kept up-to-date with the manifest.
        /// </summary>
        Core,

        /// <summary>
        /// Represents a core system-level package or component. Similar to 'Core' but may denote
        /// foundational system software rather than application-specific core components.
        /// </summary>
        CoreSystem,

        /// <summary>
        /// Represents an optional package that can be installed if needed but is not strictly required
        /// for the system's core operation. Its inclusion might depend on specific site requirements or user choices.
        /// </summary>
        Optional
    }
} 