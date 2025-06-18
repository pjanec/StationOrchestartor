using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.DTOs.API.Authentication;
using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Services.Placeholders
{
    /// <summary>
    /// Placeholder implementation of the <see cref="IAuthenticationService"/>.
    /// Provides simulated authentication logic for development and testing purposes.
    /// </summary>
    /// <remarks>
    /// This service uses a predefined in-memory dictionary of users and their passwords.
    /// Upon successful login, it generates a dummy JWT access token using configuration settings
    /// from <see cref="MasterConfig"/> (for secret key, issuer, audience, and expiration)
    /// and creates a placeholder refresh token.
    /// The refresh token logic is also a simple placeholder validation.
    /// Logout is a simulated action that doesn't perform real token invalidation.
    /// </remarks>
    public class PlaceholderAuthenticationService : IAuthenticationService
    {
        private readonly ILogger<PlaceholderAuthenticationService> _logger;
        private readonly MasterConfig _masterConfig;
        internal readonly Dictionary<string, (string Password, UserInfo UserDetails)> _users = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaceholderAuthenticationService"/> class.
        /// Populates an in-memory list of test users.
        /// </summary>
        /// <param name="logger">The logger for recording service activity.</param>
        /// <param name="masterConfig">The <see cref="MasterConfig"/> containing settings for JWT generation (secret, issuer, audience, expiration).</param>
        public PlaceholderAuthenticationService(ILogger<PlaceholderAuthenticationService> logger, MasterConfig masterConfig)
        {
            _logger = logger;
            _masterConfig = masterConfig;
            _users.Add("observer", (Password:"password", new UserInfo { Username = "observer", Role = UserRole.Observer }));
            _users.Add("operator", (Password:"password", new UserInfo { Username = "operator", Role = UserRole.Operator }));
            _users.Add("basicadmin", (Password:"password", new UserInfo { Username = "basicadmin", Role = UserRole.BasicAdmin }));
            _users.Add("advancedadmin", (Password:"password", new UserInfo { Username = "advancedadmin", Role = UserRole.AdvancedAdmin }));
        }

        /// <summary>
        /// Placeholder implementation for user login.
        /// Validates credentials against an in-memory user list and generates dummy JWT access and refresh tokens upon success.
        /// </summary>
        /// <param name="username">The username to authenticate.</param>
        /// <param name="password">The password for the specified username.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains an <see cref="AuthResponse"/>
        /// with dummy tokens and user information if authentication is successful; otherwise, null.
        /// </returns>
        public Task<AuthResponse?> LoginAsync(string username, string password)
        {
            _logger.LogInformation($"Attempting login for user: {username}");
            if (_users.TryGetValue(username, out var userData) && userData.Password == password)
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_masterConfig.JwtSecretKey);
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[] 
                    { 
                        new Claim(ClaimTypes.NameIdentifier, username),
                        new Claim(ClaimTypes.Role, userData.UserDetails.Role.ToString()) 
                    }),
                    Expires = DateTime.UtcNow.AddMinutes(_masterConfig.JwtExpirationMinutes),
                    Issuer = _masterConfig.JwtIssuer,
                    Audience = _masterConfig.JwtAudience,
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };
                var token = tokenHandler.CreateToken(tokenDescriptor);
                var accessToken = tokenHandler.WriteToken(token);

                var response = new AuthResponse
                {
                    UserInfo = userData.UserDetails,
                    AccessToken = accessToken,
                    RefreshToken = $"dummy_refresh_token_for_{username}_{Guid.NewGuid()}",
                    //ExpiresIn = 3600 // 1 hour
                };
                _logger.LogInformation($"Login successful for user: {username}");
                return Task.FromResult<AuthResponse?>(response);
            }
            _logger.LogWarning($"Login failed for user: {username}");
            return Task.FromResult<AuthResponse?>(null);
        }

        /// <summary>
        /// Placeholder implementation for refreshing an access token.
        /// Simulates token refresh by checking for a specific prefix in the provided refresh token and issuing a new dummy access token.
        /// </summary>
        /// <param name="refreshToken">The dummy refresh token to validate.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a <see cref="NewAccessTokenResponse"/>
        /// with a new dummy access token if the refresh token is considered valid by the placeholder logic; otherwise, null.
        /// </returns>
        public Task<NewAccessTokenResponse?> RefreshTokenAsync(string refreshToken)
        {
            _logger.LogInformation($"Attempting to refresh token: {refreshToken}");
            if (!string.IsNullOrWhiteSpace(refreshToken) && refreshToken.StartsWith("dummy_refresh_token_for_"))
            {
                var response = new NewAccessTokenResponse
                {
                    AccessToken = $"new_dummy_access_token_{Guid.NewGuid()}",
                    ExpiresIn = 3600 // 1 hour
                };
                _logger.LogInformation("Token refresh successful.");
                return Task.FromResult<NewAccessTokenResponse?>(response);
            }
            _logger.LogWarning("Token refresh failed.");
            return Task.FromResult<NewAccessTokenResponse?>(null);
        }

        /// <summary>
        /// Placeholder implementation for user logout.
        /// Logs the logout attempt and returns a success message without performing actual token invalidation.
        /// </summary>
        /// <param name="usernameOrToken">The username or token for which logout is requested (ignored by this placeholder).</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a <see cref="LogoutResponse"/>
        /// with a generic success message.
        /// </returns>
        public Task<LogoutResponse> LogoutAsync(string usernameOrToken)
        {
            _logger.LogInformation($"User or token '{usernameOrToken}' logged out (placeholder action).");
            return Task.FromResult(new LogoutResponse { Message = "Logout successful." });
        }
    }
} 