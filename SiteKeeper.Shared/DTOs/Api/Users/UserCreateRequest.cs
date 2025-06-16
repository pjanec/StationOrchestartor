using SiteKeeper.Shared.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Users
{
    /// <summary>
    /// Represents the request body for creating a new user.
    /// As defined in swagger: POST /users
    /// </summary>
    public class UserCreateRequest
    {
        /// <summary>
        /// The desired username for the new user. Must be unique.
        /// </summary>
        /// <example>"johndoe"</example>
        [Required]
        [StringLength(50, MinimumLength = 3)]
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// The password for the new user.
        /// Password complexity rules should be enforced by the server.
        /// </summary>
        /// <example>"P@$$wOrd123!"</example>
        [Required]
        [StringLength(100, MinimumLength = 8)]
        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// The role assigned to the new user.
        /// </summary>
        /// <example>UserRole.Operator</example>
        [Required]
        [JsonPropertyName("role")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserRole Role { get; set; }
    }
} 