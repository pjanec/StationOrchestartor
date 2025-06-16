using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Represents the aggregated operational status of an application plan.
    /// An application plan typically groups multiple applications that function together.
    /// </summary>
    /// <remarks>
    /// This enum is used in DTOs like <c>PlanInfo</c> to provide a summary
    /// of the collective state of all applications within a defined plan.
    /// Serialization to string is handled by <see cref="JsonStringEnumConverter"/>.
    /// The <c>Idle</c> member was removed to strictly align with the Swagger definition for PlanInfo.status.
    /// Systems previously using <c>Idle</c> should be updated to use <c>NotRunning</c> or another appropriate status.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PlanOperationalStatus
    {
        /// <summary>
        /// None of the applications in the plan are currently running.
        /// This is the new default state if a plan is not active.
        /// </summary>
        NotRunning,

        /// <summary>
        /// The plan is in the process of starting, meaning some or all of its applications are currently starting.
        /// </summary>
        Starting,

        /// <summary>
        /// All essential applications within the plan are running correctly.
        /// </summary>
        Running,

        /// <summary>
        /// The plan is in the process of stopping, meaning some or all of its applications are currently stopping.
        /// </summary>
        Stopping,

        /// <summary>
        /// The plan is in a failing state, likely because one or more critical applications in the plan have failed or are in an error state.
        /// </summary>
        Failing,

        /// <summary>
        /// Some, but not all, applications within the plan are running. Others might be stopped or in an error state.
        /// </summary>
        PartiallyRunning,

        /// <summary>
        /// The aggregated operational status of the plan cannot be determined.
        /// </summary>
        Unknown
    }
} 