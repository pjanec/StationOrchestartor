using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Authentication
{
    /// <summary>
    /// Represents the response body after a successful user login.
    /// As defined in swagger: #/components/schemas/AuthResponse
    /// </summary>
    /// <remarks>
    /// This DTO is returned by the authentication endpoint upon successful validation of user credentials.
    /// It contains the access token (JWT) for API authorization, a refresh token for obtaining new access tokens,
    /// and information about the authenticated user (<see cref="UserInfo"/>).
    /// Based on the AuthResponse schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class AuthResponse
    {
        /// <summary>
        /// Information about the authenticated user, including their username and role.
        /// </summary>
        [Required]
        [JsonPropertyName("user")]
        public UserInfo UserInfo { get; set; } = new UserInfo();

        /// <summary>
        /// A short-lived JSON Web Token (JWT) used to authorize subsequent API requests.
        /// This token should be included in the Authorization header (Bearer scheme) of API calls.
        /// </summary>
        /// <example>"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJhZG1pbiIsInJvbGUiOiJBZHZhbmNlZEFkbWluIiwibmJmIjoxNjE2NDk2NjQwLCJleHAiOjE2MTY0OTc1NDAsImlhdCI6MTYxNjQ5NjY0MCwiaXNzIjoic2l0ZWtlZXBlci1hcGkiLCJhdWQiOiJzaXRla2VlcGVyLWNsaWVudCJ9.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c"</example>
        [Required]
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// A longer-lived token that can be used to obtain a new access token when the current one expires.
        /// Refresh tokens help maintain user sessions without requiring frequent re-login.
        /// </summary>
        /// <example>"aVeryLongAndSecureRefreshTokenStringGeneratedByTheServer"</example>
        [Required]
        [JsonPropertyName("refreshToken")]
        public string RefreshToken { get; set; } = string.Empty;
    }
} 