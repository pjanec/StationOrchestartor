using SiteKeeper.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Authentication
{
    /// <summary>
    /// Contains information about an authenticated user.
    /// </summary>
    /// <remarks>
    /// This DTO is typically included in authentication responses (e.g., <see cref="AuthResponse"/>)
    /// to provide the client with details about the user who has successfully logged in.
    /// It includes the username and their assigned role within the SiteKeeper system.
    /// Based on the UserInfo schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class UserInfo
    {
        /// <summary>
        /// Username of the authenticated user.
        /// </summary>
        /// <example>"admin"</example>
        [Required]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// The role assigned to the authenticated user.
        /// This determines their level of access and permissions within the system.
        /// </summary>
        /// <example>UserRole.AdvancedAdmin</example>
        [Required]
        public UserRole Role { get; set; }
    }
} 