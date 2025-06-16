using SiteKeeper.Shared.Enums;
using System.Security.Claims;

namespace SiteKeeper.Shared.Security
{
    public static class ClaimsPrincipalExtensions
    {
        public static string GetUsername(this ClaimsPrincipal user)
        {
            var usernameClaim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst(ClaimTypes.Name);
            return usernameClaim?.Value ?? string.Empty;
        }

        public static bool IsObserverOrHigher(this ClaimsPrincipal user)
        {
            // Adjusted roles based on typical hierarchy. Swagger defines: Observer, Operator, BasicAdmin, AdvancedAdmin
            return user.IsInRole(SiteKeeperRoles.Observer) ||
                   user.IsInRole(SiteKeeperRoles.Operator) ||
                   user.IsInRole(SiteKeeperRoles.BasicAdmin) ||
                   user.IsInRole(SiteKeeperRoles.AdvancedAdmin);
        }

        public static bool IsOperatorOrHigher(this ClaimsPrincipal user)
        {
            return user.IsInRole(SiteKeeperRoles.Operator) ||
                   user.IsInRole(SiteKeeperRoles.BasicAdmin) ||
                   user.IsInRole(SiteKeeperRoles.AdvancedAdmin);
        }

        public static bool IsBasicAdminOrHigher(this ClaimsPrincipal user)
        {
            return user.IsInRole(SiteKeeperRoles.BasicAdmin) ||
                   user.IsInRole(SiteKeeperRoles.AdvancedAdmin);
        }

        // Added for completeness, though direct check user.IsInRole(SiteKeeperRoles.AdvancedAdmin) is clearer
        public static bool IsAdvancedAdmin(this ClaimsPrincipal user)
        {
            return user.IsInRole(SiteKeeperRoles.AdvancedAdmin);
        }
    }
} 