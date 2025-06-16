namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Indicates the outcome of an audited action.
    /// </summary>
    public enum AuditLogOutcome
    {
        /// <summary>
        /// The action was successful.
        /// </summary>
        Success,

        /// <summary>
        /// The action failed.
        /// </summary>
        Failure,

        /// <summary>
        /// The action was partially successful. Some aspects succeeded while others failed.
        /// </summary>
        PartialSuccess,

        /// <summary>
        /// The outcome of the action is unknown or could not be determined.
        /// </summary>
        Unknown
    }
} 