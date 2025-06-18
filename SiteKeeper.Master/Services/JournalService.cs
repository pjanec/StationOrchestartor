// In SiteKeeper.Master\Services\JournalService.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Abstractions.Services.Journaling;
using SiteKeeper.Master.Abstractions.Workflow;
using SiteKeeper.Master.Model;
using SiteKeeper.Master.Model.InternalData;
using SiteKeeper.Master.Web.Apis.QueryParameters;
using SiteKeeper.Shared.DTOs.AgentHub;
using SiteKeeper.Shared.DTOs.API.Environment;
using SiteKeeper.Shared.DTOs.API.Journal;
using SiteKeeper.Shared.DTOs.MasterSlave;
using SiteKeeper.Shared.Enums;
using SiteKeeper.Shared.Enums.Extensions; 
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Services
{
    /// <summary>
    /// Implements the <see cref="IJournalService"/> using a file-system-based approach. This service is the persistent memory
    /// of the SiteKeeper Master Agent, responsible for creating a detailed, durable record of every master action and significant system state change.
    /// </summary>
    /// <remarks>
    /// This service manages two main types of journals:
    /// 1.  **Action Journal**: A detailed, per-MasterAction log stored in a structured directory format (e.g., `EnvironmentName/ActionJournal/Timestamp-ActionID/`).
    ///     Each Master Action gets its own directory containing its initial parameters, stage-by-stage inputs/outputs, logs from master and slaves, and final results.
    ///     This journal is primarily for debugging, post-mortem analysis, and detailed operational history.
    /// 2.  **Change Journal**: A high-level, chronological log of significant state changes in the system (e.g., "EnvUpdateOnline Succeeded", "Backup Initiated", "Agent Connected").
    ///     This is typically an append-only log file (`system_changes_index.log`) containing <see cref="SystemChangeRecord"/> entries, with associated artifacts stored separately.
    ///     This journal is used for auditing, high-level status reporting, and identifying points for environment rollback or restore.
    /// The service uses file system operations, JSON serialization for structured data, and file locks to manage concurrent access.
    /// It is typically configured via <see cref="MasterConfig"/> for journal paths.
    /// </remarks>
    public class JournalService : IJournalService
    {
        /// <summary>
        /// Represents the active state of a journal being written for a specific Master Action.
        /// Tracks the current paths and stage information for ongoing journaling.
        /// </summary>
        private class ActiveJournalState
        {
            public string MasterActionId { get; set; } = string.Empty;
            public string MasterActionJournalPath { get; set; } = string.Empty;
            public int CurrentStageIndex { get; set; } = 0;
            public string CurrentStageName { get; set; } = "_init"; // Default initial stage name
            public string CurrentStagePath => Path.Combine(MasterActionJournalPath, "stages", $"{CurrentStageIndex:D3}-{SanitizeFileName(CurrentStageName)}");
        }
        
        // _operationJournalFolders seems to be a legacy or alternative naming for _activeJournals or their paths.
        // Based on its usage in older methods, it might be related to a previous journaling structure.
        // For the IJournalService implementation focusing on MasterAction journaling, _activeJournals is primary.
        private readonly ConcurrentDictionary<string, string> _operationJournalFolders = new();

        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();
        
        // _journalEnvRootPath is a legacy field name. The functionality is covered by specific root paths below.
        private readonly string _journalEnvRootPath;

        private readonly ILogger<JournalService> _logger;
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() }};

        private readonly ConcurrentDictionary<string, ActiveJournalState> _activeJournals = new();
        private readonly ConcurrentDictionary<string, string> _pendingChanges = new(); // Key: ChangeId, Value: SourceMasterActionId

        private readonly string _actionJournalRootPath;
        private readonly string _changeJournalRootPath;
        private readonly string _backupRepositoryRootPath;
        private readonly string _actionJournalIndexPath; // Index for all MasterActions
        private readonly string _changeJournalIndexPath; // Index for all SystemChangeRecords

        /// <summary>
        /// Initializes a new instance of the <see cref="JournalService"/> class.
        /// Sets up required directory paths for action journals, change journals, and backup repositories
        /// based on the provided master configuration.
        /// </summary>
        /// <param name="logger">The logger instance for logging service activities and errors.</param>
        /// <param name="configOptions">The master configuration options (<see cref="IOptions{MasterConfig}"/>),
        /// used to determine the root paths for journaling and the environment name.</param>
        public JournalService(ILogger<JournalService> logger, IOptions<MasterConfig> configOptions)
        {
            _logger = logger;
            var config = configOptions.Value;
            // _journalEnvRootPath is kept for compatibility with older methods if they exist, but new structure uses more specific paths.
            _journalEnvRootPath = Path.Combine(config.JournalRootPath, SanitizeFolderName(config.EnvironmentName));

            _actionJournalRootPath = Path.Combine(_journalEnvRootPath, "ActionJournal");
            _changeJournalRootPath = Path.Combine(_journalEnvRootPath, "ChangeJournal");
            _backupRepositoryRootPath = Path.Combine(_journalEnvRootPath, "BackupRepository");

            _actionJournalIndexPath = Path.Combine(_actionJournalRootPath, "action_journal_index.log");
            _changeJournalIndexPath = Path.Combine(_changeJournalRootPath, "system_changes_index.log");

            // Ensure base directories exist
            Directory.CreateDirectory(_actionJournalRootPath);
            Directory.CreateDirectory(Path.Combine(_changeJournalRootPath, "artifacts")); // For storing artifacts linked from ChangeJournal
            Directory.CreateDirectory(_backupRepositoryRootPath);
            _logger.LogInformation("JournalService initialized. Action Journal Path: {ActionJournalPath}, Change Journal Path: {ChangeJournalPath}", _actionJournalRootPath, _changeJournalRootPath);
        }

        // These methods seem to be part of an older Operation-centric journaling approach.
        // They are not directly part of the IJournalService interface methods related to MasterAction/ChangeJournal.
        // Documentation will be added assuming they are still used or are for internal compatibility.

        /// <summary>
        /// Records the initiation of a new general node action (legacy or internal).
        /// This method creates a directory structure for the action's journal and writes initial metadata.
        /// </summary>
        /// <param name="nodeAction">The <see cref="NodeAction"/> object representing the action being initiated.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RecordOperationInitiatedAsync(NodeAction nodeAction)
        {
            var journalFolderName = $"{nodeAction.CreationTime:yyyyMMddHHmmssfff}-{SanitizeFileName(nodeAction.Id)}";
            var opFolderPath = Path.Combine(_journalEnvRootPath, "Operations", journalFolderName);

            if (_operationJournalFolders.TryAdd(nodeAction.Id, opFolderPath))
            {
                try
                {
                    Directory.CreateDirectory(opFolderPath);
                    Directory.CreateDirectory(Path.Combine(opFolderPath, "logs"));
                    Directory.CreateDirectory(Path.Combine(opFolderPath, "results"));

                    var infoPath = Path.Combine(opFolderPath, "info.json");
                    await WriteJsonAsync(infoPath, nodeAction);
                    _logger.LogInformation("Legacy NodeAction Journal created for ActionId '{ActionId}' at '{OpFolderPath}'", nodeAction.Id, opFolderPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create legacy node action journal directory for ActionId '{ActionId}'", nodeAction.Id);
                    _operationJournalFolders.TryRemove(nodeAction.Id, out _);
                }
            }
        }
        
        /// <summary>
        /// Records the completion of a general node action (legacy or internal).
        /// This method updates the 'info.json' file and writes an 'overall_result.json'.
        /// </summary>
        /// <param name="nodeAction">The <see cref="NodeAction"/> object in its final terminal state.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RecordOperationCompletedAsync(NodeAction nodeAction)
        {
            if (_operationJournalFolders.TryGetValue(nodeAction.Id, out var opFolderPath))
            {
                var infoPath = Path.Combine(opFolderPath, "info.json");
                await WriteJsonAsync(infoPath, nodeAction);

                var overallResultPath = Path.Combine(opFolderPath, "results", "overall_result.json");
                await WriteJsonAsync(overallResultPath, nodeAction);

                _operationJournalFolders.TryRemove(nodeAction.Id, out _);
                _logger.LogInformation("Legacy NodeAction Journal finalized for ActionId '{ActionId}'", nodeAction.Id);
            }
            else
            {
                _logger.LogWarning("Could not record legacy node action completion: Journal folder not found for ActionId '{ActionId}'", nodeAction.Id);
            }
        }

        /// <summary>
        /// Records an intermediate change in the overall status of an ongoing general node action (legacy or internal).
        /// </summary>
        /// <param name="nodeAction">The <see cref="NodeAction"/> object with its updated status.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RecordOperationStatusChangedAsync(NodeAction nodeAction)
        {
            if (_operationJournalFolders.TryGetValue(nodeAction.Id, out var opFolderPath))
            {
                var infoPath = Path.Combine(opFolderPath, "info.json");
                await WriteJsonAsync(infoPath, nodeAction);
            }
        }
        
        /// <summary>
        /// Records the final result of an individual node task for a general node action (legacy or internal).
        /// </summary>
        /// <param name="task">The <see cref="NodeTask"/> object in its terminal state, containing the result payload.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RecordNodeTaskStatusChangedAsync(NodeTask task)
        {
            if (task.Status.IsTerminal() && task.ResultPayload != null)
            {
                if (_operationJournalFolders.TryGetValue(task.ActionId, out var opFolderPath)) // task.OperationId to task.ActionId
                {
                    var resultFileName = $"{SanitizeFileName(task.NodeName)}-{SanitizeFileName(task.TaskId)}-result.json";
                    var resultPath = Path.Combine(opFolderPath, "results", resultFileName);
                    await WriteJsonAsync(resultPath, task.ResultPayload);
                }
            }
        }

        /// <summary>
        /// Appends a single log entry from a slave to the appropriate per-task log file for a general node action (legacy or internal).
        /// </summary>
        /// <param name="logEntry">The <see cref="SlaveTaskLogEntry"/> DTO received from the slave.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task AppendToNodeTaskLogAsync(SlaveTaskLogEntry logEntry)
        {
            // SlaveTaskLogEntry DTO now uses ActionId.
            if (!_operationJournalFolders.TryGetValue(logEntry.ActionId, out var opFolderPath)) // logEntry.OperationId -> logEntry.ActionId
            {
                _logger.LogWarning("Could not append to legacy task log: Journal folder not found for ActionId '{ActionId}'", logEntry.ActionId); // logEntry.OperationId -> logEntry.ActionId
                return;
            }

            var logFileName = $"{SanitizeFileName(logEntry.NodeName)}-{SanitizeFileName(logEntry.TaskId)}.log";
            var logFilePath = Path.Combine(opFolderPath, "logs", logFileName);
            var logLine = $"{logEntry.TimestampUtc:yyyy-MM-dd HH:mm:ss.fff}Z [{logEntry.LogLevel}] {logEntry.LogMessage}{Environment.NewLine}";

            await WriteTextAsync(logFilePath, logLine);
        }
        
        // These are placeholder methods from the original IJournalService, now adapted or to be removed if fully superseded.
        // For this pass, they are documented as simple logging actions if not fitting the Action/Change Journal model.
        #region Placeholder/Logging-Only Methods (Review if these should interact with Action/Change Journals)
        
        /// <summary>
        /// Logs an informational event that a node action cancellation was requested.
        /// The actual state changes reflecting cancellation are recorded via <see cref="RecordMasterActionCompletedAsync"/> or <see cref="FinalizeStateChangeAsync"/>.
        /// </summary>
        /// <param name="nodeAction">The <see cref="NodeAction"/> for which cancellation was requested.</param>
        /// <param name="cancelledBy">Identifier for who requested the cancellation.</param>
        /// <returns>A task representing the asynchronous logging operation.</returns>
        public Task RecordOperationCancellationRequestedAsync(NodeAction nodeAction, string cancelledBy)
        {
             _logger.LogInformation("Journal Event: NodeAction Cancellation Requested. ID: {ActionId}, By: {CancelledBy}", nodeAction.Id, cancelledBy);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Logs the creation of a <see cref="NodeTask"/> within a NodeAction.
        /// Detailed status, logs, and results for the NodeTask are recorded via other specific methods
        /// (e.g., <see cref="AppendToStageLogAsync"/>, <see cref="RecordNodeTaskResultAsync"/> within the Action Journal).
        /// </summary>
        /// <param name="task">The <see cref="NodeTask"/> that was created.</param>
        /// <returns>A task representing the asynchronous logging operation.</returns>
        public Task RecordNodeTaskCreatedAsync(NodeTask task)
        {
            _logger.LogInformation("Journal Event: Node Task Created. TaskID: {TaskId}, ActionID: {ActionId}, Node: {NodeName}, Type: {TaskType}",
                task.TaskId, task.ActionId, task.NodeName, task.Type);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Logs a general event related to a specific agent. This does not write to a specific operation's journal
        /// but rather to the main service log. For operation-specific agent logs, use <see cref="AppendToStageLogAsync"/>.
        /// </summary>
        /// <param name="agentId">The identifier of the agent related to the event.</param>
        /// <param name="message">The message describing the event.</param>
        /// <param name="logLevel">The severity level of the event, using <see cref="SiteKeeper.Shared.Enums.LogLevel"/>.</param>
        /// <param name="exception">Optional. An exception associated with the event.</param>
        /// <param name="details">Optional. A dictionary of additional structured details about the event.</param>
        /// <returns>A task representing the asynchronous logging operation.</returns>
        public Task RecordAgentEventAsync(string agentId, string message, SiteKeeper.Shared.Enums.LogLevel logLevel, Exception? exception = null, Dictionary<string, object>? details = null)
        {
            var msLogLevel = (Microsoft.Extensions.Logging.LogLevel)(int)logLevel; // Map to MS logging enum
            _logger.Log(msLogLevel, exception, "Agent Event for {AgentId}: {Message}. Details: {DetailsJson}", agentId, message, details != null ? JsonSerializer.Serialize(details) : "N/A");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Logs a general system-wide event not tied to a specific operation or agent.
        /// These events are recorded in the main service log.
        /// </summary>
        /// <param name="eventName">A descriptive name for the system event (e.g., "ServiceStartup", "ConfigReloaded").</param>
        /// <param name="message">The message describing the event.</param>
        /// <param name="logLevel">The severity level of the event, using <see cref="SiteKeeper.Shared.Enums.LogLevel"/>.</param>
        /// <param name="details">Optional. A dictionary of additional structured details about the event.</param>
        /// <returns>A task representing the asynchronous logging operation.</returns>
        public Task RecordSystemEventAsync(string eventName, string message, SiteKeeper.Shared.Enums.LogLevel logLevel, Dictionary<string, object>? details = null)
        {
            var msLogLevel = (Microsoft.Extensions.Logging.LogLevel)(int)logLevel; // Map to MS logging enum
            _logger.Log(msLogLevel, "System Event {EventName}: {Message}. Details: {DetailsJson}", eventName, message, details != null ? JsonSerializer.Serialize(details) : "N/A");
            return Task.CompletedTask;
        }
        #endregion

        #region Private Helpers

        /// <summary>
        /// Finds the full directory path for a given Master Action ID by searching the Action Journal index or scanning directories.
        /// </summary>
        /// <param name="masterActionId">The ID of the Master Action.</param>
        /// <returns>The full path to the Master Action's journal directory if found; otherwise, null.</returns>
        private string? FindActionJournalDirectoryPath(string masterActionId)
        {
            // Efficiently find from index first, then fallback to directory scan if needed.
            // This is a simplified version for now. A real implementation would parse _actionJournalIndexPath.
            if (!Directory.Exists(_actionJournalRootPath))
            {
                _logger.LogWarning("Cannot search for journal. Action Journal root directory not found at '{Path}'", _actionJournalRootPath);
                return null;
            }
            // Folder name format: {timestamp}-{masterActionId}
            return Directory.GetDirectories(_actionJournalRootPath, $"*-{SanitizeFileName(masterActionId)}").FirstOrDefault();
        }

        /// <summary>
        /// Writes an object to a specified file path as indented JSON.
        /// This method uses a <see cref="SemaphoreSlim"/> to ensure thread-safe file access for a given path.
        /// It creates the directory if it doesn't exist.
        /// </summary>
        /// <param name="filePath">The full path to the file where the JSON data will be written.</param>
        /// <param name="data">The object to serialize and write.</param>
        /// <returns>A task representing the asynchronous write operation.</returns>
        private async Task WriteJsonAsync(string filePath, object data)
        {
            var fileLock = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
            await fileLock.WaitAsync();
            try
            {
				var directory = Path.GetDirectoryName(filePath);
                if (directory != null)
                {
                    Directory.CreateDirectory(directory); // Ensure directory exists
                }

                var json = JsonSerializer.Serialize(data, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);
            }
            finally
            {
                fileLock.Release();
            }
        }
        
        /// <summary>
        /// Appends text to a specified file.
        /// This method uses a <see cref="SemaphoreSlim"/> to ensure thread-safe file access for a given path.
        /// It creates the directory if it doesn't exist.
        /// </summary>
        /// <param name="filePath">The full path to the file to which text will be appended.</param>
        /// <param name="text">The text to append.</param>
        /// <returns>A task representing the asynchronous append operation.</returns>
        private async Task WriteTextAsync(string filePath, string text) // Renamed from AppendTextAsync for clarity if it's sometimes overwrite
        {
            var fileLock = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
            await fileLock.WaitAsync();
            try
            {
				var directory = Path.GetDirectoryName(filePath);
                if (directory != null)
                {
                    Directory.CreateDirectory(directory); // Ensure directory exists
                }

                await File.AppendAllTextAsync(filePath, text); // Kept as Append for log files
            }
            finally
            {
                fileLock.Release();
            }
        }
        
        /// <summary>
        /// Sanitizes a string to remove characters that are invalid for use in directory names.
        /// Replaces invalid characters with underscores.
        /// </summary>
        /// <param name="name">The string to sanitize.</param>
        /// <returns>A sanitized string suitable for use as a directory name.</returns>
        private static string SanitizeFolderName(string name) => string.Join("_", name.Split(Path.GetInvalidPathChars()));
        
        /// <summary>
        /// Sanitizes a string to remove characters that are invalid for use in file names.
        /// Replaces invalid characters with underscores.
        /// </summary>
        /// <param name="name">The string to sanitize.</param>
        /// <returns>A sanitized string suitable for use as a file name.</returns>
        private static string SanitizeFileName(string name) => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        #endregion

        // --- IJournalService Implementation ---

        #region Action Journal Methods (IJournalService)

        /// <summary>
        /// Records the initiation of a new Master Action in the Action Journal.
        /// This creates the main directory for the workflow (e.g., `EnvironmentName/ActionJournal/Timestamp-ActionID/`),
        /// an initial `_init` stage subdirectory, and writes the initial <see cref="MasterAction"/> state to `master_action_info.json`.
        /// An entry is also added to the `action_journal_index.log`.
        /// </summary>
        /// <param name="action">The <see cref="MasterAction"/> object representing the action being initiated.</param>
        /// <returns>A task representing the asynchronous journaling operation.</returns>
        public async Task RecordMasterActionInitiatedAsync(MasterAction action)
        {
            var journalFolderName = $"{action.StartTime:yyyyMMddHHmmssfff}-{SanitizeFileName(action.Id)}";
            var actionJournalPath = Path.Combine(_actionJournalRootPath, journalFolderName);

            var state = new ActiveJournalState
            {
                MasterActionId = action.Id,
                MasterActionJournalPath = actionJournalPath,
                CurrentStageIndex = 0, // Initialize stage index
                CurrentStageName = "_init" // Initial implicit stage
            };

            if (!_activeJournals.TryAdd(action.Id, state))
            {
                _logger.LogWarning("Could not add MasterActionId '{MasterActionId}' to active journals, it may already exist.", action.Id);
                return; // Or handle as an error if re-initiation shouldn't happen
            }
            
            try
            {
                Directory.CreateDirectory(actionJournalPath);
                Directory.CreateDirectory(Path.Combine(actionJournalPath, "stages")); // Main stages folder

                // Create the initial implicit stage directory structure immediately.
                var initialStagePath = state.CurrentStagePath;
                Directory.CreateDirectory(initialStagePath);
                Directory.CreateDirectory(Path.Combine(initialStagePath, "logs"));
                Directory.CreateDirectory(Path.Combine(initialStagePath, "results"));
                // Optionally write an info file for the _init stage if needed, though it might be implicit.

                var infoPath = Path.Combine(actionJournalPath, "master_action_info.json");
                await WriteJsonAsync(infoPath, action);

                var indexEntry = new { Timestamp = action.StartTime, action.Id, ActionType = action.Type.ToString(), action.InitiatedBy, JournalPath = journalFolderName };
                await AppendJsonLineAsync(_actionJournalIndexPath, indexEntry);

                _logger.LogInformation("Action Journal created for MasterActionId '{Id}' at '{Path}'", action.Id, actionJournalPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Action Journal directory or initial files for MasterActionId '{Id}'", action.Id);
                _activeJournals.TryRemove(action.Id, out _); // Clean up if initiation failed
            }
        }

        /// <summary>
        /// Records the final completion state of a Master Action in the Action Journal.
        /// This updates the `master_action_info.json` file with the action's terminal status, end time, and final result.
        /// The active journal state for this action is then removed.
        /// </summary>
        /// <param name="action">The <see cref="MasterAction"/> object in its final completed, failed, or cancelled state.</param>
        /// <returns>A task representing the asynchronous journaling operation.</returns>
        public async Task RecordMasterActionCompletedAsync(MasterAction action)
        {
            if (_activeJournals.TryGetValue(action.Id, out var state))
            {
                // Record the final result payload if it exists
                if (action.FinalResultPayload != null)
                {
                    var resultPath = Path.Combine(state.MasterActionJournalPath, "master_action_final_result.json"); // Specific name for overall result
                    await WriteJsonAsync(resultPath, action.FinalResultPayload);
                }

                var infoPath = Path.Combine(state.MasterActionJournalPath, "master_action_info.json");
                await WriteJsonAsync(infoPath, action); // Write final state of MasterAction

                _activeJournals.TryRemove(action.Id, out _);
                _logger.LogInformation("Action Journal finalized for MasterActionId '{Id}'", action.Id);
            }
            else
            {
                _logger.LogWarning("Could not finalize Action Journal: No active journal found for MasterActionId '{Id}'. Might have already been completed or not initiated correctly.", action.Id);
            }
        }

        /// <summary>
        /// Records the initiation of a new stage within an active Master Action's journal.
        /// This creates a stage-specific subdirectory (e.g., `stages/001-StageName/`) and writes an initial `stage_info.json`
        /// file with the stage name, index, start time, and input data.
        /// </summary>
        /// <param name="context">The <see cref="MasterActionContext"/> for the currently executing Master Action.</param>
        /// <param name="stageName">A descriptive name for the stage (e.g., "NodeRestart", "PackageDeployment").</param>
        /// <param name="stageInput">Optional. Input data or parameters specific to this stage, to be serialized and recorded.</param>
        /// <returns>A task representing the asynchronous journaling operation.</returns>
        public async Task RecordStageInitiatedAsync(MasterActionContext context, string stageName, object? stageInput)
        {
            if (!_activeJournals.TryGetValue(context.MasterActionId, out var state))
            {
                _logger.LogWarning("Cannot record stage initiation: No active journal found for MasterActionId '{MasterActionId}'", context.MasterActionId);
                return;
            }
            // state.CurrentStageIndex was advanced by MasterActionContext.BeginStageAsync before calling this
            // state.CurrentStageName was also set by MasterActionContext.BeginStageAsync

            var currentStagePath = state.CurrentStagePath; // Path is now based on updated index and name
            Directory.CreateDirectory(currentStagePath);
            Directory.CreateDirectory(Path.Combine(currentStagePath, "logs"));
            Directory.CreateDirectory(Path.Combine(currentStagePath, "results"));

            var stageInfo = new { StageName = stageName, StageIndex = state.CurrentStageIndex, StartTime = DateTime.UtcNow, Input = stageInput };
            await WriteJsonAsync(Path.Combine(currentStagePath, "stage_info.json"), stageInfo);
            _logger.LogInformation("Stage '{StageName}' (Index {StageIndex}) initiated for MasterActionId '{MasterActionId}'", stageName, state.CurrentStageIndex, context.MasterActionId);
        }

        /// <summary>
        /// Records the completion of a stage within an active Master Action's journal.
        /// This updates the `stage_info.json` file for the current stage with an end time and serializes the stage result
        /// to `stage_result.json` within the stage's "results" subdirectory.
        /// </summary>
        /// <param name="context">The <see cref="MasterActionContext"/> for the currently executing Master Action.</param>
        /// <param name="stageName">The name of the stage that has completed. Used for verification against current context.</param>
        /// <param name="stageResult">Optional. The result or output data from the completed stage, to be serialized and recorded.</param>
        /// <returns>A task representing the asynchronous journaling operation.</returns>
        public async Task RecordStageCompletedAsync(MasterActionContext context, string stageName, object? stageResult)
        {
            if (!_activeJournals.TryGetValue(context.MasterActionId, out var state))
            {
                _logger.LogWarning("Cannot record stage completion: No active journal found for MasterActionId '{MasterActionId}'", context.MasterActionId);
                return;
            }

            if (state.CurrentStageName != stageName)
            {
                _logger.LogWarning("Stage name mismatch on completion. Expected '{ExpectedStage}', got '{ActualStage}' for MasterActionId '{MasterActionId}'. Journaling to expected path.",
                    state.CurrentStageName, stageName, context.MasterActionId);
                // Continue using state.CurrentStagePath as it's based on the name set at BeginStage.
            }

            var stageInfoPath = Path.Combine(state.CurrentStagePath, "stage_info.json");
            if (File.Exists(stageInfoPath))
            {
                try
                {
                    var stageInfoJson = await ReadTextAsync(stageInfoPath);
                    var stageInfoNode = System.Text.Json.Nodes.JsonNode.Parse(stageInfoJson); // Using JsonNode for modification
                    if (stageInfoNode != null)
                    {
                        stageInfoNode["EndTime"] = DateTime.UtcNow;
                        // TODO: Optionally add stage outcome (Success/Failure) if available from context/result
                        await WriteJsonAsync(stageInfoPath, stageInfoNode);
                    }
                }
                catch(Exception ex)
                {
                     _logger.LogError(ex, "Failed to update stage_info.json for stage '{StageName}' in MasterActionId '{MasterActionId}'", state.CurrentStageName, context.MasterActionId);
                }
            }
            else
            {
                _logger.LogWarning("stage_info.json not found for completed stage '{StageName}' in MasterActionId '{MasterActionId}'. Path: {Path}", state.CurrentStageName, context.MasterActionId, stageInfoPath);
            }

            if (stageResult != null)
            {
                var resultPath = Path.Combine(state.CurrentStagePath, "results", "stage_result.json");
                await WriteJsonAsync(resultPath, stageResult);
            }
            _logger.LogInformation("Stage '{StageName}' (Index {StageIndex}) completed for MasterActionId '{MasterActionId}'", state.CurrentStageName, state.CurrentStageIndex, context.MasterActionId);
        }

        /// <summary>
        /// Appends a log entry from a slave agent to the currently active stage's log file for a given Master Action.
        /// The log file is named after the slave node (e.g., `NodeName.log`) within the stage's "logs" subdirectory.
        /// </summary>
        /// <param name="masterActionId">The ID of the Master Action to which the log belongs.</param>
        /// <param name="logEntry">The <see cref="SlaveTaskLogEntry"/> DTO containing the log message, source node, timestamp, and level.</param>
        /// <returns>A task representing the asynchronous append operation.</returns>
        public async Task AppendToStageLogAsync(string masterActionId, SlaveTaskLogEntry logEntry)
        {
            if (!_activeJournals.TryGetValue(masterActionId, out var state))
            {
                _logger.LogWarning("Cannot append to stage log: No active journal found for MasterActionId '{Id}'", masterActionId);
                return;
            }

            var logFileName = $"{SanitizeFileName(logEntry.NodeName)}.log"; // Per-node log file within the stage
            var logFilePath = Path.Combine(state.CurrentStagePath, "logs", logFileName);

            var logLine = $"{logEntry.TimestampUtc:yyyy-MM-dd HH:mm:ss.fff}Z [{logEntry.LogLevel}] {logEntry.LogMessage}{Environment.NewLine}";
            await AppendTextAsync(logFilePath, logLine); // AppendTextAsync handles file locking and directory creation
        }

        /// <summary>
        /// Records the final result payload from an individual <see cref="NodeTask"/> in the currently active stage's "results" subdirectory.
        /// The result is stored in a JSON file named after the node (e.g., `NodeName-result.json`).
        /// </summary>
        /// <param name="context">The <see cref="MasterActionContext"/> for the currently executing Master Action.</param>
        /// <param name="task">The <see cref="NodeTask"/> whose result is to be recorded. Its <see cref="NodeTask.ResultPayload"/> is used.</param>
        /// <returns>A task representing the asynchronous journaling operation.</returns>
        public async Task RecordNodeTaskResultAsync(MasterActionContext context, NodeTask task)
        {
            if (!_activeJournals.TryGetValue(context.MasterActionId, out var state))
            {
                 _logger.LogWarning("Cannot record node task result: No active journal found for MasterActionId '{MasterActionId}'", context.MasterActionId);
                return;
            }

            if (task.ResultPayload == null)
            {
                _logger.LogDebug("NodeTaskResult for Task '{TaskId}' on Node '{NodeName}' has no payload to record.", task.TaskId, task.NodeName);
                return;
            }

            var resultFileName = $"{SanitizeFileName(task.NodeName)}-{SanitizeFileName(task.TaskId)}-taskresult.json"; // More specific name
            var resultPath = Path.Combine(state.CurrentStagePath, "results", resultFileName);
            await WriteJsonAsync(resultPath, task.ResultPayload);
            _logger.LogInformation("Result for Task '{TaskId}' on Node '{NodeName}' recorded for MasterActionId '{MasterActionId}'", task.TaskId, task.NodeName, context.MasterActionId);
        }
        #endregion

        #region Change Journal Methods (IJournalService)

        /// <summary>
        /// Initiates a record for a new state change in the Change Journal (system_changes_index.log).
        /// An initial entry with "Initiated" status is logged. If the change type is <see cref="ChangeEventType.Backup"/>,
        /// a dedicated directory is created in the Backup Repository.
        /// The association between the returned Change ID and the source MasterActionId is tracked for finalization.
        /// </summary>
        /// <param name="changeInfo">A <see cref="StateChangeInfo"/> DTO containing details about the state change being initiated.</param>
        /// <returns>A <see cref="StateChangeCreationResult"/> containing the unique ID for this change event and, if applicable, the full path to the backup artifact directory.</returns>
        public async Task<StateChangeCreationResult> InitiateStateChangeAsync(StateChangeInfo changeInfo)
        {
            var changeId = $"chg-{Guid.NewGuid():N}";
            _pendingChanges[changeId] = changeInfo.SourceMasterActionId ?? "_system"; // Store source action ID, or a placeholder

            var record = new SystemChangeRecord
            {
                Timestamp = DateTime.UtcNow,
                ChangeId = changeId,
                EventType = $"{changeInfo.Type}Initiated", // e.g., "BackupInitiated"
                Description = changeInfo.Description,
                SourceMasterActionId = changeInfo.SourceMasterActionId,
                InitiatedBy = changeInfo.InitiatedBy
            };
            await AppendJsonLineAsync(_changeJournalIndexPath, record);
            _logger.LogInformation("Initiated Change Journal record {ChangeId} for event type {EventType}, sourced from MasterActionId '{SourceMasterActionId}'",
                changeId, changeInfo.Type, changeInfo.SourceMasterActionId ?? "N/A");

            string? backupPath = null;
            if (changeInfo.Type == ChangeEventType.Backup)
            {
                var backupFolderName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-backup-{SanitizeFileName(changeId)}";
                backupPath = Path.Combine(_backupRepositoryRootPath, backupFolderName);
                Directory.CreateDirectory(backupPath);
                _logger.LogInformation("Created backup artifact directory for ChangeId {ChangeId} at '{BackupPath}'", changeId, backupPath);
            }

            return new StateChangeCreationResult { ChangeId = changeId, BackupArtifactPath = backupPath };
        }

        /// <summary>
        /// Finalizes a state change event by writing a 'Completed' (or other terminal status) record to the Change Journal.
        /// It also saves the final result artifact (e.g., a manifest, backup summary) to the Change Journal's private artifact store
        /// in a subdirectory named after the Change ID.
        /// </summary>
        /// <param name="finalizationInfo">A <see cref="StateChangeFinalizationInfo"/> DTO containing the final outcome, description, and result data for the change.</param>
        /// <returns>A task representing the asynchronous journaling operation.</returns>
        public async Task FinalizeStateChangeAsync(StateChangeFinalizationInfo finalizationInfo)
        {
            if (!_pendingChanges.TryRemove(finalizationInfo.ChangeId, out var sourceMasterActionId))
            {
                _logger.LogWarning("Could not finalize Change Journal record {ChangeId}: Not found in pending changes or already finalized.", finalizationInfo.ChangeId);
                return;
            }

            var artifactSubfolder = Path.Combine(_changeJournalRootPath, "artifacts", SanitizeFolderName(finalizationInfo.ChangeId));
            Directory.CreateDirectory(artifactSubfolder);

            string? artifactPath = null;
            if (finalizationInfo.ResultArtifact != null)
            {
                artifactPath = Path.Combine(artifactSubfolder, "result_artifact.json");
                await WriteJsonAsync(artifactPath, finalizationInfo.ResultArtifact);
            }

            var record = new SystemChangeRecord
            {
                Timestamp = DateTime.UtcNow,
                ChangeId = finalizationInfo.ChangeId,
                EventType = finalizationInfo.Outcome.ToString(), // Uses the outcome as the event type, e.g., "Success", "Failure"
                Outcome = finalizationInfo.Outcome.ToString(),
                Description = finalizationInfo.Description,
                ArtifactPath = artifactPath,
                SourceMasterActionId = sourceMasterActionId,
                InitiatedBy = finalizationInfo.FinalizedBy // Assuming FinalizedBy captures the relevant user/system
            };
            await AppendJsonLineAsync(_changeJournalIndexPath, record);
            _logger.LogInformation("Finalized Change Journal record {ChangeId} with outcome {Outcome}", finalizationInfo.ChangeId, finalizationInfo.Outcome);
        }
        #endregion

        #region Query Methods (IJournalService)

        /// <summary>
        /// Lists a high-level, chronological history of system state changes by reading from the Change Journal's index file (`system_changes_index.log`).
        /// Supports pagination and basic filtering.
        /// </summary>
        /// <param name="queryParams">The <see cref="JournalQueryParameters"/> defining filtering, sorting, and pagination for the query.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is a tuple containing the list of <see cref="JournalEntrySummary"/> items for the current page and the total count of items matching the query.</returns>
        public async Task<(IEnumerable<JournalEntrySummary> Items, int TotalCount)> ListJournalEntriesAsync(JournalQueryParameters queryParams)
        {
            var summaries = new List<JournalEntrySummary>();
            if (!File.Exists(_changeJournalIndexPath))
            {
                 _logger.LogWarning("Change Journal index file not found at '{Path}'. Returning empty list.", _changeJournalIndexPath);
                return (summaries, 0);
            }

            var lines = await File.ReadAllLinesAsync(_changeJournalIndexPath);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var record = JsonSerializer.Deserialize<SystemChangeRecord>(line, _jsonOptions);
                    if(record == null) continue;

                    // Apply filtering based on queryParams
                    if (queryParams.StartDate.HasValue && record.Timestamp < queryParams.StartDate.Value) continue;
                    if (queryParams.EndDate.HasValue && record.Timestamp > queryParams.EndDate.Value) continue;
                    if (!string.IsNullOrWhiteSpace(queryParams.OperationType) && !record.EventType.Equals(queryParams.OperationType, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.IsNullOrWhiteSpace(queryParams.FilterText) &&
                        !((record.Description != null && record.Description.Contains(queryParams.FilterText, StringComparison.OrdinalIgnoreCase)) ||
                          record.EventType.Contains(queryParams.FilterText, StringComparison.OrdinalIgnoreCase) ||
                          (record.SourceMasterActionId != null && record.SourceMasterActionId.Contains(queryParams.FilterText, StringComparison.OrdinalIgnoreCase)) )) continue;

                    summaries.Add(new JournalEntrySummary
                    {
                        JournalRecordId = record.ChangeId, // Using ChangeId as the primary ID for these summaries
                        Timestamp = record.Timestamp,
                        OperationType = record.EventType, // e.g., "BackupInitiated", "Success", "Failure"
                        Summary = record.Description,
                        Outcome = record.Outcome
                    });
                }
                catch (Exception ex) { _logger.LogError(ex, "Failed to parse line in change journal index: {Line}", line); }
            }

            // Apply sorting
            bool descending = queryParams.SortOrder?.Equals("desc", StringComparison.OrdinalIgnoreCase) ?? true;
            var sortBy = queryParams.SortBy ?? "Timestamp"; // Default sort by Timestamp

            IEnumerable<JournalEntrySummary> sortedSummaries;
            // Add more sortable fields as needed
            // For now, only Timestamp is explicitly supported for sorting ChangeJournal entries.
            // if (sortBy.Equals("OperationType", StringComparison.OrdinalIgnoreCase))
            //     sortedSummaries = descending ? summaries.OrderByDescending(s => s.OperationType) : summaries.OrderBy(s => s.OperationType);
            // else
            sortedSummaries = descending ? summaries.OrderByDescending(s => s.Timestamp) : summaries.OrderBy(s => s.Timestamp);

            var totalCount = sortedSummaries.Count();
			var page = queryParams.Page ?? 1;
			var pageSize = queryParams.PageSize ?? 10;
			var pagedItems = sortedSummaries.Skip( (page - 1) * pageSize ).Take( pageSize ).ToList();

            return (pagedItems, totalCount);
        }

        /// <summary>
        /// Gets the full diagnostic details for a Master Action by reading its dedicated Action Journal.
        /// This involves finding its folder (e.g., via `action_journal_index.log` or by scanning),
        /// then reading `master_action_info.json`, and potentially aggregating stage data and logs.
        /// </summary>
        /// <param name="masterActionId">The unique ID of the Master Action to retrieve details for.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is a <see cref="JournalEntry"/> DTO containing the aggregated details, or null if not found.</returns>
        public async Task<JournalEntry?> GetJournalEntryDetailsAsync(string masterActionId)
        {
            var actionJournalDir = FindActionJournalDirectoryPath(masterActionId);
            if (actionJournalDir == null)
            {
                _logger.LogInformation("No Action Journal directory found for MasterActionId '{MasterActionId}'", masterActionId);
                return null;
            }

            var infoPath = Path.Combine(actionJournalDir, "master_action_info.json");
            if (!File.Exists(infoPath))
            {
                _logger.LogWarning("Action Journal directory found for '{MasterActionId}', but master_action_info.json is missing.", masterActionId);
                return null;
            }
            
            try
            {
                var opJson = await ReadTextAsync(infoPath); // Uses private helper with locking
                var masterAction = JsonSerializer.Deserialize<MasterAction>(opJson, _jsonOptions);
                if (masterAction == null) return null;
            
                // In a full implementation, you would recursively scan the 'stages' subfolder
                // and aggregate all stage_info.json, stage_result.json, and *.log files.
                // For now, we return the MasterAction object as the 'Details'.
                return new JournalEntry // This is SiteKeeper.Shared.DTOs.API.Journal.JournalEntry
                {
                    JournalRecordId = masterAction.Id, // MasterActionId is the JournalRecordId here
                    Timestamp = masterAction.StartTime,
                    OperationType = masterAction.Type.ToString(),
                    Summary = masterAction.Name ?? $"Action {masterAction.Type}",
                    Outcome = masterAction.OverallStatus.ToString(),
                    DurationSeconds = masterAction.EndTime.HasValue ? (int)(masterAction.EndTime.Value - masterAction.StartTime).TotalSeconds : (int?)null,
                    Details = masterAction, // The entire MasterAction can serve as details
                    LogSnippets = masterAction.RecentLogs?.ToList() // Assuming RecentLogs is a suitable snippet source
                };
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Failed to read or parse master_action_info.json for MasterActionId '{MasterActionId}' from '{Path}'", masterActionId, infoPath);
                return null;
            }
        }

        /// <summary>
        /// Retrieves a historical/archived <see cref="MasterAction"/> object from the Action Journal by its ID.
        /// This is primarily used internally or for deep diagnostic querying of completed operations.
        /// </summary>
        /// <param name="masterActionId">The ID of the MasterAction to retrieve.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is the deserialized <see cref="MasterAction"/> object if found; otherwise, null.</returns>
        public async Task<MasterAction?> GetArchivedMasterActionAsync(string masterActionId)
        {
            var actionJournalDir = FindActionJournalDirectoryPath(masterActionId);
            if (actionJournalDir == null)
            {
                _logger.LogInformation("No Action Journal directory found for archived MasterActionId '{Id}'", masterActionId);
                return null;
            }

            var infoPath = Path.Combine(actionJournalDir, "master_action_info.json");
            if (!File.Exists(infoPath))
            {
                _logger.LogWarning("Action Journal directory found for MasterActionId '{Id}', but master_action_info.json is missing.", masterActionId);
                return null;
            }

            try
            {
                var opJson = await ReadTextAsync(infoPath); // Uses private helper with locking
                return JsonSerializer.Deserialize<MasterAction>(opJson, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read or parse master_action_info.json for MasterActionId '{Id}'", masterActionId);
                return null;
            }
        }

        /// <summary>
        /// Finds the last successful <see cref="SystemChangeRecord"/> of a specific <see cref="ChangeEventType"/>
        /// by querying the Change Journal's index file (`system_changes_index.log`).
        /// This is useful for operations like finding the manifest from the last successful update or the details of the last backup.
        /// </summary>
        /// <param name="type">The <see cref="ChangeEventType"/> to search for (e.g., <see cref="ChangeEventType.EnvUpdateOnline"/>, <see cref="ChangeEventType.Backup"/>).</param>
        /// <returns>A task that represents the asynchronous operation. The task result is the <see cref="SystemChangeRecord"/> of the last successful change of the specified type, or null if no such record is found.</returns>
        public async Task<SystemChangeRecord?> GetLastSuccessfulChangeOfTypeAsync(ChangeEventType type)
        {
            if (!File.Exists(_changeJournalIndexPath))
            {
                _logger.LogWarning("Change Journal index file not found at '{Path}'. Cannot get last successful change.", _changeJournalIndexPath);
                return null;
            }

            var lines = await File.ReadAllLinesAsync(_changeJournalIndexPath);
            foreach (var line in lines.Reverse()) // Read in reverse to find the last one efficiently
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var record = JsonSerializer.Deserialize<SystemChangeRecord>(line, _jsonOptions);
                    // Check if outcome is "Success" and if EventType starts with the enum's string representation
                    // (e.g., "EnvUpdateOnlineCompleted" or "EnvUpdateOnlineSuccess" would match "EnvUpdateOnline")
                    if (record != null &&
                        record.Outcome != null && record.Outcome.Equals(OperationOutcome.Success.ToString(), StringComparison.OrdinalIgnoreCase) &&
                        record.EventType != null && record.EventType.StartsWith(type.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        return record;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to parse line in change journal index while searching for last successful change: {Line}", line);
                    // Continue to the next line
                }
            }
            _logger.LogInformation("No successful change of type '{ChangeEventType}' found in the Change Journal.", type);
            return null;
        }
        #endregion

        #region Private Helpers (XML Docs added)

        /// <summary>
        /// Asynchronously reads all text from a specified file path.
        /// This method uses a <see cref="SemaphoreSlim"/> to ensure thread-safe file access for the given path.
        /// </summary>
        /// <param name="filePath">The full path to the file to read.</param>
        /// <returns>A task that represents the asynchronous read operation. The task result contains the file's content as a string.</returns>
        private async Task<string> ReadTextAsync(string filePath)
        {
            var fileLock = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
            await fileLock.WaitAsync();
            try { return await File.ReadAllTextAsync(filePath); }
            finally { fileLock.Release(); }
        }

        /// <summary>
        /// Asynchronously appends a line of text to a specified file path.
        /// This method uses a <see cref="SemaphoreSlim"/> to ensure thread-safe file access for the given path.
        /// It creates the directory if it doesn't exist.
        /// </summary>
        /// <param name="filePath">The full path to the file to which text will be appended.</param>
        /// <param name="text">The text to append to the file.</param>
        /// <returns>A task representing the asynchronous append operation.</returns>
        private async Task AppendTextAsync(string filePath, string text) // Was WriteTextAsync, but AppendAllTextAsync is used.
        {
            var fileLock = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
            await fileLock.WaitAsync();
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (directory != null)
                {
                    Directory.CreateDirectory(directory); // Ensure directory exists
                }
                await File.AppendAllTextAsync(filePath, text);
            }
            finally { fileLock.Release(); }
        }

        /// <summary>
        /// Serializes an object to a JSON string and appends it as a new line to the specified file path.
        /// Uses a <see cref="JsonStringEnumConverter"/> for enum serialization.
        /// This method ensures thread-safe file access.
        /// </summary>
        /// <param name="filePath">The full path to the file to which the JSON line will be appended.</param>
        /// <param name="data">The object to serialize and append.</param>
        /// <returns>A task representing the asynchronous append operation.</returns>
        private async Task AppendJsonLineAsync(string filePath, object data)
        {
            // Using a new options instance here to ensure only JsonStringEnumConverter if others were added to _jsonOptions.
            var line = JsonSerializer.Serialize(data, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() }}) + Environment.NewLine;
            await AppendTextAsync(filePath, line);
        }
        #endregion
    }

    /// <summary>
    /// Provides extension methods for the <see cref="NodeTaskStatus"/> enum.
    /// </summary>
    /// <remarks>
    /// This class is currently defined within the JournalService.cs file.
    /// It should ideally be moved to its own file within a relevant project structure,
    /// such as `SiteKeeper.Shared/Enums/Extensions/NodeTaskStatusExtensions.cs` or similar,
    /// if it's intended for broader use or if other extensions for shared enums exist.
    /// </remarks>
    public static class NodeTaskStatusExtensions
    {
        /// <summary>
        /// Determines if the <see cref="NodeTaskStatus"/> represents a final, terminal state for a task
        /// (i.e., the task will not undergo further status changes).
        /// </summary>
        /// <param name="status">The node task status to check.</param>
        /// <returns><c>true</c> if the status is a terminal state (e.g., Succeeded, Failed, Cancelled); otherwise, <c>false</c>.</returns>
        public static bool IsTerminal(this NodeTaskStatus status)
        {
            switch (status)
            {
                // Early terminal states (before execution)
                case NodeTaskStatus.NotReadyForTask:
                case NodeTaskStatus.ReadinessCheckTimedOut:
                case NodeTaskStatus.DispatchFailed_Prepare:
                
                // Post-execution terminal states
                case NodeTaskStatus.Succeeded:
                case NodeTaskStatus.SucceededWithIssues:
                case NodeTaskStatus.Failed:
                case NodeTaskStatus.Cancelled:
                case NodeTaskStatus.CancellationFailed:
                case NodeTaskStatus.TaskDispatchFailed_Execute:
                case NodeTaskStatus.NodeOfflineDuringTask:
                case NodeTaskStatus.TimedOut:
                    return true;
                
                // Non-terminal states
                case NodeTaskStatus.Unknown:
                case NodeTaskStatus.Pending:
                case NodeTaskStatus.AwaitingReadiness:
                case NodeTaskStatus.ReadinessCheckSent:
                case NodeTaskStatus.ReadyToExecute:
                case NodeTaskStatus.TaskDispatched:
                case NodeTaskStatus.Starting:
                case NodeTaskStatus.InProgress:
                case NodeTaskStatus.Retrying:
                case NodeTaskStatus.Cancelling:
                default:
                    return false;
            }
        }
    }
}
