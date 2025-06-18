using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Model.InternalData;
using SiteKeeper.Master.Workflow.DTOs;
using SiteKeeper.Shared.DTOs.AgentHub;
using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Workflow
{
    /// <summary>
    /// A stateful object that carries information and provides services throughout the
    /// lifecycle of a single Master Action. An instance of this class is created for
    /// each workflow run and passed to every stage handler. It acts as the primary
    /// bridge between the running workflow logic and the master state object.
    /// </summary>
    public class MasterActionContext
    {
        #region Private Fields
        
        /// <summary>
        /// A direct reference to the MasterAction state object that this context manages.
        /// All state-mutating methods in this context will operate on this object.
        /// </summary>
        private readonly MasterAction _masterAction;
        private readonly IJournalService _journalService;
        private string _currentStageName = "_init"; // Track the current stage name

        /// <summary>
        /// A function pointer to the log flushing mechanism of the UILoggingTarget.
        /// This allows the workflow to request a log sync without being directly coupled to NLog.
        /// </summary>
        private readonly Func<Task> _logFlushProvider;

        // Fields for managing logical step-based progress calculation.
        private int _totalExpectedSteps = 1;
        private int _currentStepNumber = 0;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the unique ID of the parent <see cref="InternalData.MasterAction"/> this context is associated with.
        /// </summary>
        public string MasterActionId => _masterAction.Id;

        /// <summary>
        /// Gets a logger pre-configured with the MasterActionId for contextual logging within the workflow.
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        /// Gets the <see cref="System.Threading.CancellationToken"/> for the entire Master Action.
        /// Workflow stages should monitor this token to support graceful cancellation of the operation.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// Gets the initial parameters provided by the API request that initiated this Master Action.
        /// These parameters are available read-only to all stages of the workflow.
        /// </summary>
        public IReadOnlyDictionary<string, object> Parameters => _masterAction.Parameters;

        /// <summary>
        /// Gets the progress reporter for the currently executing stage.
        /// Stage handlers use this to report their granular progress (percentage and messages)
        /// back to the master action, which then aggregates overall progress.
        /// This is updated by <see cref="BeginStageAsync(string, object?)"/>.
        /// </summary>
        public IProgress<StageProgress> StageProgress { get; private set; }

        /// <summary>
        /// Gets a direct reference to the <see cref="InternalData.MasterAction"/> state object that this context manages.
        /// All state-mutating methods in this context will operate on this object.
        /// </summary>
        public MasterAction MasterAction => _masterAction;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="MasterActionContext"/> class.
        /// </summary>
        /// <param name="masterAction">The core <see cref="InternalData.MasterAction"/> state object this context will manage. Must not be null.</param>
        /// <param name="journalService">The <see cref="IJournalService"/> for recording workflow events. Must not be null.</param>
        /// <param name="logger">The <see cref="ILogger"/> for logging messages, pre-configured for this context. Must not be null.</param>
        /// <param name="cancellationToken">The <see cref="System.Threading.CancellationToken"/> for signalling cancellation to the workflow.</param>
        /// <param name="logFlushProvider">A function delegate (<see cref="Func{Task}"/>) that provides the mechanism to flush buffered logs. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if masterAction, logger, or logFlushProvider is null.</exception>
        public MasterActionContext(
            MasterAction masterAction,
            IJournalService journalService,
            ILogger logger,
            CancellationToken cancellationToken,
            Func<Task> logFlushProvider)
        {
            _masterAction = masterAction ?? throw new ArgumentNullException(nameof(masterAction));
            _journalService = journalService; // journalService is used by methods, nullability handled by DI or caller of those methods
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            CancellationToken = cancellationToken;
            _logFlushProvider = logFlushProvider ?? throw new ArgumentNullException(nameof(logFlushProvider));
            // The stage progress reporter is initialized here, but it will be replaced
            // with a stage-specific one when BeginStage is called.
            StageProgress = new Progress<StageProgress>();
        }

        #region Public Methods

        /// <summary>
        /// Initializes the progress tracking for the workflow by setting the estimated
        /// total number of steps or stages in the "happy path" of execution.
        /// This method must be called at the beginning of a workflow handler to enable accurate progress reporting.
        /// </summary>
        /// <param name="totalSteps">The estimated number of distinct stages or logical steps in a successful run of the workflow.</param>
        public void InitializeProgress(int totalSteps)
        {
            _totalExpectedSteps = Math.Max(1, totalSteps); // Ensure at least 1 step to avoid division by zero.
            _currentStepNumber = 0;
            _masterAction.OverallProgressPercent = 0;
        }

        /// <summary>
        /// Dynamically adds a step to the total number of expected steps for progress calculation.
        /// This is useful when an unexpected but necessary stage (e.g., a recovery action or an optional branch)
        /// is added to the workflow after initial progress setup.
        /// </summary>
        public void AddExpectedStep() => _totalExpectedSteps++;

        /// <summary>
        /// Signals the start of a new stage within the Master Action.
        /// This method advances the internal step counter for progress calculation,
        /// records the stage initiation in the journal via <see cref="IJournalService"/>,
        /// and sets up a new progress reporter (<see cref="StageProgress"/>) specific to this stage.
        /// It also ensures logs from any previous stage are flushed.
        /// </summary>
        /// <param name="stageName">A descriptive name for the stage, used in logging and journaling (e.g., "NodeRestart", "PackageDeployment").</param>
        /// <param name="stageInput">Optional. Input data or parameters specific to this stage, to be recorded in the journal for diagnostic purposes.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of beginning the stage, primarily for awaiting log flushes and journal writes.</returns>
        public async Task BeginStageAsync(string stageName, object? stageInput = null)
        {
            // Flush any logs from the previous stage before starting the new one.
            await FlushLogsAsync();

            _currentStageName = stageName;
            // The context now calls the journal service to create the stage's record.
            await _journalService.RecordStageInitiatedAsync(this, stageName, stageInput);
            _currentStepNumber++;
            LogInfo($"--- Beginning Stage {_currentStepNumber}/{_totalExpectedSteps}: {stageName} ---");
            
            // Create a progress reporter that will automatically calculate the overall
            // progress of the master action based on the progress of this single stage.
            StageProgress = new Progress<StageProgress>(progress =>
            {
                // Calculate the progress contributed by already completed stages.
                double completedStagesProgress = ((double)_currentStepNumber - 1) / _totalExpectedSteps * 100;
                // Calculate the progress contributed by the current stage.
                double currentStageContribution = (double)progress.ProgressPercent / _totalExpectedSteps;
                
                _masterAction.OverallProgressPercent = (int)(completedStagesProgress + currentStageContribution);
                
                // TODO: Update a "CurrentStatusMessage" on the MasterAction object based on stage progress message
            });
        }

        /// <summary>
        /// Signals the completion of the current stage.
        /// This method ensures any logs generated during this stage are flushed and then
        /// records the stage completion in the journal via <see cref="IJournalService"/>, including any result data from the stage.
        /// </summary>
        /// <param name="stageResult">Optional. The result or output data from the completed stage, to be recorded in the journal.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation of completing the stage, primarily for awaiting log flushes and journal writes.</returns>
        public async Task CompleteStageAsync(object? stageResult = null)
        {
            // Flush any logs generated during this stage before marking it as complete.
            await FlushLogsAsync();

            // cleanly mark a stage as complete in the journal.
            await _journalService.RecordStageCompletedAsync(this, _currentStageName, stageResult);
        }
        
        /// <summary>
        /// Asynchronously requests a flush of all buffered log messages associated with this Master Action.
        /// This ensures that logs are persisted and sent to relevant listeners (e.g., UI via SignalR)
        /// in a timely manner, maintaining order, especially between stages.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous log flush operation.</returns>
        public async Task FlushLogsAsync()
        {
            await _logFlushProvider();
        }
        
        /// <summary>
        /// Sets the final, conclusive result payload for the entire Master Action.
        /// This method should be called by the <c>IMasterActionHandler</c> implementation when the workflow
        /// has successfully produced its primary output or result.
        /// </summary>
        /// <param name="result">The final result object of the Master Action. This will be serialized and stored.</param>
        public void SetFinalResult(object? result) => _masterAction.FinalResultPayload = result;

        /// <summary>
        /// Sets the Master Action to a final 'Succeeded' state, updates progress to 100%, and records the end time.
        /// </summary>
        /// <param name="message">A final message indicating successful completion, which will be logged.</param>
        public void SetCompleted(string message)
        {
            LogInfo(message);
            _masterAction.OverallStatus = OperationOverallStatus.Succeeded;
            _masterAction.OverallProgressPercent = 100;
            _masterAction.EndTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Sets the Master Action to a final 'Failed' state, logs the error, updates progress to 100% (as the action is complete), and records the end time.
        /// </summary>
        /// <param name="message">A message describing the failure, which will be logged as an error.</param>
        public void SetFailed(string message)
        {
            LogError(null, message);
            _masterAction.OverallStatus = OperationOverallStatus.Failed;
            _masterAction.OverallProgressPercent = 100; // A failed action is still 100% complete in its lifecycle.
            _masterAction.EndTime = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Sets the Master Action to a final 'Cancelled' state, logs a warning, updates progress to 100% (as the action is complete), and records the end time.
        /// </summary>
        /// <param name="message">A message indicating why the action was cancelled, which will be logged as a warning.</param>
        public void SetCancelled(string message)
        {
            LogWarning(message); // Cancellation is a warning, not an error.
            _masterAction.OverallStatus = OperationOverallStatus.Cancelled;
            _masterAction.OverallProgressPercent = 100; // A cancelled action is still 100% "complete" in its lifecycle.
            _masterAction.EndTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Logs an informational message related to the current Master Action.
        /// The message is sent to the configured <see cref="ILogger"/> and added to the <see cref="InternalData.MasterAction.RecentLogs"/> queue.
        /// </summary>
        /// <param name="message">The informational message to log.</param>
        public void LogInfo(string message)
        {
            // First, send the log to the standard logging pipeline (for NLog targets)
            Logger.LogInformation(message);
    
            // Second, add the formatted log to the in-memory queue for API status polling
            _masterAction.AddLogEntry($"[{DateTime.UtcNow:HH:mm:ss.fff} INFO] {message}");
        }

        /// <summary>
        /// Logs a warning message related to the current Master Action.
        /// The message is sent to the configured <see cref="ILogger"/> and added to the <see cref="InternalData.MasterAction.RecentLogs"/> queue.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        public void LogWarning(string message)
        {
            // First, send the log to the standard logging pipeline (for NLog targets)
            Logger.LogWarning(message);
    
            // Second, add the formatted log to the in-memory queue for API status polling
            _masterAction.AddLogEntry($"[{DateTime.UtcNow:HH:mm:ss.fff} WARN] {message}");
        }

        /// <summary>
        /// Logs an error message related to the current Master Action.
        /// The message (and optional exception) is sent to the configured <see cref="ILogger"/> and added to the <see cref="InternalData.MasterAction.RecentLogs"/> queue.
        /// </summary>
        /// <param name="ex">Optional. The <see cref="Exception"/> associated with the error.</param>
        /// <param name="message">The error message to log.</param>
        public void LogError(Exception? ex, string message)
        {
            // First, send the log to the standard logging pipeline (for NLog targets)
            Logger.LogError(ex, message);

            // Second, add the formatted log to the in-memory queue for API status polling
            _masterAction.AddLogEntry($"[{DateTime.UtcNow:HH:mm:ss.fff} ERROR] {message}");
        }
        #endregion
    }
} 