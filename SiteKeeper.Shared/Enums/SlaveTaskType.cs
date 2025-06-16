using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Defines the types of tasks that a Slave Agent can execute.
    /// </summary>
    /// <remarks>
    /// This enum is used by the Master Agent when creating <see cref="SiteKeeper.Master.Model.InternalData.NodeTask"/> instances
    /// and by Slave Agents to understand the specific action to perform for a received task.
    /// It corresponds to the types of executable units or scripts within the Slave Agent.
    /// Serialization to string is handled by <see cref="JsonStringEnumConverter"/>.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SlaveTaskType
    {
        /// <summary>
        /// An unknown or unspecified task type.
        /// </summary>
        Unknown,

        /// <summary>
        /// Task to restart the node (the machine itself).
        /// </summary>
        RestartNode,

        /// <summary>
        /// Task to shut down the node (the machine itself).
        /// </summary>
        ShutdownNode,

        /// <summary>
        /// Task to run diagnostic procedures on the node.
        /// </summary>
        RunDiagnostics,

        /// <summary>
        /// Task related to package management (install, update, uninstall, revert, refresh).
        /// The specific action is determined by the task payload.
        /// </summary>
        ManagePackage,

        /// <summary>
        /// Task related to software application control (start, stop, restart).
        /// The specific action and target application are determined by the task payload.
        /// </summary>
        ManageSoftware,

        /// <summary>
        /// Task to apply a configuration or manifest to the node.
        /// This is typically used for environment updates (online/offline).
        /// </summary>
        ApplyConfiguration,

        /// <summary>
        /// Task to verify the current configuration of the node against a baseline or manifest.
        /// </summary>
        VerifyConfiguration,

        /// <summary>
        /// Task to execute a specific verification script or routine (more granular than full VerifyConfiguration).
        /// </summary>
        ExecuteVerification,

        /// <summary>
        /// Task to adjust the system time on the slave based on master's instruction.
        /// </summary>
        AdjustSystemTime,

        /// <summary>
        /// Task to perform a step in a backup process (e.g., backing up specific files or application data).
        /// </summary>
        ExecuteBackupStep,

        /// <summary>
        /// A task used exclusively for end-to-end orchestration testing.
        /// The payload will contain instructions on how to behave (succeed, fail, timeout, etc.).
        /// </summary>
        TestOrchestration

        // Add other specific task types as needed, e.g.:
        // ExecuteScript, FileOperation, HealthCheck, etc.
    }
} 