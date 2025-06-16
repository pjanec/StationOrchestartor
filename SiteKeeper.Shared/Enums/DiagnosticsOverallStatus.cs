using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Represents the overall summary of diagnostic statuses for the environment or a specific node/component.
    /// </summary>
    /// <remarks>
    /// This enum provides a consolidated view of the health based on diagnostic checks.
    /// It's used in dashboards and summary reports to quickly indicate if issues have been detected.
    /// Serialization to string is handled by <see cref="JsonStringEnumConverter"/> as per guidelines in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DiagnosticsOverallStatus
    {
        /// <summary>
        /// All diagnostic checks passed successfully; no issues detected.
        /// </summary>
        OK,

        /// <summary>
        /// Some non-critical issues or warnings were detected by diagnostic checks.
        /// The system might still be operational but requires attention.
        /// </summary>
        Warnings,

        /// <summary>
        /// Critical issues or errors were detected by diagnostic checks, potentially impacting system functionality.
        /// </summary>
        Errors,

        /// <summary>
        /// The diagnostic status cannot be determined, or checks have not been run recently.
        /// </summary>
        Unknown
    }
} 