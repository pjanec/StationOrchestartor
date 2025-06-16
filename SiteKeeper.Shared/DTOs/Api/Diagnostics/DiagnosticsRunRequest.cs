using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Diagnostics
{
    /// <summary>
    /// Represents the request to initiate a diagnostics run.
    /// Based on the DiagnosticsRunRequest schema in `web api swagger.yaml`.
    /// </summary>
    public class DiagnosticsRunRequest
    {
        /// <summary>
        /// Specific health check names to run. 
        /// If empty or null, all applicable checks will be run.
        /// </summary>
        /// <example>["DiskSpaceCheck", "DatabaseConnectivityCheck"]</example>
        public List<string>? CheckNames { get; set; }

        /// <summary>
        /// Optional node name to target for diagnostics. 
        /// If null, applies to the whole environment or master.
        /// </summary>
        /// <example>"SIMSERVER"</example>
        public string? TargetNode { get; set; }
    }
} 