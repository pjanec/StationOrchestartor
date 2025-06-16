using SiteKeeper.Shared.DTOs.API.Authentication;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Defines the contract for an authentication service.
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// Logs a user in.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns>An AuthResponse if successful, otherwise null.</returns>
        Task<AuthResponse?> LoginAsync(string username, string password);

        /// <summary>
        /// Refreshes an access token.
        /// </summary>
        /// <param name="refreshToken">The refresh token.</param>
        /// <returns>A NewAccessTokenResponse if successful, otherwise null.</returns>
        Task<NewAccessTokenResponse?> RefreshTokenAsync(string refreshToken);

        /// <summary>
        /// Logs a user out.
        /// </summary>
        /// <param name="usernameOrToken">The username or token to invalidate.</param>
        /// <returns>A response object indicating the outcome of the logout operation.</returns>
        Task<LogoutResponse> LogoutAsync(string usernameOrToken);
    }
} 