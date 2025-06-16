using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Represents the role of an authenticated user.
    /// </summary>
    /// <remarks>
    /// This enum defines the different levels of access and permissions a user can have within the SiteKeeper system.
    /// It is used for authorization purposes across the API and potentially in the UI to tailor the user experience.
    /// The roles are typically assigned during user authentication.
    /// This enum is designed to be serialized as a string in JSON communications, as recommended in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UserRole
    {
        /// <summary>
        /// Users with Observer role can view system status, logs, and information but cannot perform any actions.
        /// </summary>
        Observer,

        /// <summary>
        /// Users with Operator role can perform routine operations such as starting/stopping applications,
        /// running predefined diagnostic tasks, and acknowledging alerts. They typically cannot change configurations
        /// or manage users.
        /// </summary>
        Operator,

        /// <summary>
        /// Users with BasicAdmin role can perform most administrative tasks, including some configuration changes,
        /// but may be restricted from critical system-wide operations or user management.
        /// </summary>
        BasicAdmin,

        /// <summary>
        /// Users with AdvancedAdmin role have full control over the SiteKeeper system, including all configurations,
        /// user management, and critical operations. This is the highest level of privilege.
        /// </summary>
        AdvancedAdmin
    }
} 