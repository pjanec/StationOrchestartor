using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Defines the general type of a software package within the SiteKeeper system.
    /// </summary>
    /// <remarks>
    /// This enum is used to categorize packages, for example, when reporting on installed packages (<c>PackageOnNode</c> DTO)
    /// or managing package lifecycles. It includes types that align with <see cref="PackageManifestType"/> but also
    /// an 'Unknown' type for packages that may be discovered on a node but are not defined in the current manifest.
    /// Serialization to string is handled by <see cref="JsonStringEnumConverter"/> as per guidelines in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PackageType
    {
        /// <summary>
        /// A core package, typically essential for system functionality.
        /// </summary>
        Core,

        /// <summary>
        /// A core system-level package or component.
        /// </summary>
        CoreSystem,

        /// <summary>
        /// An optional package that is not strictly required for core operations.
        /// </summary>
        Optional,

        /// <summary>
        /// The type of the package cannot be determined or does not match known manifest types.
        /// This can be used for packages discovered on a system that are not part of the managed environment's definition.
        /// </summary>
        Unknown
    }
} 