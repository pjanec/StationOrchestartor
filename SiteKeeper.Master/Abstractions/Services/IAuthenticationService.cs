using SiteKeeper.Shared.DTOs.API.Authentication;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Defines the contract for a service responsible for user authentication, including credential validation,
    /// token issuance, token refresh, and session invalidation (logout).
    /// </summary>
    /// <remarks>
    /// This service is a core component of the security infrastructure. It is typically consumed by API controllers
    /// that expose authentication endpoints (e.g., /auth/login, /auth/refresh, /auth/logout).
    /// Implementations are expected to handle interactions with user data stores and token generation/validation logic.
    /// </remarks>
    public interface IAuthenticationService
    {
        /// <summary>
        /// Authenticates a user by validating their username and password.
        /// If successful, generates and returns authentication tokens (access and refresh) along with user information.
        /// Typically called by the API endpoint handling POST /auth/login requests.
        /// </summary>
        /// <param name="username">The user's username.</param>
        /// <param name="password">The user's password.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains an <see cref="AuthResponse"/>
        /// with tokens and user details if authentication is successful; otherwise, null or an exception might be raised
        /// depending on implementation for failed login attempts.
        /// </returns>
        Task<AuthResponse?> LoginAsync(string username, string password);

        /// <summary>
        /// Refreshes an access token using a valid, unexpired refresh token.
        /// This allows clients to maintain an active session without requiring the user to re-enter credentials.
        /// Typically called by the API endpoint handling POST /auth/refresh requests.
        /// </summary>
        /// <param name="refreshToken">The refresh token previously issued to the client.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a <see cref="NewAccessTokenResponse"/>
        /// with a new access token (and potentially a new refresh token if rotation is enabled) if the refresh token is valid;
        /// otherwise, null or an exception might be raised.
        /// </returns>
        Task<NewAccessTokenResponse?> RefreshTokenAsync(string refreshToken);

        /// <summary>
        /// Logs a user out by invalidating their current session, primarily by invalidating their stored refresh token(s).
        /// Typically called by the API endpoint handling POST /auth/logout requests.
        /// </summary>
        /// <param name="usernameOrToken">The username of the user logging out, or the refresh token itself,
        /// depending on the session invalidation strategy implemented by the service.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a <see cref="LogoutResponse"/>
        /// confirming the outcome of the logout attempt.
        /// </returns>
        Task<LogoutResponse> LogoutAsync(string usernameOrToken);
    }
} 