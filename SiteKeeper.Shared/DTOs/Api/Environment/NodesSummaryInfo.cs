using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Environment
{
    /// <summary>
    /// Provides a summary of node statuses within the environment.
    /// </summary>
    /// <remarks>
    /// This DTO is typically part of a larger dashboard or environment status response (e.g., <see cref="EnvironmentStatusResponse"/>).
    /// It gives a quick count of total nodes and how many are currently online versus offline.
    /// Based on the NodesSummaryInfo schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class NodesSummaryInfo
    {
        /// <summary>
        /// The total number of nodes configured or expected in the SiteKeeper managed environment.
        /// </summary>
        /// <example>4</example>
        [Required]
        [Range(0, int.MaxValue)]
        public int Total { get; set; }

        /// <summary>
        /// The number of nodes whose SiteKeeper agents are currently connected to the Master and reporting as online.
        /// </summary>
        /// <example>3</example>
        [Required]
        [Range(0, int.MaxValue)]
        public int Online { get; set; }

        /// <summary>
        /// The number of nodes whose SiteKeeper agents are currently not connected to the Master or are unresponsive.
        /// </summary>
        /// <example>1</example>
        [Required]
        [Range(0, int.MaxValue)]
        public int Offline { get; set; }
    }
} 