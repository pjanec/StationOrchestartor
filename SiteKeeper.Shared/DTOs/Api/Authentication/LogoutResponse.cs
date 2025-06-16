using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Authentication
{
    /// <summary>
    /// Represents the response body after a successful user logout.
    /// </summary>
    /// <remarks>
    /// This DTO is returned by the logout endpoint (e.g., POST /auth/logout) to confirm that the user's session
    /// (primarily their refresh token) has been invalidated on the server side.
    /// Based on the LogoutResponse schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class LogoutResponse
    {
        /// <summary>
        /// A confirmation message indicating the result of the logout operation.
        /// </summary>
        /// <example>"Successfully logged out."</example>
        [Required]
        public string Message { get; set; } = string.Empty;
    }
} 