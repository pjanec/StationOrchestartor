using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Users
{
    /// <summary>
    /// Represents the request to create a new user.
    /// Based on the UserCreationRequest schema in `web api swagger.yaml`.
    /// </summary>
    public class UserCreationRequest
    {
        /// <summary>
        /// The username for the new user. Must be unique.
        /// </summary>
        /// <example>"new_user"</example>
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// The password for the new user.
        /// </summary>
        /// <example>"Str0ngP@sswOrd!"</example>
        [Required]
        [StringLength(100, MinimumLength = 8)] // Consider password complexity rules
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// The role to assign to the new user.
        /// Must be one of the defined SiteKeeperRoles (e.g., Observer, Operator, BasicAdmin, AdvancedAdmin).
        /// </summary>
        /// <example>"Operator"</example>
        [Required]
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// Optional display name for the user.
        /// </summary>
        /// <example>"New User Display Name"</example>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Optional email address for the user.
        /// </summary>
        /// <example>"new_user@example.com"</example>
        [EmailAddress]
        public string? Email { get; set; }
    }
} 