using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Abstractions.Services.Journaling;
using SiteKeeper.Master.Abstractions.Workflow;
using SiteKeeper.Master.Model.InternalData;
using SiteKeeper.Master.Web.Apis.QueryParameters;
using SiteKeeper.Shared.DTOs.AgentHub;
using SiteKeeper.Shared.DTOs.API.Environment;
using SiteKeeper.Shared.DTOs.API.Journal;
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
    /// Implements the IJournal service using a file-system-based approach. This service is the persistent memory
    /// of the SiteKeeper Master Agent, responsible for creating a detailed, durable record of every operation.
    /// It is designed as a stateless, thread-safe singleton that relies on explicit context from its callers,
    /// rather than tracking the "current" state of a workflow, to robustly handle asynchronous operations.
    /// </summary>
    public class Journal : IJournal
    {
        /// <summary>
        /// A simplified, stateless context object for an active master action. It only holds the root
        /// journal path, removing the previous responsibility of tracking the "current stage" to prevent race conditions.
        /// </summary>
        private class ActiveJournalState
        {
            public string MasterActionId { get; set; }
            public string MasterActionJournalPath { get; set; }
        }

        /// <summary>
        /// A thread-safe dictionary of semaphores to prevent race conditions when writing to the same journal file from multiple threads.
        /// Each file path gets its own semaphore to ensure writes are atomic without blocking writes to other files.
        /// </summary>
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();

        /// <summary>
        /// A thread-safe mapping of a NodeAction ID to the full file path of its parent stage directory.
        /// This is the key to routing late-arriving logs correctly in a stateless manner.
        /// Key: nodeActionId (string), Value: Full path to stage directory (string).
        /// </summary>
        private readonly ConcurrentDictionary<string, string> _nodeActionToStagePathMap = new();

        /// <summary>
        /// A thread-safe reverse mapping to track all NodeAction IDs created for a given MasterAction ID.
        /// This makes the cleanup process highly efficient upon workflow completion.
        /// Key: masterActionId (string), Value: A thread-safe bag of associated nodeActionIds.
        /// </summary>
        private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _masterToNodeActionMap = new();

        private readonly string _journalEnvRootPath;
        private readonly ILogger<Journal> _logger;
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
        private readonly ConcurrentDictionary<string, ActiveJournalState> _activeJournals = new();
        private readonly ConcurrentDictionary<string, string> _pendingChanges = new();
        private readonly string _actionJournalRootPath;
        private readonly string _changeJournalRootPath;
        private readonly string _backupRepositoryRootPath;
        private readonly string _actionJournalIndexPath;
        private readonly string _changeJournalIndexPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="Journal"/> class.
        /// It constructs the necessary paths for the journal system based on configuration
        /// and ensures the directory structure exists upon service instantiation.
        /// </summary>
        /// <param name="logger">The logger instance provided by dependency injection.</param>
        /// <param name="configOptions">The master configuration options, used to determine the journal's root path and environment name.</param>
        public Journal(ILogger<Journal> logger, IOptions<MasterConfig> configOptions)
        {
            _logger = logger;
            var config = configOptions.Value;

            // Construct all root paths for the journaling system.
            _journalEnvRootPath = Path.Combine(config.JournalRootPath, SanitizeFolderName(config.EnvironmentName));
            _actionJournalRootPath = Path.Combine(_journalEnvRootPath, "ActionJournal");
            _changeJournalRootPath = Path.Combine(_journalEnvRootPath, "ChangeJournal");
            _backupRepositoryRootPath = Path.Combine(_journalEnvRootPath, "BackupRepository");
            _actionJournalIndexPath = Path.Combine(_journalEnvRootPath, "action_journal_index.log");
            _changeJournalIndexPath = Path.Combine(_journalEnvRootPath, "system_changes_index.log");

            // Ensure all required directories exist on startup.
            Directory.CreateDirectory(_actionJournalRootPath);
            Directory.CreateDirectory(Path.Combine(_changeJournalRootPath, "artifacts"));
            Directory.CreateDirectory(_backupRepositoryRootPath);
        }

        #region Change Journal Methods
        
        /// <inheritdoc />
        public async Task<StateChangeCreationResult> InitiateStateChangeAsync(StateChangeInfo changeInfo)
        {
            var changeId = $"chg-{Guid.NewGuid():N}";
            _pendingChanges[changeId] = changeInfo.SourceMasterActionId;
            var record = new SystemChangeRecord { Timestamp = DateTime.UtcNow, ChangeId = changeId, EventType = $"{changeInfo.Type}Initiated", Description = changeInfo.Description, SourceMasterActionId = changeInfo.SourceMasterActionId };
            await AppendJsonLineAsync(_changeJournalIndexPath, record);
            _logger.LogInformation("Initiated Change Journal record {Id} for MasterAction {MasterActionId}", changeId, changeInfo.SourceMasterActionId);

            string? backupPath = null;
            if (changeInfo.Type == ChangeEventType.Backup)
            {
                var backupFolderName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-backup-{changeId}";
                backupPath = Path.Combine(_backupRepositoryRootPath, backupFolderName);
                Directory.CreateDirectory(backupPath);
            }

            return new StateChangeCreationResult { ChangeId = changeId, BackupArtifactPath = backupPath };
        }

        /// <inheritdoc />
        public async Task FinalizeStateChangeAsync(StateChangeFinalizationInfo finalizationInfo)
        {
            if (!_pendingChanges.TryRemove(finalizationInfo.ChangeId, out var sourceMasterActionId))
            {
                _logger.LogWarning("Could not finalize change record {Id}: Not found in pending changes.", finalizationInfo.ChangeId);
                return;
            }

            var artifactSubfolder = Path.Combine(_changeJournalRootPath, "artifacts", finalizationInfo.ChangeId);
            Directory.CreateDirectory(artifactSubfolder);

            var artifactPath = Path.Combine(artifactSubfolder, "result_artifact.json");
            await WriteJsonAsync(artifactPath, finalizationInfo.ResultArtifact);

            var record = new SystemChangeRecord { Timestamp = DateTime.UtcNow, ChangeId = finalizationInfo.ChangeId, EventType = $"{finalizationInfo.Outcome}", Outcome = finalizationInfo.Outcome.ToString(), Description = finalizationInfo.Description, ArtifactPath = artifactPath, SourceMasterActionId = sourceMasterActionId };
            await AppendJsonLineAsync(_changeJournalIndexPath, record);
            _logger.LogInformation("Finalized Change Journal record {Id}", finalizationInfo.ChangeId);
        }
        #endregion

        #region Action Journal Methods

        /// <inheritdoc />
        public async Task RecordMasterActionInitiatedAsync(MasterAction action)
        {
            var journalFolderName = $"{action.StartTime:yyyyMMddHHmmssfff}-{action.Id}";
            var actionJournalPath = Path.Combine(_actionJournalRootPath, journalFolderName);
            var state = new ActiveJournalState { MasterActionId = action.Id, MasterActionJournalPath = actionJournalPath };
            if (!_activeJournals.TryAdd(action.Id, state)) return;

            try
            {
                // Create the root directory for this specific master action's journal.
                Directory.CreateDirectory(actionJournalPath);
                Directory.CreateDirectory(Path.Combine(actionJournalPath, "stages"));

                // Write the main information file and the index entry.
                var infoPath = Path.Combine(actionJournalPath, "master_action_info.json");
                await WriteJsonAsync(infoPath, action);
                var indexEntry = new { Timestamp = action.StartTime, action.Id, ActionType = action.Type.ToString(), action.InitiatedBy, JournalPath = journalFolderName };
                await AppendJsonLineAsync(_actionJournalIndexPath, indexEntry);
                _logger.LogInformation("Action Journal created for MasterActionId '{Id}' at '{Path}'", action.Id, actionJournalPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Action Journal directory for MasterActionId '{Id}'", action.Id);
                _activeJournals.TryRemove(action.Id, out _);
            }
        }

        /// <inheritdoc />
        public async Task RecordMasterActionCompletedAsync(MasterAction action)
        {
            // Find the active journal state for the master action.
            if (_activeJournals.TryGetValue(action.Id, out var state))
            {
                // Overwrite the main info file with the final, completed state of the action.
                var infoPath = Path.Combine(state.MasterActionJournalPath, "master_action_info.json");
                await WriteJsonAsync(infoPath, action);

                // Remove the action from the active journals dictionary.
                _activeJournals.TryRemove(action.Id, out _);
                _logger.LogInformation("Action Journal finalized for MasterActionId '{Id}'", action.Id);
            }
        }

        /// <inheritdoc />
        public async Task RecordStageInitiatedAsync(string masterActionId, int stageIndex, string stageName, object? stageInput)
        {
            if (!_activeJournals.TryGetValue(masterActionId, out var state)) return;

            // Construct the path using the explicit index and name passed from the caller.
            // The brittle directory-counting logic is now gone.
            var stagesRoot = Path.Combine(state.MasterActionJournalPath, "stages");
            var stagePath = Path.Combine(stagesRoot, $"{stageIndex}-{SanitizeFileName(stageName)}");
            
            // Create the directory structure for the new stage.
            Directory.CreateDirectory(stagePath);
            Directory.CreateDirectory(Path.Combine(stagePath, "logs"));
            Directory.CreateDirectory(Path.Combine(stagePath, "results"));

            // Write the initial metadata for the stage, using the explicit index.
            var stageInfo = new { StageName = stageName, StageIndex = stageIndex, StartTime = DateTime.UtcNow, Input = stageInput };
            await WriteJsonAsync(Path.Combine(stagePath, "stage_info.json"), stageInfo);
        }
        
        /// <inheritdoc />
        public async Task RecordStageCompletedAsync(string masterActionId, int stageIndex, string stageName, object? stageResult)
        {
            if (!_activeJournals.TryGetValue(masterActionId, out var state)) return;

            // Construct the path to the specific stage directory using the explicit index and name.
            var stagePath = Path.Combine(state.MasterActionJournalPath, "stages", $"{stageIndex}-{SanitizeFileName(stageName)}");
            var stageInfoPath = Path.Combine(stagePath, "stage_info.json");

            // Update the stage's info file with an end time.
            if (File.Exists(stageInfoPath))
            {
                var stageInfoJson = await ReadTextAsync(stageInfoPath);
                var stageInfoNode = System.Text.Json.Nodes.JsonNode.Parse(stageInfoJson);
                if (stageInfoNode != null)
                {
                    stageInfoNode["EndTime"] = DateTime.UtcNow;
                    await WriteJsonAsync(stageInfoPath, stageInfoNode);
                }
            }
            if (stageResult != null)
            {
                var resultPath = Path.Combine(stagePath, "results", "stage_result.json");
                await WriteJsonAsync(resultPath, stageResult);
            }
        }

        /// <inheritdoc />
        public async Task AppendSlaveLogToStageAsync(string masterActionId, SlaveTaskLogEntry logEntry)
        {
            var nodeActionId = logEntry.ActionId;
            _logger.LogDebug("JOURNAL-SERVICE: AppendToStageLogAsync called for NodeActionId '{NodeActionId}', Node: '{NodeName}'", nodeActionId, logEntry.NodeName);

            // The core of the stateless logging fix:
            // Use the nodeActionId from the log entry to look up the correct historical stage path.
            if (_nodeActionToStagePathMap.TryGetValue(nodeActionId, out var stagePath))
            {
                var logFileName = $"{SanitizeFileName(logEntry.NodeName)}.log";
                var logFilePath = Path.Combine(stagePath, "logs", logFileName);

                _logger.LogDebug("JOURNAL-SERVICE: Found mapping. Writing log to historical stage path: '{LogFilePath}'", logFilePath);

                var logLine = $"{logEntry.TimestampUtc:yyyy-MM-dd HH:mm:ss.fff}Z [{logEntry.LogLevel}] {logEntry.LogMessage}{Environment.NewLine}";
                await AppendTextAsync(logFilePath, logLine);
            }
            else
            {
                // This case handles logs that arrive for an unknown or already cleaned-up action.
                _logger.LogWarning("Cannot append log: No stage path mapping found for NodeActionId '{NodeActionId}'. Log from '{NodeName}' will be dropped. Message: {Message}",
                    nodeActionId, logEntry.NodeName, logEntry.LogMessage);
            }
        }

        public async Task AppendMasterLogToStageAsync(string masterActionId, int? stageIndex, string? stageName, SlaveTaskLogEntry logEntry)
        {
            if (!_activeJournals.TryGetValue(masterActionId, out var state))
            {
                _logger.LogWarning("Cannot append master log: No active journal found for MasterActionId '{MasterActionId}'.", masterActionId);
                return;
            }

            string stageFolderName;
            // Use the logical information passed from the MDLC.
            if (stageIndex.HasValue && !string.IsNullOrEmpty(stageName))
            {
                stageFolderName = $"{stageIndex.Value}-{SanitizeFileName(stageName)}";
            }
            else
            {
                // This case should ideally not be hit with the new MasterActionContext logic,
                // but it provides a safe fallback.
                stageFolderName = "misc-master-logs";
            }

            var stagePath = Path.Combine(state.MasterActionJournalPath, "stages", stageFolderName);
            var logFilePath = Path.Combine(stagePath, "logs", "_master.log");

            _logger.LogDebug("JOURNAL-SERVICE: Writing master log to stage path: '{LogFilePath}'", logFilePath);

            var logLine = $"{logEntry.TimestampUtc:yyyy-MM-dd HH:mm:ss.fff}Z [{logEntry.LogLevel}] {logEntry.LogMessage}{Environment.NewLine}";
            await AppendTextAsync(logFilePath, logLine);
        }

        /// <inheritdoc />
        public async Task RecordNodeTaskResultAsync(NodeTask task)
        {
            if (task.ResultPayload == null) return;
            
            // The task.ActionId is the nodeActionId, which is the key for the mapping.
            if (_nodeActionToStagePathMap.TryGetValue(task.ActionId, out var stagePath))
            {
                var resultFileName = $"{SanitizeFileName(task.NodeName)}-result.json";
                var resultPath = Path.Combine(stagePath, "results", resultFileName);
                await WriteJsonAsync(resultPath, task.ResultPayload);
            }
        }

        /// <inheritdoc />
        public async Task MapNodeActionToStageAsync(string masterActionId, int stageIndex, string stageName, string nodeActionId)
        {
            if (!_activeJournals.TryGetValue(masterActionId, out var state))
            {
                _logger.LogWarning("Cannot map NodeAction to Stage: No active journal found for MasterActionId '{Id}'", masterActionId);
                return;
            }

            // Create the full path to the stage directory.
            var stagePath = Path.Combine(state.MasterActionJournalPath, "stages", $"{stageIndex}-{SanitizeFileName(stageName)}");
            
            // This is an atomic operation on a ConcurrentDictionary, ensuring thread safety.
            // It creates the primary mapping used for routing logs and results.
            _nodeActionToStagePathMap[nodeActionId] = stagePath;
            
            // This populates the reverse-lookup map, which is used for efficient cleanup.
            var nodeActionBag = _masterToNodeActionMap.GetOrAdd(masterActionId, _ => new ConcurrentBag<string>());
            nodeActionBag.Add(nodeActionId);

            _logger.LogDebug("Journal mapped NodeActionId '{NodeActionId}' to stage path '{StagePath}'", nodeActionId, stagePath);

            // Keep async signature to conform to the interface definition.
            await Task.CompletedTask;
        }

        /// <inheritdoc />
        public void ClearJournalMappings(string masterActionId)
        {
            _logger.LogInformation("Clearing journal mappings for completed MasterActionId: {MasterActionId}", masterActionId);
            
            // Efficiently retrieve all nodeActionIds associated with the completed masterActionId.
            if (_masterToNodeActionMap.TryRemove(masterActionId, out var nodeActionIds))
            {
                // Iterate through the retrieved IDs and remove each from the primary mapping dictionary.
                foreach (var nodeActionId in nodeActionIds)
                {
                    _nodeActionToStagePathMap.TryRemove(nodeActionId, out _);
                }
                _logger.LogDebug("Cleared {Count} node action mappings for MasterActionId: {MasterActionId}", nodeActionIds.Count, masterActionId);
            }
        }
        #endregion

        #region Query Methods
        
        /// <inheritdoc />
        public async Task<(IEnumerable<JournalEntrySummary> Items, int TotalCount)> ListJournalEntriesAsync(JournalQueryParameters queryParams)
        {
            var summaries = new List<JournalEntrySummary>();
            if (!File.Exists(_changeJournalIndexPath)) return (summaries, 0);

            var lines = await File.ReadAllLinesAsync(_changeJournalIndexPath);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var record = JsonSerializer.Deserialize<SystemChangeRecord>(line);
                    if(record == null) continue;
                    summaries.Add(new JournalEntrySummary
                    {
                        JournalRecordId = record.ChangeId,
                        Timestamp = record.Timestamp,
                        OperationType = record.EventType,
                        Summary = record.Description,
                        Outcome = record.Outcome
                    });
                }
                catch (Exception ex) { _logger.LogError(ex, "Failed to parse line in change journal index: {Line}", line); }
            }

            var filtered = summaries.OrderByDescending(s => s.Timestamp).ToList();
            var totalCount = filtered.Count;
			var page = queryParams.Page ?? 1;
			var pageSize = queryParams.PageSize ?? 10;
			var pagedItems = filtered.Skip( (page - 1) * pageSize ).Take( pageSize ).ToList();
            return (pagedItems, totalCount);
        }

        /// <inheritdoc />
        public async Task<JournalEntry?> GetJournalEntryDetailsAsync(string masterActionId)
        {
            string? actionFolder = null;
            if (File.Exists(_actionJournalIndexPath))
            {
                var lines = await File.ReadAllLinesAsync(_actionJournalIndexPath);
                foreach (var line in lines)
                {
                    if (line.Contains(masterActionId))
                    {
                        var indexNode = System.Text.Json.Nodes.JsonNode.Parse(line);
                        actionFolder = indexNode?["JournalPath"]?.GetValue<string>();
                        break;
                    }
                }
            }
            if (actionFolder == null) return null;

            var actionJournalPath = Path.Combine(_actionJournalRootPath, actionFolder);
            var infoPath = Path.Combine(actionJournalPath, "master_action_info.json");
            if (!File.Exists(infoPath)) return null;
            
            var opJson = await ReadTextAsync(infoPath);
            var masterAction = JsonSerializer.Deserialize<MasterAction>(opJson);
            if (masterAction == null) return null;
            
            return new JournalEntry
            {
                JournalRecordId = masterAction.Id,
                Timestamp = masterAction.StartTime,
                Summary = masterAction.Name,
                Outcome = masterAction.OverallStatus.ToString(),
                Details = masterAction
            };
        }

        /// <inheritdoc />
        public async Task<MasterAction?> GetArchivedMasterActionAsync(string masterActionId)
        {
            var actionJournalDir = FindActionJournalDirectoryPath(masterActionId);
            if (actionJournalDir == null)
            {
                _logger.LogInformation("No journal directory found for archived MasterActionId '{Id}'", masterActionId);
                return null;
            }

            var infoPath = Path.Combine(actionJournalDir, "master_action_info.json");
            if (!File.Exists(infoPath))
            {
                _logger.LogWarning("Journal directory found for '{Id}', but master_action_info.json is missing.", masterActionId);
                return null;
            }

            try
            {
                var opJson = await ReadTextAsync(infoPath);
                return JsonSerializer.Deserialize<MasterAction>(opJson, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read or parse master_action_info.json for MasterActionId '{Id}'", masterActionId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<SystemChangeRecord?> GetLastSuccessfulChangeOfTypeAsync(ChangeEventType type)
        {
            if (!File.Exists(_changeJournalIndexPath)) return null;
            
            var lines = await File.ReadAllLinesAsync(_changeJournalIndexPath);
            foreach (var line in lines.Reverse())
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var record = JsonSerializer.Deserialize<SystemChangeRecord>(line);
                    if (record?.Outcome == "Success" && record.EventType.StartsWith(type.ToString()))
                    {
                        return record;
                    }
                }
                catch { continue; }
            }
            return null;
        }
        #endregion

        #region Private Helpers
        private string? FindActionJournalDirectoryPath(string masterActionId)
        {
            if (!Directory.Exists(_actionJournalRootPath))
            {
                _logger.LogWarning("Cannot search for journal. Action Journal root directory not found at '{Path}'", _actionJournalRootPath);
                return null;
            }
            return Directory.GetDirectories(_actionJournalRootPath, $"*-{masterActionId}").FirstOrDefault();
        }

        private async Task WriteJsonAsync(string filePath, object data)
        {
            var fileLock = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
            await fileLock.WaitAsync();
            try
            {
				var directory = Path.GetDirectoryName(filePath);
                if (directory != null)
                {
                    Directory.CreateDirectory(directory);
                }
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);
            }
            finally
            {
                fileLock.Release();
            }
        }

        private async Task<string> ReadTextAsync(string filePath)
        {
            var fileLock = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
            await fileLock.WaitAsync();
            try { return await File.ReadAllTextAsync(filePath); }
            finally { fileLock.Release(); }
        }

        private async Task AppendTextAsync(string filePath, string text)
        {
            var fileLock = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
            await fileLock.WaitAsync();
            try
            {
                // directory creation logic for robustness.
                var directory = Path.GetDirectoryName(filePath);
                if (directory != null)
                {
                    Directory.CreateDirectory(directory);
                }
                await File.AppendAllTextAsync(filePath, text);
            }
            finally { fileLock.Release(); }
        }

        private async Task AppendJsonLineAsync(string filePath, object data)
        {
            var line = JsonSerializer.Serialize(data, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() }}) + Environment.NewLine;

            // directory creation logic for robustness.
            var directory = Path.GetDirectoryName(filePath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            await AppendTextAsync(filePath, line);
        }
        
        private static string SanitizeFolderName(string name) => string.Join("_", name.Split(Path.GetInvalidPathChars()));
        
        private static string SanitizeFileName(string name) => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        #endregion

        public async Task<T?> GetArchivedStageResultAsync<T>(string masterActionId, int stageIndex) where T : class
        {
            var stageDir = FindStageDirectoryPath(masterActionId, stageIndex);
            if (stageDir == null) return null;

            var resultPath = Path.Combine(stageDir, "results", "stage_result.json");
            if (!File.Exists(resultPath)) return null;

            try
            {
                var json = await ReadTextAsync(resultPath);
                // Note: The result file contains a composite object. We extract the relevant part.
                var jsonNode = JsonDocument.Parse(json).RootElement;
                if (jsonNode.TryGetProperty("NodeActionResults", out var resultsElement))
                {
                    return resultsElement.Deserialize<T>(_jsonOptions);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read or parse stage_result.json for Op:{opId}, Stage:{stageIdx}", masterActionId, stageIndex);
                return null;
            }
        }

        public async Task<string?> GetArchivedStageLogContentAsync(string masterActionId, int stageIndex, string logFileName)
        {
            var stageDir = FindStageDirectoryPath(masterActionId, stageIndex);
            if (stageDir == null) return null;

            var logFilePath = Path.Combine(stageDir, "logs", SanitizeFileName(logFileName));
            if (!File.Exists(logFilePath)) return null;

            return await ReadTextAsync(logFilePath);
        }

        // New helper to find a stage directory by index
        private string? FindStageDirectoryPath(string masterActionId, int stageIndex)
        {
            var actionJournalDir = FindActionJournalDirectoryPath(masterActionId);
            if (actionJournalDir == null) return null;

            var stagesDir = Path.Combine(actionJournalDir, "stages");
            if (!Directory.Exists(stagesDir)) return null;
            
            // The directory name starts with the index, e.g., "1-MultiNodeTestStage"
            return Directory.GetDirectories(stagesDir, $"{stageIndex}-*").FirstOrDefault();
        }

    }
}
