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
    /// Implements the IJournalService using a file-system-based approach. This service is the persistent memory
    /// of the SiteKeeper Master Agent, responsible for creating a detailed, durable record of every operation.
    /// It listens to events from the OperationCoordinatorService and records them to a structured directory format,
    /// enabling auditability, post-mortem analysis, and debugging. It also provides methods to query this journal.
    /// </summary>
    public class JournalService : IJournalService
    {
        private class ActiveJournalState
        {
            public string MasterActionId { get; set; }
            public string MasterActionJournalPath { get; set; }
            public int CurrentStageIndex { get; set; } = 0;
            public string CurrentStageName { get; set; } = "_init";
            public string CurrentStagePath => Path.Combine(MasterActionJournalPath, "stages", $"{CurrentStageIndex}-{SanitizeFileName(CurrentStageName)}");
        }
        
        /// <summary>
        /// A thread-safe dictionary to map an active OperationId to its unique, timestamped journal folder name.
        /// This avoids costly directory scans for ongoing operations and ensures consistency.
        /// Key: OperationId (string), Value: Journal Folder Name (string, e.g., "20250601...-op-abc...").
        /// </summary>
        private readonly ConcurrentDictionary<string, string> _operationJournalFolders = new();

        /// <summary>
        /// A thread-safe dictionary of semaphores to prevent race conditions when writing to the same journal file from multiple threads.
        /// Each file path gets its own semaphore to ensure writes are atomic without blocking writes to other files.
        /// Key: Full File Path (string), Value: SemaphoreSlim instance.
        /// </summary>
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();
        
        /// <summary>
        /// The root directory path where all journal entries for the current environment are stored.
        /// This is constructed from the MasterConfig's JournalRootPath and EnvironmentName.
        /// </summary>
        private readonly string _journalEnvRootPath;

        /// <summary>
        /// Standard logger for the JournalService. Used for logging the service's own activities, not for writing to the journal itself.
        /// </summary>
        private readonly ILogger<JournalService> _logger;
        
        /// <summary>
        /// Standard JSON serialization options used throughout the service for consistency.
        /// Ensures enums are written as strings and the output is indented for readability.
        /// </summary>
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() }};

        private readonly ConcurrentDictionary<string, ActiveJournalState> _activeJournals = new();
        private readonly ConcurrentDictionary<string, string> _pendingChanges = new();
        private readonly string _environmentRootPath;
        private readonly string _actionJournalRootPath;
        private readonly string _changeJournalRootPath;
        private readonly string _backupRepositoryRootPath;
        private readonly string _actionJournalIndexPath;
        private readonly string _changeJournalIndexPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="JournalService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance provided by dependency injection.</param>
        /// <param name="configOptions">The master configuration options, used to determine the journal's root path and environment name.</param>
        public JournalService(ILogger<JournalService> logger, IOptions<MasterConfig> configOptions)
        {
            _logger = logger;
            var config = configOptions.Value;
            _journalEnvRootPath = Path.Combine(config.JournalRootPath, SanitizeFolderName(config.EnvironmentName));
            _environmentRootPath = _journalEnvRootPath;
            _actionJournalRootPath = Path.Combine(_environmentRootPath, "ActionJournal");
            _changeJournalRootPath = Path.Combine(_environmentRootPath, "ChangeJournal");
            _backupRepositoryRootPath = Path.Combine(_environmentRootPath, "BackupRepository");
            _actionJournalIndexPath = Path.Combine(_environmentRootPath, "action_journal_index.log");
            _changeJournalIndexPath = Path.Combine(_environmentRootPath, "system_changes_index.log");
            Directory.CreateDirectory(_actionJournalRootPath);
            Directory.CreateDirectory(Path.Combine(_changeJournalRootPath, "artifacts"));
            Directory.CreateDirectory(_backupRepositoryRootPath);
        }

        /// <summary>
        /// Records the initiation of a new operation. This method creates the complete directory structure
        /// for the operation's journal and writes the initial 'info.json' file with the operation's metadata.
        /// </summary>
        /// <param name="operation">The in-memory Operation object that has just been created.</param>
        public async Task RecordOperationInitiatedAsync(Operation operation)
        {
            var journalFolderName = $"{operation.CreationTime:yyyyMMddHHmmssfff}-{operation.Id}";
            var opFolderPath = Path.Combine(_journalEnvRootPath, journalFolderName);

            if (_operationJournalFolders.TryAdd(operation.Id, journalFolderName))
            {
                try
                {
                    Directory.CreateDirectory(opFolderPath);
                    Directory.CreateDirectory(Path.Combine(opFolderPath, "logs"));
                    Directory.CreateDirectory(Path.Combine(opFolderPath, "results"));

                    var infoPath = Path.Combine(opFolderPath, "info.json");
                    await WriteJsonAsync(infoPath, operation);
                    _logger.LogInformation("Journal created for OperationId '{OperationId}' at '{OpFolderPath}'", operation.Id, opFolderPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create journal directory for OperationId '{OperationId}'", operation.Id);
                    _operationJournalFolders.TryRemove(operation.Id, out _);
                }
            }
        }
        
        /// <summary>
        /// Records the completion of an operation. This method updates the 'info.json' file with the final state
        /// of the operation and writes a separate 'overall_result.json' file containing a comprehensive summary.
        /// </summary>
        /// <param name="operation">The in-memory Operation object in its final terminal state.</param>
        public async Task RecordOperationCompletedAsync(Operation operation)
        {
            if (_operationJournalFolders.TryGetValue(operation.Id, out var journalFolderName))
            {
                var opFolderPath = Path.Combine(_journalEnvRootPath, journalFolderName);
                var infoPath = Path.Combine(opFolderPath, "info.json");
                await WriteJsonAsync(infoPath, operation); 

                var overallResultPath = Path.Combine(opFolderPath, "results", "overall_result.json");
                await WriteJsonAsync(overallResultPath, operation);

                _operationJournalFolders.TryRemove(operation.Id, out _);
            }
            else
            {
                _logger.LogWarning("Could not record operation completion: Journal folder not found for OperationId '{OperationId}'", operation.Id);
            }
        }

        /// <summary>
        /// Records an intermediate change in the overall status of an ongoing operation by updating its 'info.json' file.
        /// </summary>
        /// <param name="operation">The in-memory Operation object with its updated status.</param>
        public async Task RecordOperationStatusChangedAsync(Operation operation)
        {
            if (_operationJournalFolders.TryGetValue(operation.Id, out var journalFolderName))
            {
                var infoPath = Path.Combine(Path.Combine(_journalEnvRootPath, journalFolderName), "info.json");
                await WriteJsonAsync(infoPath, operation);
            }
        }
        
        /// <summary>
        /// Records the final result of an individual node task. When a task reaches a terminal state (e.g., Succeeded, Failed)
        /// and has a result payload, this method writes that payload to a unique JSON file in the 'results' subfolder.
        /// </summary>
        /// <param name="task">The NodeTask object in its terminal state.</param>
        public async Task RecordNodeTaskStatusChangedAsync(NodeTask task)
        {
            if (task.Status.IsTerminal() && task.ResultPayload != null)
            {
                if (_operationJournalFolders.TryGetValue(task.OperationId, out var journalFolderName))
                {
                    var resultFileName = $"{SanitizeFileName(task.NodeName)}-{SanitizeFileName(task.TaskId)}-result.json";
                    var resultPath = Path.Combine(_journalEnvRootPath, journalFolderName, "results", resultFileName);
                    await WriteJsonAsync(resultPath, task.ResultPayload);
                }
            }
        }

        /// <summary>
        /// Appends a single log entry, received from a slave, to the appropriate per-task log file.
        /// This method's signature now matches the IJournalService interface.
        /// </summary>
        /// <param name="logEntry">The log entry DTO received from the slave, which contains all necessary context.</param>
        public async Task AppendToNodeTaskLogAsync(SlaveTaskLogEntry logEntry)
        {
            if (!_operationJournalFolders.TryGetValue(logEntry.OperationId, out var journalFolderName))
            {
                _logger.LogWarning("Could not append to task log: Journal folder not found for OperationId '{OperationId}'", logEntry.OperationId);
                return;
            }

            var logFileName = $"{SanitizeFileName(logEntry.NodeName)}-{SanitizeFileName(logEntry.TaskId)}.log";
            var logFilePath = Path.Combine(_journalEnvRootPath, journalFolderName, "logs", logFileName);
            var logLine = $"{logEntry.TimestampUtc:yyyy-MM-dd HH:mm:ss.fff}Z [{logEntry.LogLevel}] {logEntry.LogMessage}{Environment.NewLine}";

            await WriteTextAsync(logFilePath, logLine);
        }

        /// <summary>
        /// Retrieves a paginated list of journal entry summaries. This implementation reads from the file system,
        /// parsing the 'info.json' from each operation directory to build the summary.
        /// </summary>
        /// <param name="startDate">Optional. Filters entries to be on or after this date.</param>
        /// <param name="endDate">Optional. Filters entries to be on or before this date.</param>
        /// <param name="operationType">Optional. Filters entries by the exact operation type string.</param>
        /// <param name="filterText">Optional. A text filter applied to the operation type for partial matches.</param>
        /// <param name="sortBy">Optional. Field to sort by. Currently supports 'Timestamp'.</param>
        /// <param name="sortOrder">Optional. Sort order ('asc' or 'desc'). Defaults to 'desc'.</param>
        /// <param name="pageNumber">The 1-indexed page number for pagination.</param>
        /// <param name="pageSize">The number of items per page.</param>
        /// <returns>A tuple containing the list of journal entry summaries and the total count of items matching the filter.</returns>
        public async Task<(IEnumerable<JournalEntrySummary> Items, int TotalCount)> ListJournalEntriesAsync(DateTime? startDate, DateTime? endDate, string? operationType, string? filterText, string? sortBy, string? sortOrder, int pageNumber, int pageSize)
        {
            var summaries = new List<JournalEntrySummary>();
            if (!Directory.Exists(_journalEnvRootPath))
            {
                _logger.LogWarning("Journal root directory not found at '{Path}'", _journalEnvRootPath);
                return (summaries, 0);
            }

            var operationDirs = Directory.GetDirectories(_journalEnvRootPath);

            foreach (var dir in operationDirs)
            {
                var infoPath = Path.Combine(dir, "info.json");
                if (File.Exists(infoPath))
                {
                    try
                    {
                        var opJson = await File.ReadAllTextAsync(infoPath);
                        var op = JsonSerializer.Deserialize<Operation>(opJson);
                        if (op != null)
                        {
                            summaries.Add(new JournalEntrySummary
                            {
                                JournalRecordId = Path.GetFileName(dir),
                                Timestamp = op.CreationTime,
                                OperationType = op.Type.ToString(),
                                Summary = op.Name ?? op.Type.ToString(),
                                Outcome = op.FinalOutcome?.ToString() ?? op.OverallStatus.ToString()
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to read or parse journal info.json from '{Directory}'", dir);
                    }
                }
            }

            IEnumerable<JournalEntrySummary> query = summaries;
            if (startDate.HasValue) query = query.Where(j => j.Timestamp >= startDate.Value);
            if (endDate.HasValue) query = query.Where(j => j.Timestamp <= endDate.Value);
            if (!string.IsNullOrWhiteSpace(operationType)) query = query.Where(j => j.OperationType.Equals(operationType, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(filterText)) query = query.Where(j => j.OperationType.Contains(filterText, StringComparison.OrdinalIgnoreCase));

            bool descending = sortOrder?.Equals("desc", StringComparison.OrdinalIgnoreCase) ?? true;
            query = descending ? query.OrderByDescending(j => j.Timestamp) : query.OrderBy(j => j.Timestamp);

            var totalCount = query.Count();
            var pagedItems = query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            
            return (pagedItems, totalCount);
        }

        
        #region Placeholder/Logging-Only Methods
        
        /// <summary>
        /// Logs a request to cancel an operation. This is an informational event. The actual status change
        /// is recorded via RecordOperationStatusChangedAsync.
        /// </summary>
        public Task RecordOperationCancellationRequestedAsync(Operation operation, string cancelledBy)
        {
             _logger.LogInformation("Journal Event: Operation Cancellation Requested. ID: {OperationId}, By: {CancelledBy}", operation.Id, cancelledBy);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Logs the creation of a node task. The task's results and logs will be recorded in separate files.
        /// </summary>
        public Task RecordNodeTaskCreatedAsync(NodeTask task)
        {
            _logger.LogInformation("Journal Event: Node Task Created. TaskID: {TaskId}, OperationID: {OperationId}", task.TaskId, task.OperationId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Logs a general event related to a specific agent. This does not write to an operation's journal.
        /// This signature is now fully qualified to resolve ambiguity.
        /// </summary>
        public Task RecordAgentEventAsync(string agentId, string message, SiteKeeper.Shared.Enums.LogLevel logLevel, Exception? exception = null, Dictionary<string, object>? details = null)
        {
            // Maps the shared enum to the logging provider's enum to avoid ambiguity at the call site.
            var msLogLevel = (Microsoft.Extensions.Logging.LogLevel)(int)logLevel;
            _logger.Log(msLogLevel, exception, "Journal Event: Agent Event for {AgentId}: {Message}. Details: {Details}", agentId, message, JsonSerializer.Serialize(details));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Logs a general system-wide event. This does not write to an operation's journal.
        /// This signature is now fully qualified to resolve ambiguity.
        /// </summary>
        public Task RecordSystemEventAsync(string eventName, string message, SiteKeeper.Shared.Enums.LogLevel logLevel, Dictionary<string, object>? details = null)
        {
            var msLogLevel = (Microsoft.Extensions.Logging.LogLevel)(int)logLevel;
            _logger.Log(msLogLevel, "Journal Event: System Event {EventName}: {Message}. Details: {Details}", eventName, message, JsonSerializer.Serialize(details));
            return Task.CompletedTask;
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
            // Folder name format: {timestamp}-{masterActionId}
            return Directory.GetDirectories(_actionJournalRootPath, $"*-{masterActionId}").FirstOrDefault();
        }

        /// <summary>
        /// Writes an object to a file as indented JSON, using a semaphore to prevent concurrent write conflicts.
        /// </summary>
        private async Task WriteJsonAsync(string filePath, object data)
        {
            var fileLock = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
            await fileLock.WaitAsync();
            try
            {
				// because of a highly async nature of the service, we ensure the directory exists before writing.
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
        
        /// <summary>
        /// Appends text to a file, using a semaphore to prevent concurrent write conflicts.
        /// </summary>
        private async Task WriteTextAsync(string filePath, string text)
        {
            var fileLock = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
            await fileLock.WaitAsync();
            try
            {
				// because of a highly async nature of the service, we ensure the directory exists before writing.
				var directory = Path.GetDirectoryName(filePath);
                if (directory != null)
                {
                    Directory.CreateDirectory(directory);
                }

                await File.AppendAllTextAsync(filePath, text);
            }
            finally
            {
                fileLock.Release();
            }
        }
        
        /// <summary>
        /// Removes invalid characters from a string to make it safe for use as a folder name.
        /// </summary>
        private static string SanitizeFolderName(string name) => string.Join("_", name.Split(Path.GetInvalidPathChars()));
        
        /// <summary>
        /// Removes invalid characters from a string to make it safe for use as a file name.
        /// </summary>
        private static string SanitizeFileName(string name) => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        #endregion

        /// <summary>
        /// Retrieves a historical/archived operation object from the journal by its ID.
        /// This is essential for querying the status of completed operations.
        /// </summary>
        /// <param name="operationId">The ID of the operation to retrieve.</param>
        /// <returns>The deserialized <see cref="Operation"/> object if found in the journal; otherwise, null.</returns>
        public async Task<Operation?> GetArchivedOperationAsync(string operationId)
        {
            if (!Directory.Exists(_journalEnvRootPath))
            {
                _logger.LogWarning("Cannot get archived operation. Journal root directory not found at '{Path}'", _journalEnvRootPath);
                return null;
            }

            // Find the directory that corresponds to the operationId.
            // The folder name is expected to be in the format: {timestamp}-{operationId}
            var operationDir = Directory.GetDirectories(_journalEnvRootPath, $"*-{operationId}").FirstOrDefault();

            if (operationDir == null)
            {
                _logger.LogInformation("No journal directory found for archived operationId '{OperationId}'", operationId);
                return null;
            }

            var infoPath = Path.Combine(operationDir, "info.json");
            if (!File.Exists(infoPath))
            {
                _logger.LogWarning("Journal directory found for '{OperationId}', but info.json is missing at '{Path}'", operationId, infoPath);
                return null;
            }

            try
            {
                var opJson = await File.ReadAllTextAsync(infoPath);
                // The deserializer needs to handle the Operation object correctly.
                var op = JsonSerializer.Deserialize<Operation>(opJson, _jsonOptions);
                return op;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read or parse journal info.json for OperationId '{OperationId}' from '{Path}'", operationId, infoPath);
                return null;
            }
        }

        /// <summary>
        /// Retrieves the full details for a specific journal entry by its operation's unique identifier.
        /// This implementation scans the journal directory to find the matching operation folder.
        /// </summary>
        public async Task<JournalEntry?> GetJournalEntryDetailsByOperationIdAsync(string operationId)
        {
            if (!Directory.Exists(_journalEnvRootPath))
            {
                _logger.LogWarning("Cannot get journal entry by op ID. Journal root directory not found at '{Path}'", _journalEnvRootPath);
                return null;
            }

            // Find the directory that corresponds to the operationId. The folder name format is "{timestamp}-{operationId}".
            var journalDir = Directory.GetDirectories(_journalEnvRootPath, $"*-{operationId}").FirstOrDefault();

            if (journalDir == null)
            {
                _logger.LogInformation("No journal directory found for operationId '{OperationId}'", operationId);
                return null;
            }

            // Once we have the directory name (which is the journalRecordId), we can call the existing method.
            var journalRecordId = Path.GetFileName(journalDir);
            return await GetJournalEntryDetailsAsync(journalRecordId);
        }

        /// <summary>
        /// Records the final, aggregate result payload of a completed Master Action.
        /// </summary>
        /// <param name="action">The completed MasterAction containing the final payload.</param>
        public async Task RecordMasterActionResultAsync(MasterAction action)
        {
            // We use the MasterAction's ID, which is the OperationId for journaling purposes.
            if (_operationJournalFolders.TryGetValue(action.Id, out var journalFolderName))
            {
                if (action.FinalResultPayload != null)
                {
                    var resultPath = Path.Combine(_journalEnvRootPath, journalFolderName, "results", "master_action_result.json");
                    await WriteJsonAsync(resultPath, action.FinalResultPayload);
                    _logger.LogInformation("Successfully journaled final result for MasterAction {MasterActionId}", action.Id);
                }
            }
            else
            {
                _logger.LogWarning("Could not record master action result: Journal folder not found for MasterActionId '{MasterActionId}'", action.Id);
            }
        }

        #region Action Journal Methods
        public async Task RecordMasterActionInitiatedAsync(MasterAction action)
        {
            var journalFolderName = $"{action.StartTime:yyyyMMddHHmmssfff}-{action.Id}";
            var actionJournalPath = Path.Combine(_actionJournalRootPath, journalFolderName);
            var state = new ActiveJournalState { MasterActionId = action.Id, MasterActionJournalPath = actionJournalPath };
            if (!_activeJournals.TryAdd(action.Id, state)) return;
            
            try
            {
                Directory.CreateDirectory(actionJournalPath);

                // create the initial stage directory structure immediately.
                var initialStagePath = state.CurrentStagePath; // This correctly resolves to ".../stages/0-_init"
                Directory.CreateDirectory(initialStagePath);
                Directory.CreateDirectory(Path.Combine(initialStagePath, "logs"));
                Directory.CreateDirectory(Path.Combine(initialStagePath, "results"));

                Directory.CreateDirectory(Path.Combine(actionJournalPath, "stages"));
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

        public async Task RecordMasterActionCompletedAsync(MasterAction action)
        {
            if (_activeJournals.TryGetValue(action.Id, out var state))
            {
                var infoPath = Path.Combine(state.MasterActionJournalPath, "master_action_info.json");
                await WriteJsonAsync(infoPath, action);
                _activeJournals.TryRemove(action.Id, out _);
                _logger.LogInformation("Action Journal finalized for MasterActionId '{Id}'", action.Id);
            }
        }

        public async Task RecordStageInitiatedAsync(MasterActionContext context, string stageName, object? stageInput)
        {
            if (!_activeJournals.TryGetValue(context.MasterActionId, out var state)) return;
            state.CurrentStageIndex++;
            state.CurrentStageName = stageName;
            Directory.CreateDirectory(state.CurrentStagePath);
            Directory.CreateDirectory(Path.Combine(state.CurrentStagePath, "logs"));
            Directory.CreateDirectory(Path.Combine(state.CurrentStagePath, "results"));
            var stageInfo = new { StageName = stageName, StageIndex = state.CurrentStageIndex, StartTime = DateTime.UtcNow, Input = stageInput };
            await WriteJsonAsync(Path.Combine(state.CurrentStagePath, "stage_info.json"), stageInfo);
        }

        public async Task RecordStageCompletedAsync(MasterActionContext context, string stageName, object? stageResult)
        {
            if (!_activeJournals.TryGetValue(context.MasterActionId, out var state)) return;
            var stageInfoPath = Path.Combine(state.CurrentStagePath, "stage_info.json");
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
                var resultPath = Path.Combine(state.CurrentStagePath, "results", "stage_result.json");
                await WriteJsonAsync(resultPath, stageResult);
            }
        }

        /// <summary>
        /// Appends a log entry to the currently active stage's log file for a given master action.
        /// </summary>
        /// <param name="masterActionId">The ID of the master action to which the log belongs.</param>
        /// <param name="logEntry">The log entry DTO containing the message and source node.</param>
        public async Task AppendToStageLogAsync(string masterActionId, SlaveTaskLogEntry logEntry)
        {
            _logger.LogDebug("JOURNAL-SERVICE: AppendToStageLogAsync called for MasterActionId '{MasterActionId}', Node: '{NodeName}'", masterActionId, logEntry.NodeName);

            // Find the active journal state using the masterActionId.
            if (!_activeJournals.TryGetValue(masterActionId, out var state))
            {
                _logger.LogWarning("Cannot append to log: No active journal found for MasterActionId '{Id}'", masterActionId);
                return;
            }

            var logFileName = $"{SanitizeFileName(logEntry.NodeName)}.log";
            var logFilePath = Path.Combine(state.CurrentStagePath, "logs", logFileName);

            _logger.LogDebug("JOURNAL-SERVICE: Preparing to write log to file: '{LogFilePath}'", logFilePath);

            var logLine = $"{logEntry.TimestampUtc:yyyy-MM-dd HH:mm:ss.fff}Z [{logEntry.LogLevel}] {logEntry.LogMessage}{Environment.NewLine}";
            await AppendTextAsync(logFilePath, logLine);
        }

        public async Task RecordNodeTaskResultAsync(MasterActionContext context, NodeTask task)
        {
            if (!_activeJournals.TryGetValue(context.MasterActionId, out var state) || task.ResultPayload == null) return;
            var resultFileName = $"{SanitizeFileName(task.NodeName)}-result.json";
            var resultPath = Path.Combine(state.CurrentStagePath, "results", resultFileName);
            await WriteJsonAsync(resultPath, task.ResultPayload);
        }

        #endregion

        #region Change Journal Methods
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

        #region Query Methods
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
                    // Simple filtering for summary
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

            // In a real implementation, add full filtering logic here based on queryParams
            var filtered = summaries.OrderByDescending(s => s.Timestamp).ToList();
            var totalCount = filtered.Count;
			var page = queryParams.Page ?? 1; // Default to 1 if null
			var pageSize = queryParams.PageSize ?? 10; // Default to 10 if null
			var pagedItems = filtered.Skip( (page - 1) * pageSize ).Take( pageSize ).ToList();
            return (pagedItems, totalCount);
        }

        public async Task<JournalEntry?> GetJournalEntryDetailsAsync(string masterActionId)
        {
            // Find the correct folder from the action_journal_index.log
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
            
            // In a full implementation, you would recursively scan the stages folder
            // and aggregate all logs and results into the response DTO.
            return new JournalEntry
            {
                JournalRecordId = masterAction.Id,
                Timestamp = masterAction.StartTime,
                Summary = masterAction.Name,
                Outcome = masterAction.OverallStatus.ToString(),
                Details = masterAction
            };
        }

        /// <summary>
        /// Retrieves a historical/archived MasterAction object from the journal by its ID.
        /// </summary>
        public async Task<MasterAction?> GetArchivedMasterActionAsync(string masterActionId)
        {
            // Find the directory that corresponds to the masterActionId.
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

        public async Task<SystemChangeRecord?> GetLastSuccessfulChangeOfTypeAsync(ChangeEventType type)
        {
            if (!File.Exists(_changeJournalIndexPath)) return null;

            // Read lines in reverse to find the last one efficiently
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
            try { await File.AppendAllTextAsync(filePath, text); }
            finally { fileLock.Release(); }
        }

        private async Task AppendJsonLineAsync(string filePath, object data)
        {
            var line = JsonSerializer.Serialize(data, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() }}) + Environment.NewLine;
            await AppendTextAsync(filePath, line);
        }
        #endregion
    }

    /// <summary>
    /// Provides extension methods for task status enums.
    /// This should ideally be in its own file within the project structure (e.g., SiteKeeper.Shared\Enums\Extensions).
    /// </summary>
    public static class NodeTaskStatusExtensions
    {
        /// <summary>
        /// Determines if the task status represents a final, terminal state.
        /// </summary>
        /// <param name="status">The node task status.</param>
        /// <returns><c>true</c> if the status is a terminal state; otherwise, <c>false</c>.</returns>
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
