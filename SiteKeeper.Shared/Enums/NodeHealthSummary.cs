using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Represents a summary of the health status of a managed node.
    /// </summary>
    /// <remarks>
    /// This enum provides a quick, high-level indication of a node's health, considering factors like
    /// resource utilization (CPU, RAM, disk), agent connectivity, and outcomes of recent health checks.
    /// It is used in DTOs like <c>NodeSummary</c> and <c>SignalRNodeStatusUpdate</c> for dashboard displays.
    /// The "Issues" value is a general term for problems, while "Warning" and "Error" can represent
    /// more specific states if detailed health aggregation logic is implemented.
    /// Serialization to string is handled by <see cref="JsonStringEnumConverter"/> as per guidelines in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum NodeHealthSummary
    {
        /// <summary>
        /// The node is operating within normal parameters, and no significant issues are detected.
        /// </summary>
        OK,

        /// <summary>
        /// The node has some general problems or potential issues that might not be critical but warrant investigation.
        /// This can be used when specific Warning/Error states are not granularly defined or as a catch-all.
        /// </summary>
        Issues,

        /// <summary>
        /// The node is experiencing conditions that could lead to problems if not addressed (e.g., high resource usage).
        /// </summary>
        Warning,

        /// <summary>
        /// The node has encountered a significant error or is in a critical state (e.g., critical service down, disk full).
        /// </summary>
        Error,

        /// <summary>
        /// The health status of the node cannot be determined (e.g., agent offline, health data not available).
        /// </summary>
        Unknown
    }
} 