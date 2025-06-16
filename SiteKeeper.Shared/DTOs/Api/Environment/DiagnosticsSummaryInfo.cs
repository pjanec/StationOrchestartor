using SiteKeeper.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Environment
{
    /// <summary>
    /// Provides a summary of the overall diagnostic status for the environment.
    /// </summary>
    /// <remarks>
    /// This DTO is typically included in broader status responses (e.g., <see cref="EnvironmentStatusResponse"/>)
    /// to give a quick indication of the system's health based on diagnostic checks (e.g., OK, Warnings, Errors).
    /// Based on the DiagnosticsSummaryInfo schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class DiagnosticsSummaryInfo
    {
        /// <summary>
        /// The overall diagnostic status summary for the environment.
        /// </summary>
        /// <example>DiagnosticsOverallStatus.OK</example>
        [Required]
        public DiagnosticsOverallStatus Status { get; set; }
    }
} 