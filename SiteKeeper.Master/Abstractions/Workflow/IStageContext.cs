using SiteKeeper.Master.Model.InternalData;  
using System;  
using System.Collections.Generic;  
using System.Threading.Tasks;  
using SiteKeeper.Shared.Enums;

namespace SiteKeeper.Master.Abstractions.Workflow  
{  
    /// <summary>  
    /// Defines the contract for a Stage Context, which manages the lifecycle,  
    /// progress, and logging for a single stage within a Master Action.  
    /// </summary>  
    public interface IStageContext : IAsyncDisposable  
    {  
        /// <summary>  
        /// Creates a new NodeAction targeting given (or all connected) slave agents, registers its ID for translation,  
        /// and executes it as a sub-step within this stage.  
        /// </summary>  
        /// <param name="actionName">A unique, descriptive name for this specific action, used for correlating results.</param>
        /// <param name="slaveTaskType">The type of task the slave agents will execute.</param>
        /// <param name="auditContext">Optional context for journaling.</param>
        /// <param name="nodeSpecificPayloads">Optional node-specific parameters for the slave task.</param>
        /// <param name="targetNodeNames">An optional list of specific node names to run this action on. If null or empty, the action runs on all connected agents.</param>
        Task<NodeActionResult> CreateAndExecuteNodeActionAsync(  
            string actionName,  
            SlaveTaskType slaveTaskType,  
            Dictionary<string, object>? auditContext = null,  
            Dictionary<string, Dictionary<string, object>>? nodeSpecificPayloads = null,
            List<string>? targetNodeNames = null
            );

        /// <summary>
        /// Creates and executes multiple NodeActions in parallel within this stage.
        /// It aggregates their progress and returns a collection of their final results.
        /// </summary>
        /// <param name="actionInputs">A collection of definitions for the node actions to run in parallel.</param>
        Task<List<NodeActionResult>> CreateAndExecuteNodeActionsInParallelAsync(
            IEnumerable<NodeActionInput> actionInputs);

        /// <summary>  
        /// Manually reports the progress of a custom (non-NodeAction) process within this stage.  
        /// </summary>  
        void ReportProgress(int subStepProgressPercent, string statusMessage);

        /// <summary>Logs an informational message specific to this stage.</summary>  
        void LogInfo(string message);

        /// <summary>Logs a warning message specific to this stage.</summary>  
        void LogWarning(string message);

        /// <summary>Logs an error message specific to this stage.</summary>  
        void LogError(Exception? ex, string message);

		/// <summary>Sets the final custom result object for this stage, to be saved to journal upon disposal.
		/// Note: node action results are saved separately and automatically and do not affect this.
		/// This is for stages not using NodeActions, or for custom results that are not tied to any NodeAction.
		/// </summary>
		void SetCustomResult(object? result);

    }  

    /// <summary>
    /// A helper record to define the input for a single parallel NodeAction.
    /// </summary>
    /// <param name="ActionName">A unique, descriptive name for this specific action, used for correlating results.</param>
    /// <param name="SlaveTaskType">The type of task the slave agents will execute.</param>
    /// <param name="TargetNodeNames">An optional list of specific node names to run this action on. If null or empty, the action runs on all connected agents.</param>
    /// <param name="AuditContext">Optional context for journaling.</param>
    /// <param name="NodeSpecificPayloads">Optional node-specific parameters for the slave task.</param>
    public record NodeActionInput(
        string ActionName,
        SlaveTaskType SlaveTaskType,
        List<string>? TargetNodeNames = null,
        Dictionary<string, object>? AuditContext = null,
        Dictionary<string, Dictionary<string, object>>? NodeSpecificPayloads = null
    );
} 