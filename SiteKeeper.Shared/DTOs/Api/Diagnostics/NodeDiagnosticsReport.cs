using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Diagnostics
{
    /// <summary>
    /// Represents a diagnostics report for a specific node.
    /// </summary>
    /// <remarks>
    /// This DTO provides a summary of the diagnostic checks performed on a node, including the overall health status,
    /// the time of the last check, and a list of any identified issues (<see cref="HealthCheckIssue"/>).
    /// Based on the NodeDiagnosticsReport schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class NodeDiagnosticsReport
    {
        /// <summary>
        /// The name of the node for which this diagnostic report applies.
        /// </summary>
        /// <example>"AppServer01"</example>
        [Required]
        public string NodeName { get; set; } = string.Empty;

        /// <summary>
        /// The overall health summary of the node based on the diagnostic checks.
        /// </summary>
        /// <example>NodeHealthSummary.Warning</example>
        [Required]
        public NodeHealthSummary OverallHealth { get; set; }

        /// <summary>
        /// Timestamp (UTC) when the last diagnostic check was performed on this node.
        /// </summary>
        /// <example>"2023-10-26T14:00:00Z"</example>
        [Required]
        public DateTime LastCheckTime { get; set; }

        /// <summary>
        /// A list of specific issues identified during the health check.
        /// This list will be empty if no issues were found.
        /// </summary>
        public List<HealthCheckIssue> Issues { get; set; } = new List<HealthCheckIssue>();
    }
} 