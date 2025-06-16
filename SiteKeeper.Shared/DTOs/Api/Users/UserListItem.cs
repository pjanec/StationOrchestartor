using SiteKeeper.Shared.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Users
{
    /// <summary>
    /// Represents a user item in a list, providing summary information (username and role).
    /// As used by GET /api/users response.
    /// </summary>
    public class UserListItem
    {
        /// <summary>
        /// The username of the user.
        /// </summary>
        /// <example>"johndoe"</example>
        [Required]
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// The role assigned to the user.
        /// </summary>
        /// <example>UserRole.Operator</example>
        [Required]
        [JsonPropertyName("role")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserRole Role { get; set; }
    }
} 