using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Diagnostics
{
    /// <summary>
    /// Represents the request to initiate a diagnostics run, allowing selection of specific checks and targets.
    /// </summary>
    /// <remarks>
    /// This DTO is typically used as the request body for an API endpoint that triggers a new diagnostics session,
    /// such as POST /diagnostics/runs or a similar endpoint.
    /// Based on the DiagnosticsRunRequest schema in `web api swagger.yaml`.
    /// </remarks>
    public class DiagnosticsRunRequest
    {
        /// <summary>
        /// Gets or sets a list of specific health check names to execute.
        /// If this list is null or empty, the system may interpret this as a request to run all applicable default health checks.
        /// </summary>
        /// <example>["DiskSpaceCheck", "DatabaseConnectivityCheck"]</example>
        public List<string>? CheckNames { get; set; }

        /// <summary>
        /// Gets or sets the optional name of a specific node (agent) to target for the diagnostics.
        /// If this is null or empty, the diagnostics may apply to the master server itself or the entire environment,
        /// depending on the context of the specific health checks being run.
        /// </summary>
        /// <example>"SIMSERVER"</example>
        public string? TargetNode { get; set; }
    }
} 