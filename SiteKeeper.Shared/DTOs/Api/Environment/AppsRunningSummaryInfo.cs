using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Environment
{
    /// <summary>
    /// Provides a summary of application running status within the environment.
    /// </summary>
    /// <remarks>
    /// This DTO is typically used as part of a larger dashboard or environment status response (e.g., <see cref="EnvironmentStatusResponse"/>)
    /// to give a quick overview of how many applications are running versus the total number of manageable applications.
    /// Based on the AppsRunningSummaryInfo schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class AppsRunningSummaryInfo
    {
        /// <summary>
        /// The number of applications currently confirmed to be in a running state.
        /// </summary>
        /// <example>5</example>
        [Required]
        [Range(0, int.MaxValue)]
        public int Running { get; set; }

        /// <summary>
        /// The total number of applications that are configured or managed within the environment.
        /// </summary>
        /// <example>10</example>
        [Required]
        [Range(0, int.MaxValue)]
        public int Total { get; set; }
    }
} 