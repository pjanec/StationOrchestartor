using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Defines the types of offline sources that can be scanned for update bundles.
    /// </summary>
    /// <remarks>
    /// This enum is used by the <c>OfflineSource</c> DTO to categorize potential locations
    /// where offline update bundles might be found.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OfflineSourceType
    {
        /// <summary>
        /// A removable drive, such as a USB stick or external HDD.
        /// </summary>
        RemovableDrive,

        /// <summary>
        /// A network share.
        /// </summary>
        NetworkShare,

        /// <summary>
        /// A mounted ISO file.
        /// </summary>
        MountedISO,

        /// <summary>
        /// Other types of offline sources.
        /// </summary>
        Other
    }
} 