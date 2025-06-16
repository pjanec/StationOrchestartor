using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Authentication
{
    /// <summary>
    /// Represents the response body after successfully refreshing an access token.
    /// As defined in swagger: #/components/schemas/NewAccessTokenResponse
    /// </summary>
    /// <remarks>
    /// This DTO is returned by the token refresh endpoint (e.g., POST /auth/refresh) when a valid refresh token is provided.
    /// It contains a new JWT access token and, optionally, a new refresh token if token rotation is enabled.
    /// Based on the NewAccessTokenResponse schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class NewAccessTokenResponse
    {
        /// <summary>
        /// The new short-lived JWT access token.
        /// This token should be used for subsequent API requests.
        /// </summary>
        /// <example>"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJhZG1pbiIsInJvbGUiOiJBZHZhbmNlZEFkbWluIiwibmJmIjoxNjE2NDk3NjQwLCJleHAiOjE2MTY0OTg1NDAsImlhdCI6MTYxNjQ5NzY0MCwiaXNzIjoic2l0ZWtlZXBlci1hcGkiLCJhdWQiOiJzaXRla2VlcGVyLWNsaWVudCJ9.anotherSflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c"</example>
        [Required]
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// Access token validity in seconds.
        /// </summary>
        /// <example>3600</example>
        [Required]
        [JsonPropertyName("expiresIn")]
        public int ExpiresIn { get; set; }

        /// <summary>
        /// An optional new refresh token, provided if refresh token rotation is enabled.
        /// If present, the client should store this new refresh token and discard the old one.
        /// </summary>
        /// <example>"anotherVeryLongAndSecureRefreshTokenStringGeneratedByTheServer"</example>
        [JsonPropertyName("refreshToken")]
        public string? RefreshToken { get; set; }
    }
} 