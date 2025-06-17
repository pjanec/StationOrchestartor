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

        /// <summary> The unique ID of the parent Master Action. </summary>
        public string MasterActionId => _masterAction.Id;

        /// <summary> A logger pre-configured with the MasterActionId for contextual logging. </summary>
        public ILogger Logger { get; }

        /// <summary> The cancellation token for the entire Master Action. Stages should monitor this token to support cancellation. </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary> The initial parameters provided by the API request, available to all stages. </summary>
        public IReadOnlyDictionary<string, object> Parameters => _masterAction.Parameters;

        /// <summary>
        /// The progress reporter for the currently executing stage. Stage handlers use this
        /// to report their granular progress back to the master action.
        /// </summary>
        public IProgress<StageProgress> StageProgress { get; private set; }

        public MasterAction MasterAction => _masterAction;

        #endregion

        public MasterActionContext(
            MasterAction masterAction,
            IJournalService journalService,
            ILogger logger,
            CancellationToken cancellationToken,
            Func<Task> logFlushProvider)
        {
            _masterAction = masterAction ?? throw new ArgumentNullException(nameof(masterAction));
            _journalService = journalService;
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
        /// number of steps in the "happy path". Must be called at the start of a workflow handler.
        /// </summary>
        /// <param name="totalSteps">The estimated number of stages in a successful run.</param>
        public void InitializeProgress(int totalSteps)
        {
            _totalExpectedSteps = Math.Max(1, totalSteps); // Ensure at least 1 step
            _currentStepNumber = 0;
            _masterAction.OverallProgressPercent = 0;
        }

        /// <summary>
        /// Dynamically adds a step to the total number of expected steps.
        /// This is used when an unexpected stage (like a recovery action) is added to the workflow.
        /// </summary>
        public void AddExpectedStep() => _totalExpectedSteps++;

        /// <summary>
        /// Signals the start of a new stage. This advances the internal step counter and
        /// sets up a new progress reporter for the stage.
        /// </summary>
        /// <param name="stageName">A descriptive name for the stage, used in logging.</param>
        /// <param name="stageInput">Optional input data for the stage, to be recorded in the journal.</param>
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
                
                // TODO: Update a "CurrentStatusMessage" on the MasterAction object
            });
        }

        public async Task CompleteStageAsync(object? stageResult = null)
        {
            // Flush any logs generated during this stage before marking it as complete.
            await FlushLogsAsync();

            // cleanly mark a stage as complete in the journal.
            await _journalService.RecordStageCompletedAsync(this, _currentStageName, stageResult);
        }
        
        /// <summary>
        /// Pauses workflow execution until all buffered log messages for this
        /// Master Action have been sent to the UI, guaranteeing log order between stages.
        /// </summary>
        public async Task FlushLogsAsync()
        {
            await _logFlushProvider();
        }
        
        /// <summary>
        /// Sets the final, conclusive result payload for the entire Master Action.
        /// This should be called by the IMasterActionHandler when the workflow has
        /// successfully produced its primary output.
        /// </summary>
        public void SetFinalResult(object? result) => _masterAction.FinalResultPayload = result;

        /// <summary>
        /// Sets the Master Action to a final 'Succeeded' state.
        /// </summary>
        public void SetCompleted(string message)
        {
            LogInfo(message);
            _masterAction.OverallStatus = OperationOverallStatus.Succeeded;
            _masterAction.OverallProgressPercent = 100;
            _masterAction.EndTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Sets the Master Action to a final 'Failed' state.
        /// </summary>
        public void SetFailed(string message)
        {
            LogError(null, message);
            _masterAction.OverallStatus = OperationOverallStatus.Failed;
            _masterAction.OverallProgressPercent = 100; // A failed action is still 100% complete
            _masterAction.EndTime = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Sets the Master Action to a final 'Cancelled' state.
        /// </summary>
        public void SetCancelled(string message)
        {
            LogWarning(message); // Cancellation is a warning, not an error.
            _masterAction.OverallStatus = OperationOverallStatus.Cancelled;
            _masterAction.OverallProgressPercent = 100; // A cancelled action is still 100% "complete" in its lifecycle.
            _masterAction.EndTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Logs an informational message. This helper ensures the message is sent to the standard logger,
        /// added to the in-memory RecentLogs queue, and persisted to the journal via the UILoggingTarget.
        /// </summary>
        public void LogInfo(string message)
        {
            // First, send the log to the standard logging pipeline (for NLog targets)
            Logger.LogInformation(message);
    
            // Second, add the formatted log to the in-memory queue for API status polling
            _masterAction.AddLogEntry($"[{DateTime.UtcNow:HH:mm:ss.fff} INFO] {message}");
        }

        /// <summary>
        /// Logs an informational message. This helper ensures the message is sent to the standard logger,
        /// added to the in-memory RecentLogs queue, and persisted to the journal via the UILoggingTarget.
        /// </summary>
        public void LogWarning(string message)
        {
            // First, send the log to the standard logging pipeline (for NLog targets)
            Logger.LogWarning(message);
    
            // Second, add the formatted log to the in-memory queue for API status polling
            _masterAction.AddLogEntry($"[{DateTime.UtcNow:HH:mm:ss.fff} WARN] {message}");
        }

        /// <summary>
        /// Logs an error message. This helper ensures the message is sent to the standard logger,
        /// added to the in-memory RecentLogs queue, and persisted to the journal via the UILoggingTarget.
        /// </summary>
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