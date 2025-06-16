using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Authentication
{
    /// <summary>
    /// Represents the request body for a user login attempt.
    /// </summary>
    /// <remarks>
    /// This DTO is used by clients (e.g., the Web UI) to send user credentials to the authentication endpoint (e.g., POST /auth/login).
    /// It contains the username and password provided by the user.
    /// Based on the UserLoginRequest schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class UserLoginRequest
    {
        /// <summary>
        /// The user's username.
        /// </summary>
        /// <example>"admin"</example>
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// The user's password.
        /// </summary>
        /// <example>"P@$$wOrd"</example>
        [Required]
        [StringLength(100, MinimumLength = 6)] // Example validation, adjust as needed
        public string Password { get; set; } = string.Empty;
    }
} 