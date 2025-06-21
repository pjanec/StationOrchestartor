using System;

namespace SiteKeeper.Master.Model.InternalData
{
    /// <summary>
    /// Represents a persistent, historical record of a single stage's execution
    /// within a MasterAction. This gets saved to the main journal file.
    /// </summary>
    public class StageRecord
    {
        public int StageIndex { get; set; }
        public string StageName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsSuccess { get; set; }

        /// <summary>
        /// A list of the final states of all NodeActions that were executed
        /// during this stage. This provides a complete, contextual record of the stage's outcome.
        /// This property is populated automatically by the StageContext when a node action completes.
        /// </summary>
        public List<NodeAction> FinalNodeActions { get; set; } = new List<NodeAction>();

        /// <summary>
        /// Stores a custom, non-node-action result for the stage. This is set explicitly
        /// by the workflow handler using 'stage.SetCustomResult()'. It is independent
        /// of any NodeAction results.
        /// </summary>
        public object? CustomResult { get; set; }
    }
}
