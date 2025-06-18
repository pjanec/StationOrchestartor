using SiteKeeper.Shared.Enums;
using System.Security.Claims;

namespace SiteKeeper.Shared.Security
{
    /// <summary>
    /// Provides extension methods for <see cref="ClaimsPrincipal"/> to facilitate working with
    /// SiteKeeper-specific user information, such as retrieving usernames and checking roles.
    /// </summary>
    /// <remarks>
    /// These methods are typically used in API controllers or services for authorization
    /// and to obtain authenticated user context. They rely on role strings defined in <see cref="SiteKeeperRoles"/>.
    /// </remarks>
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>
        /// Retrieves the username from the <see cref="ClaimsPrincipal"/>.
        /// It checks standard claim types <see cref="ClaimTypes.NameIdentifier"/> and then <see cref="ClaimTypes.Name"/>.
        /// </summary>
        /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the user.</param>
        /// <returns>The username string if found; otherwise, an empty string.</returns>
        public static string GetUsername(this ClaimsPrincipal user)
        {
            var usernameClaim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst(ClaimTypes.Name);
            return usernameClaim?.Value ?? string.Empty;
        }

        /// <summary>
        /// Checks if the user has at least Observer privileges (Observer, Operator, BasicAdmin, or AdvancedAdmin).
        /// </summary>
        /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the user.</param>
        /// <returns><c>true</c> if the user fulfills Observer or higher role criteria; otherwise, <c>false</c>.</returns>
        public static bool IsObserverOrHigher(this ClaimsPrincipal user)
        {
            // Adjusted roles based on typical hierarchy. SiteKeeperRoles defines: Observer, Operator, BasicAdmin, AdvancedAdmin
            return user.IsInRole(SiteKeeperRoles.Observer) ||
                   user.IsInRole(SiteKeeperRoles.Operator) ||
                   user.IsInRole(SiteKeeperRoles.BasicAdmin) ||
                   user.IsInRole(SiteKeeperRoles.AdvancedAdmin);
        }

        /// <summary>
        /// Checks if the user has at least Operator privileges (Operator, BasicAdmin, or AdvancedAdmin).
        /// </summary>
        /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the user.</param>
        /// <returns><c>true</c> if the user fulfills Operator or higher role criteria; otherwise, <c>false</c>.</returns>
        public static bool IsOperatorOrHigher(this ClaimsPrincipal user)
        {
            return user.IsInRole(SiteKeeperRoles.Operator) ||
                   user.IsInRole(SiteKeeperRoles.BasicAdmin) ||
                   user.IsInRole(SiteKeeperRoles.AdvancedAdmin);
        }

        /// <summary>
        /// Checks if the user has at least BasicAdmin privileges (BasicAdmin or AdvancedAdmin).
        /// </summary>
        /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the user.</param>
        /// <returns><c>true</c> if the user fulfills BasicAdmin or AdvancedAdmin role criteria; otherwise, <c>false</c>.</returns>
        public static bool IsBasicAdminOrHigher(this ClaimsPrincipal user)
        {
            return user.IsInRole(SiteKeeperRoles.BasicAdmin) ||
                   user.IsInRole(SiteKeeperRoles.AdvancedAdmin);
        }

        /// <summary>
        /// Checks if the user has AdvancedAdmin privileges.
        /// </summary>
        /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the user.</param>
        /// <returns><c>true</c> if the user is an AdvancedAdmin; otherwise, <c>false</c>.</returns>
        // Added for completeness, though direct check user.IsInRole(SiteKeeperRoles.AdvancedAdmin) is often clear.
        public static bool IsAdvancedAdmin(this ClaimsPrincipal user)
        {
            return user.IsInRole(SiteKeeperRoles.AdvancedAdmin);
        }
    }
} 