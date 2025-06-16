namespace SiteKeeper.Master.Workflow.DTOs
{
    /// <summary>
    /// Reports the progress of a single workflow stage.
    /// </summary>
    public class StageProgress
    {
        /// <summary>
        /// The completion percentage of the current stage (0-100).
        /// </summary>
        public int ProgressPercent { get; set; }

        /// <summary>
        /// A descriptive message about the current status of the stage.
        /// </summary>
        public string StatusMessage { get; set; } = string.Empty;
    }
} 