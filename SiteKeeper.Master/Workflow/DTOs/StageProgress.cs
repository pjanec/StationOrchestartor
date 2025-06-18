using SiteKeeper.Master.Abstractions.Workflow; // For MasterActionContext
using System; // For IProgress

namespace SiteKeeper.Master.Workflow.DTOs
{
    /// <summary>
    /// Represents a progress update for an individual stage within a <see cref="MasterAction"/>.
    /// </summary>
    /// <remarks>
    /// Instances of this class are typically created by <see cref="IStageHandler{TInput, TOutput}"/> implementations
    /// and reported to the <see cref="MasterActionContext"/> via its <see cref="MasterActionContext.StageProgress"/>
    /// property (which is an <see cref="IProgress{T}"/> of <see cref="StageProgress"/>).
    /// This allows the <see cref="MasterActionContext"/> to calculate and update the overall progress
    /// of the parent <see cref="SiteKeeper.Master.Model.InternalData.MasterAction"/>.
    /// </remarks>
    public class StageProgress
    {
        /// <summary>
        /// Gets or sets the completion percentage of the current stage (0-100).
        /// </summary>
        /// <example>50</example>
        public int ProgressPercent { get; set; }

        /// <summary>
        /// Gets or sets a descriptive message about the current status or activity within the stage.
        /// This message may be logged or displayed in user interfaces monitoring operation progress.
        /// </summary>
        /// <example>"Deploying package 'CoreApp-bin' to node 'AppServer01'..."</example>
        public string StatusMessage { get; set; } = string.Empty;
    }
} 