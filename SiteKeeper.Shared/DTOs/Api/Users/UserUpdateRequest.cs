using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Users
{
    /// <summary>
    /// Represents the request to update an existing user's information.
    /// Based on the UserUpdateRequest schema in `web api swagger.yaml`.
    /// </summary>
    public class UserUpdateRequest
    {
        /// <summary>
        /// Optional new display name for the user.
        /// If null, the display name will not be changed.
        /// </summary>
        /// <example>"Updated User Display Name"</example>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Optional new email address for the user.
        /// If null, the email address will not be changed.
        /// </summary>
        /// <example>"updated_user@example.com"</example>
        [EmailAddress]
        public string? Email { get; set; }

        /// <summary>
        /// Optional new role for the user.
        /// If null, the role will not be changed.
        /// Must be one of the defined SiteKeeperRoles (e.g., Observer, Operator, BasicAdmin, AdvancedAdmin).
        /// </summary>
        /// <example>"BasicAdmin"</example>
        public string? Role { get; set; }
        // Password changes should be handled by a separate, dedicated endpoint for security reasons.
    }
} 