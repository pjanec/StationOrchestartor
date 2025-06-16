using SiteKeeper.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.SignalR
{
    /// <summary>
    /// DTO for sending real-time updates about an application plan's status via SignalR.
    /// </summary>
    /// <remarks>
    /// The Master Agent's <c>GuiHub</c> would send messages of this type to clients to inform them
    /// about changes in the aggregated operational status of an application plan (e.g., Running, Stopped, PartiallyRunning).
    /// This DTO is related to <see cref="API.SoftwareControl.PlanInfo"/> but is focused on broadcasting status changes.
    /// Based on the SignalRPlanStatusUpdate schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class SignalRPlanStatusUpdate
    {
        /// <summary>
        /// The name of the application plan whose status is being updated.
        /// </summary>
        /// <example>"CoreServicesPlan"</example>
        [Required]
        public string PlanName { get; set; } = string.Empty;

        /// <summary>
        /// The new aggregated operational status of the plan.
        /// </summary>
        /// <example>PlanOperationalStatus.PartiallyRunning</example>
        [Required]
        public PlanOperationalStatus Status { get; set; }
    }
} 