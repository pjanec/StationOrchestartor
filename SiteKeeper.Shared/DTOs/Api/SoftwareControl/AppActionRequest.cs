using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.SoftwareControl
{
    /// <summary>
    /// Represents the request body for performing an action on a specific application.
    /// Based on the AppActionRequest schema in `web api swagger.yaml`.
    /// </summary>
    public class AppActionRequest
    {
        /// <summary>
        /// Optional list of specific node names where the application action should be applied.
        /// If null or empty, the action may apply to all nodes where the app is managed,
        /// or be context-dependent based on the specific app and action.
        /// </summary>
        /// <example>["SIMSERVER", "IOS1"]</example>
        [JsonPropertyName("targetNodes")]
        public List<string>? TargetNodes { get; set; }

        /// <summary>
        /// Optional parameters specific to the action being performed (e.g., restart delay, specific component target).
        /// </summary>
        /// <example>{"delaySeconds": 10, "force": true}</example>
        [JsonPropertyName("actionParameters")]
        public Dictionary<string, object>? ActionParameters { get; set; }
    }
} 