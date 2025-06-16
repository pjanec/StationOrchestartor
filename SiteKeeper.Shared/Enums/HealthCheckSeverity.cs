using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Represents the severity level of an issue found by a health check.
    /// </summary>
    /// <remarks>
    /// This enum is used within the <c>HealthCheckIssue</c> DTO to classify the impact or urgency of a detected health problem.
    /// It helps in prioritizing responses to health issues and can be used for filtering or color-coding in UI displays.
    /// Serialization to string is handled by <see cref="JsonStringEnumConverter"/> as per guidelines in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum HealthCheckSeverity
    {
        /// <summary>
        /// Indicates a critical issue that likely impacts system functionality or stability and requires immediate attention.
        /// </summary>
        Error,

        /// <summary>
        /// Indicates a non-critical issue that may not immediately impact functionality but could lead to problems
        /// if not addressed, or represents a deviation from best practices.
        /// </summary>
        Warning,

        /// <summary>
        /// Indicates an informational message or a minor issue that does not pose an immediate risk but might be of interest.
        /// </summary>
        Info
    }
} 