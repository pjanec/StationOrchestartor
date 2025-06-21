using SiteKeeper.Master.Abstractions.Services.Journaling;
using SiteKeeper.Master.Abstractions.Workflow;
using SiteKeeper.Master.Model.InternalData;
using SiteKeeper.Master.Web.Apis.QueryParameters;
using SiteKeeper.Shared.DTOs.AgentHub;
using SiteKeeper.Shared.DTOs.API.Environment;
using SiteKeeper.Shared.DTOs.API.Journal;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Defines the contract for the dual journaling system, which manages both a detailed Action Journal
    /// for debugging and a high-level Change Journal for auditing and system state management.
    /// </summary>
    public interface IJournal
    {
        #region Change Journal & Backup Repository Methods

        /// <summary>
        /// Creates an 'Initiated' record for a new state change in the Change Journal.
        /// If the change type is 'Backup', it also creates a dedicated folder in the Backup Repository as a side effect.
        /// The implementation of this service must temporarily cache the link between the returned ChangeId
        /// and the provided SourceMasterActionId to be used by FinalizeStateChangeAsync.
        /// </summary>
        /// <param name="changeInfo">DTO containing initial information about the state change.</param>
        /// <returns>A result object containing the unique ID for this change event and, if applicable, the full path to the directory where backup artifacts should be stored.</returns>
        Task<StateChangeCreationResult> InitiateStateChangeAsync(StateChangeInfo changeInfo);

        /// <summary>
        /// Finalizes a state change event by writing a 'Completed' record to the Change Journal
        /// and saving the final result artifact (e.g., a manifest) to the Change Journal's private artifact store.
        /// </summary>
        /// <param name="finalizationInfo">DTO containing the final outcome and result data.</param>
        Task FinalizeStateChangeAsync(StateChangeFinalizationInfo finalizationInfo);

        #endregion

        #region Action Journal Methods (for detailed debugging)

        /// <summary>
        /// Records the initiation of a new Master Action in the Action Journal. This creates the main
        /// directory for the workflow and its entry in the action_journal_index.log.
        /// </summary>
        Task RecordMasterActionInitiatedAsync(MasterAction action);

        /// <summary>
        /// Records the final completion state of a Master Action in the Action Journal by updating its main info file.
        /// </summary>
        Task RecordMasterActionCompletedAsync(MasterAction action);

        /// <summary>
        /// Records the start of a new stage within a Master Action's journal entry. This creates the
        /// stage-specific sub-directory and its initial metadata file.
        /// </summary>
        Task RecordStageInitiatedAsync(string masterActionId, int stageIndex, string stageName, object? stageInput);

        /// <summary>
        /// Records the completion of a stage within the Action Journal by updating its metadata file and saving its result.
        /// </summary>
        Task RecordStageCompletedAsync(string masterActionId, int stageIndex, string stageName, object? stageResult);

        /// <summary>
        /// Appends a log entry (from either the master or a slave) to the correct log file
        /// within the currently active stage's journal sub-directory.
        /// </summary>
        /// <param name="masterActionId">The ID of the master action to which the log belongs.</param>
        /// <param name="logEntry">The log entry DTO containing the message and source node.</param>
        Task AppendSlaveLogToStageAsync(string masterActionId, SlaveTaskLogEntry logEntry);

        Task AppendMasterLogToStageAsync(string masterActionId, int? stageIndex, string? stageName, SlaveTaskLogEntry logEntry);

        /// <summary>
        /// Records the final result payload from an individual node task in its stage's 'results' sub-directory.
        /// </summary>
        Task RecordNodeTaskResultAsync(NodeTask task);

        /// <summary>
        /// Maps a NodeAction ID to a specific stage directory within a MasterAction's journal.
        /// This is the key to routing late-arriving logs correctly.
        /// </summary>
        /// <param name="masterActionId">The ID of the parent MasterAction.</param>
        /// <param name="stageIndex">The index of the stage this NodeAction belongs to.</param>
        /// <param name="stageName">The name of the stage, used for creating the directory path.</param>
        /// <param name="nodeActionId">The ID of the NodeAction being mapped.</param>
        Task MapNodeActionToStageAsync(string masterActionId, int stageIndex, string stageName, string nodeActionId);

        /// <summary>
        /// Clears all internal mappings associated with a completed MasterAction to prevent memory leaks.
        /// </summary>
        /// <param name="masterActionId">The ID of the MasterAction whose mappings should be cleared.</param>
        void ClearJournalMappings(string masterActionId);

        #endregion

        #region Query Methods

        /// <summary>
        /// Lists a high-level, chronological history of events by efficiently reading from the Change Journal's index file.
        /// </summary>
        Task<(IEnumerable<JournalEntrySummary> Items, int TotalCount)> ListJournalEntriesAsync(JournalQueryParameters queryParams);

        /// <summary>
        /// Gets the full debug details for a Master Action by finding its folder in the Action Journal's index
        /// and then recursively reading all of its stages, logs, and results.
        /// </summary>
        Task<JournalEntry?> GetJournalEntryDetailsAsync(string masterActionId);

        /// <summary>
        /// Retrieves a historical/archived MasterAction object from the journal by its ID.
        /// This is essential for querying the status of completed operations.
        /// </summary>
        /// <param name="masterActionId">The ID of the MasterAction to retrieve.</param>
        /// <returns>The deserialized <see cref="MasterAction"/> object if found; otherwise, null.</returns>
        Task<MasterAction?> GetArchivedMasterActionAsync(string masterActionId);

         /// <summary>
        /// Finds the last successful change record of a specific type by querying the Change Journal.
        /// This is used to answer questions like "what was the manifest from the last successful update?".
        /// </summary>
        Task<SystemChangeRecord?> GetLastSuccessfulChangeOfTypeAsync(ChangeEventType type);

        /// <summary>
        /// Retrieves the detailed result object for a specific completed stage from the journal.
        /// </summary>
        /// <typeparam name="T">The expected type of the result object.</typeparam>
        /// <param name="masterActionId">The ID of the parent MasterAction.</param>
        /// <param name="stageIndex">The 1-based index of the stage.</param>
        /// <returns>The deserialized stage result object, or null if not found.</returns>
        Task<T?> GetArchivedStageResultAsync<T>(string masterActionId, int stageIndex) where T : class;

        /// <summary>
        /// Retrieves the raw text content of a specific log file from a specific stage in the journal.
        /// </summary>
        /// <param name="masterActionId">The ID of the parent MasterAction.</param>
        /// <param name="stageIndex">The 1-based index of the stage.</param>
        /// <param name="logFileName">The name of the log file (e.g., "_master.log" or "InternalTestSlave.log").</param>
        /// <returns>The content of the log file as a string, or null if not found.</returns>
        Task<string?> GetArchivedStageLogContentAsync(string masterActionId, int stageIndex, string logFileName);

        #endregion
    }
}
