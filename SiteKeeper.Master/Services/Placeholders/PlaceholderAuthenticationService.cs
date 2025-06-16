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
    public class PlaceholderAuthenticationService : IAuthenticationService
    {
        private readonly ILogger<PlaceholderAuthenticationService> _logger;
        private readonly MasterConfig _masterConfig; // Inject config
        internal readonly Dictionary<string, (string Password, UserInfo UserDetails)> _users = new();

        public PlaceholderAuthenticationService(ILogger<PlaceholderAuthenticationService> logger, MasterConfig masterConfig)
        {
            _logger = logger;
            _masterConfig = masterConfig; // Store config
            _users.Add("observer", (Password:"password", new UserInfo { Username = "observer", Role = UserRole.Observer }));
            _users.Add("operator", (Password:"password", new UserInfo { Username = "operator", Role = UserRole.Operator }));
            _users.Add("basicadmin", (Password:"password", new UserInfo { Username = "basicadmin", Role = UserRole.BasicAdmin }));
            _users.Add("advancedadmin", (Password:"password", new UserInfo { Username = "advancedadmin", Role = UserRole.AdvancedAdmin }));
        }

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

        public Task<LogoutResponse> LogoutAsync(string usernameOrToken)
        {
            _logger.LogInformation($"User or token '{usernameOrToken}' logged out (placeholder action).");
            return Task.FromResult(new LogoutResponse { Message = "Logout successful." });
        }
    }
} 