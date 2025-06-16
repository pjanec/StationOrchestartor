using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Authentication
{
    /// <summary>
    /// Represents the request body for refreshing an access token.
    /// </summary>
    /// <remarks>
    /// This DTO is used by clients to request a new JWT access token using a valid, unexpired refresh token.
    /// This is typically sent to an endpoint like POST /auth/refresh.
    /// Based on the RefreshTokenRequest schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class RefreshTokenRequest
    {
        /// <summary>
        /// The refresh token that was previously issued to the client upon successful login.
        /// This token is used to obtain a new access token without requiring the user to re-enter credentials.
        /// </summary>
        /// <example>"aVeryLongAndSecureRefreshTokenStringGeneratedByTheServer"</example>
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
} 