using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.Enums
{
    /// <summary>
    /// Defines the types of operations that can be initiated and coordinated by the SiteKeeper Master Agent.
    /// </summary>
    /// <remarks>
    /// This enum is crucial for the <c>OperationCoordinatorService</c> to understand the nature of a requested operation,
    /// select appropriate target nodes, map to specific <see cref="SlaveTaskType"/> values for slave execution,
    /// and manage operation lifecycles (e.g., conflict detection, journaling, UI reporting).
    /// It's used in the internal <c>Operation</c> data structure and potentially in API request parameters
    /// to specify the desired action.
    /// See "SiteKeeper - Master - Data Structures.md" and "SiteKeeper Master Slave - guidelines.md".
    /// Serialization to string is handled by <see cref="JsonStringEnumConverter"/>.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OperationType
    {
        // --- Environment Wide Operations ---

        /// <summary>
        /// An operation to verify the integrity and configuration of the environment against its manifest.
        /// </summary>
        EnvVerify,

        /// <summary>
        /// Performs a backup of the environment's critical data and configurations.
        /// The scope of the backup (e.g., application data, system settings) is defined by the operation's implementation.
        /// </summary>
        EnvBackup,

        /// <summary>
        /// An operation to restore the environment from a previously created backup.
        /// </summary>
        EnvRestore,

        /// <summary>
        /// An operation to revert the environment's configuration to a previously known "pure" state,
        /// typically based on a manifest from a past successful update, without using a full backup.
        /// </summary>
        EnvRevert,

        /// <summary>
        /// Updates the environment to a new version using an online package repository.
        /// This involves downloading and deploying new package versions to target nodes.
        /// </summary>
        EnvUpdateOnline,

        /// <summary>
        /// Updates the environment to a new version using an offline update bundle (e.g., from a USB drive or network share).
        /// </summary>
        EnvUpdateOffline,

        /// <summary>
        /// Synchronizes files or configurations across the environment or to specific nodes according to predefined rules.
        /// </summary>
        EnvSync,

        /// <summary>
        /// Runs a standard set of diagnostic checks across the environment or on specified nodes.
        /// The specific checks performed are typically predefined for the environment type.
        /// </summary>
        RunStandardDiagnostics,

        // --- Node Specific Operations (can be batched for multiple nodes) ---

        /// <summary>
        /// Restarts one or more specified nodes.
        /// </summary>
        NodeRestart,

        /// <summary>
        /// Shuts down one or more specified nodes.
        /// </summary>
        NodeShutdown,

        /// <summary>
        /// Pings one or more specified nodes to check basic network connectivity and agent responsiveness.
        /// </summary>
        NodePing,

        /// <summary>
        /// Triggers a VNC (Virtual Network Computing) session or makes a VNC connection available for a specified node.
        /// (Actual VNC mechanism is external to SiteKeeper; this is a trigger/facilitator).
        /// </summary>
        NodeVncTrigger,

        /// <summary>
        /// Triggers an RDP (Remote Desktop Protocol) session for a specified node.
        /// (Actual RDP mechanism is external; this is a trigger/facilitator).
        /// </summary>
        NodeRdpTrigger,

        /// <summary>
        /// Initiates or facilitates a file transfer to or from a specified node.
        /// </summary>
        NodeFileTransferTrigger,

        /// <summary>
        /// A generic control action for a single node, where the action type is specified in the parameters.
        /// </summary>
        NodeControl,

        /// <summary>
        /// A generic control action for multiple nodes, where the action type is specified in the parameters.
        /// </summary>
        MultiNodeControl,

        // --- Package Management Operations ---

        /// <summary>
        /// Changes the version of a specific package on one or more target nodes.
        /// This can be an upgrade, downgrade, or reinstallation to the same version.
        /// </summary>
        PackageChangeVersion,

        /// <summary>
        /// Reverts deviations for packages on specified nodes to match the versions defined in the current environment manifest.
        /// </summary>
        PackageRevertDeviations,

        /// <summary>
        /// Installs an optional package on specified nodes.
        /// </summary>
        PackageOptionalInstall,

        /// <summary>
        /// Uninstalls an optional package from specified nodes.
        /// </summary>
        PackageOptionalUninstall,

        /// <summary>
        /// Refreshes one or more packages. This might involve re-downloading from a source or re-applying configurations.
        /// </summary>
        PackageRefresh,

        // --- Software Control Operations ---

        /// <summary>
        /// Starts the entire managed software suite on all applicable nodes.
        /// </summary>
        SystemSoftwareStart,

        /// <summary>
        /// Stops the entire managed software suite on all applicable nodes.
        /// </summary>
        SystemSoftwareStop,

        /// <summary>
        /// Restarts the entire managed software suite on all applicable nodes.
        /// </summary>
        SystemSoftwareRestart,

        /// <summary>
        /// Starts a specific application on one or more target nodes where it resides.
        /// </summary>
        AppStart,

        /// <summary>
        /// Stops a specific application on one or more target nodes.
        /// </summary>
        AppStop,

        /// <summary>
        /// Restarts a specific application on one or more target nodes.
        /// </summary>
        AppRestart,

        /// <summary>
        /// Starts an entire application plan (a group of related applications) across relevant nodes.
        /// </summary>
        PlanStart,

        /// <summary>
        /// Stops an entire application plan.
        /// </summary>
        PlanStop,

        /// <summary>
        /// Restarts an entire application plan.
        /// </summary>
        PlanRestart,

        // --- Diagnostic Data Collection ---

        /// <summary>
        /// Collects logs or other diagnostic data packages for a specific application from target nodes.
        /// </summary>
        CollectAppLogs,

        // --- Internal/Wizard Operations ---

        /// <summary>
        /// Scans available offline sources (e.g., USB drives, network shares) for valid update bundles.
        /// This is typically part of an offline update wizard flow.
        /// </summary>
        OfflineScanSources,

        /// <summary>
        /// A special operation type used exclusively for end-to-end orchestration testing.
        /// It instructs the slave to simulate specific behaviors (success, failure, timeout).
        /// </summary>
        OrchestrationTest,

        /// <summary>
        /// A null operation that performs no actions. Useful for testing or as a placeholder.
        /// </summary>
        NoOp,

        /// <summary>
        /// Represents an unknown or unspecified operation type. Should ideally not be used for actual operations.
        /// </summary>
        Unknown
    }
} 