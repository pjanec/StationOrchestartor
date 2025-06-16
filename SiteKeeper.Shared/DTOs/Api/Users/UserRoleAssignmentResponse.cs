using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Users
{
    /// <summary>
    /// Represents the response after assigning a role to a user.
    /// Based on the UserRoleAssignmentResponse schema in `web api swagger.yaml`.
    /// </summary>
    public class UserRoleAssignmentResponse
    {
        /// <summary>
        /// The username of the user to whom the role was assigned.
        /// </summary>
        /// <example>"target_user"</example>
        [Required]
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// The role that was assigned to the user.
        /// </summary>
        /// <example>"Operator"</example>
        [Required]
        [JsonPropertyName("roleAssigned")]
        public string RoleAssigned { get; set; } = string.Empty;

        /// <summary>
        /// A confirmation message.
        /// </summary>
        /// <example>"Role 'Operator' assigned successfully to user 'target_user'."</example>
        [Required]
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
} 