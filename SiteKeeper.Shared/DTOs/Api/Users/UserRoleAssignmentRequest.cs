using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Users
{
    /// <summary>
    /// Represents the request to assign a role to a user.
    /// Based on the UserRoleAssignmentRequest schema in `web api swagger.yaml`.
    /// </summary>
    public class UserRoleAssignmentRequest
    {
        /// <summary>
        /// The role to assign to the user.
        /// Must be one of the defined SiteKeeperRoles (e.g., Observer, Operator, BasicAdmin, AdvancedAdmin).
        /// </summary>
        /// <example>"Operator"</example>
        [Required]
        public string Role { get; set; } = string.Empty;
    }
} 