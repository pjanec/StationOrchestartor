namespace SiteKeeper.Shared.Security
{
    /// <summary>
    /// Defines standardized role names used within the SiteKeeper system.
    /// These roles are used for authorization and should align with the roles defined
    /// in the identity provider and API specifications.
    /// </summary>
    public static class SiteKeeperRoles
    {
        /// <summary>
        /// Users with this role can view system status and information but cannot make changes.
        /// </summary>
        public const string Observer = "Observer";

        /// <summary>
        /// Users with this role can perform operational tasks like starting/stopping applications or plans.
        /// </summary>
        public const string Operator = "Operator";

        /// <summary>
        /// Users with this role have basic administrative privileges, potentially including user management
        /// for lower roles and some configuration changes.
        /// </summary>
        public const string BasicAdmin = "BasicAdmin";

        /// <summary>
        /// Users with this role have full administrative control over the system,
        /// including advanced configuration, user management, and potentially sensitive operations.
        /// </summary>
        public const string AdvancedAdmin = "AdvancedAdmin";
    }
} 