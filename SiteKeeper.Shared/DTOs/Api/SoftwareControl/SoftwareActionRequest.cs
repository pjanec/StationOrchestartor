using System.Collections.Generic;

namespace SiteKeeper.Shared.DTOs.API.SoftwareControl
{
    /// <summary>
    /// Represents the request body for initiating actions on system-wide software, specific applications, or application plans.
    /// </summary>
    /// <remarks>
    /// This DTO is used for operations like starting, stopping, or restarting the entire software suite,
    /// a single application, or a defined plan of applications. The specific software entity is often implied by the endpoint
    /// (e.g., /system/start vs /apps/{appName}/start) or can be specified in the request.
    /// Based on the SoftwareActionRequest schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class SoftwareActionRequest
    {
        /// <summary>
        /// Optional list of specific node names where the software action should be applied.
        /// If null or empty, the action typically applies to all relevant nodes for the targeted software entity
        /// (e.g., all nodes for system-wide actions, or nodes where a specific app/plan is deployed).
        /// </summary>
        /// <example>["AppServer01", "AppServer02"]</example>
        public List<string>? NodeNames { get; set; }

        // Depending on the API design, an AppName or PlanName might be needed here if the
        // endpoint is generic (e.g., /software/action). However, if endpoints are specific
        // (e.g., /apps/{appName}/start), then these fields are implicit and not needed in the DTO.
        // The swagger.yaml and API DTO Definitions MD suggest these are often implicit.
        // For a /system/* action, no specific name is needed beyond the action itself.
    }
} 