using SiteKeeper.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Environment
{
    /// <summary>
    /// Represents the core status information for the environment dashboard.
    /// </summary>
    /// <remarks>
    /// This DTO provides a comprehensive overview of the SiteKeeper managed environment's current state.
    /// It includes the environment name, current version, overall software status, summaries for running apps,
    /// node statuses, diagnostics, and information about the current and last completed operations.
    /// This is a key DTO for the main dashboard UI.
    /// Based on the EnvironmentStatusResponse schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class EnvironmentStatusResponse
    {
        /// <summary>
        /// Name of the current environment being managed (e.g., "Production-SiteA", "Staging-Lab").
        /// </summary>
        /// <example>"Production-SiteA"</example>
        [Required]
        public string EnvironmentName { get; set; } = string.Empty;

        /// <summary>
        /// Identifier of the currently applied environment version or manifest ID.
        /// </summary>
        /// <example>"v1.2.3-patch4"</example>
        [Required]
        public string CurrentVersionId { get; set; } = string.Empty;

        /// <summary>
        /// Overall status of the managed software suite within the environment.
        /// </summary>
        [Required]
        public SystemSoftwareOverallStatus SystemSoftwareStatus { get; set; }

        /// <summary>
        /// Summary of running applications.
        /// </summary>
        [Required]
        public AppsRunningSummaryInfo AppsRunningSummary { get; set; } = new AppsRunningSummaryInfo();

        /// <summary>
        /// Summary of node statuses (total, online, offline).
        /// </summary>
        [Required]
        public NodesSummaryInfo NodesSummary { get; set; } = new NodesSummaryInfo();

        /// <summary>
        /// Summary of the overall diagnostic status of the environment.
        /// </summary>
        [Required]
        public DiagnosticsSummaryInfo DiagnosticsSummary { get; set; } = new DiagnosticsSummaryInfo();

        /// <summary>
        /// Summary of the currently ongoing asynchronous operation, if any.
        /// Null if no operation is currently active.
        /// </summary>
        public OngoingOperationSummary? CurrentOperation { get; set; }

        /// <summary>
        /// Summary of the last completed asynchronous operation, if any.
        /// Null if no operations have completed yet or if this information is not tracked/available.
        /// </summary>
        public CompletedOperationSummary? LastCompletedOperation { get; set; }
    }
} 