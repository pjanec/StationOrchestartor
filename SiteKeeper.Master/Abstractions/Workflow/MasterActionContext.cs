using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Model.InternalData;
using SiteKeeper.Master.Workflow.DTOs;
using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SiteKeeper.Master.Workflow;

namespace SiteKeeper.Master.Abstractions.Workflow
{
    /// <summary>
    /// A stateful object that carries information and provides services throughout the
    /// lifecycle of a single Master Action. An instance of this class is created for
    /// each workflow run and passed to every stage handler. It acts as the primary
    /// bridge between the running workflow logic and the master state object.
    /// </summary>
    public class MasterActionContext : IDisposable
    {
        #region Private Fields
        
        /// <summary>
        /// A direct reference to the MasterAction state object that this context manages.
        /// All state-mutating methods in this context will operate on this object.
        /// </summary>
        private readonly MasterAction _masterAction;
        private readonly IJournal _journalService;
        private readonly IServiceProvider _serviceProvider;
        private int _lastStartedStageIndex = 0;

        /// <summary>
        /// A function pointer to the log flushing mechanism of the UILoggingTarget.
        /// This allows the workflow to request a log sync without being directly coupled to NLog.
        /// </summary>
        private readonly Func<Task> _logFlushProvider;

        // Fields for managing logical step-based progress calculation.
        private int _totalExpectedSteps = 1;
        private int _currentStepNumber = 0;

        private readonly object _actionStateLock; // <<< Add field for the lock

        #endregion

        #region Public Properties

        /// <summary> The unique ID of the parent Master Action. </summary>
        public string MasterActionId => _masterAction.Id;

        /// <summary> 
        /// A logger instance provided by the DI container. When resolved within a workflow's
        /// DI scope, this will be an IWorkflowLogger instance that carries the correct context.
        /// </summary>
        public ILogger Logger { get; }

        /// <summary> The cancellation token for the entire Master Action. Stages should monitor this token to support cancellation. </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary> The initial parameters provided by the API request, available to all stages. </summary>
        public IReadOnlyDictionary<string, object> Parameters => _masterAction.Parameters;

        public MasterAction MasterAction => _masterAction;

        #endregion

        public MasterActionContext(
            MasterAction masterAction,
            IJournal journalService,
            IServiceProvider serviceProvider,
            ILogger logger, // This is an IWorkflowLogger passed as ILogger from the scoped DI container
            CancellationToken cancellationToken,
            Func<Task> logFlushProvider,
            object actionStateLock)
        {
            _masterAction = masterAction ?? throw new ArgumentNullException(nameof(masterAction));
            _journalService = journalService;
            _serviceProvider = serviceProvider;
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            CancellationToken = cancellationToken;
            _logFlushProvider = logFlushProvider ?? throw new ArgumentNullException(nameof(logFlushProvider));
			_actionStateLock = actionStateLock ?? throw new ArgumentNullException( nameof( actionStateLock ) );
		}

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
        /// updates the context of the scoped IWorkflowLogger.
        /// </summary>
        /// <param name="stageName">A descriptive name for the stage, used in logging.</param>
        /// <param name="subActionCount">The number of sub-actions in the stage.</param>
        /// <param name="stageInput">Optional input data for the stage, to be recorded in the journal.</param>
        public async Task<IStageContext> BeginStageAsync(string stageName, int subActionCount = 1, object? stageInput = null)
        {
            await FlushLogsAsync();

            _currentStepNumber++;
            _lastStartedStageIndex = _currentStepNumber;

            lock (_actionStateLock)
            {
                 // If this is the first stage being started, update the overall status from Initiated to InProgress.
                if (_masterAction.OverallStatus == MasterActionStatus.Initiated)
                {
                    _masterAction.OverallStatus = MasterActionStatus.InProgress;
                }

                // Set the current stage on the parent action for live tracking.
                _masterAction.CurrentStageName = stageName;
                _masterAction.CurrentStageIndex = _currentStepNumber;
            }


            // Update the scoped logger's stage context. This is safe because we resolve
            // IWorkflowLogger within the MasterAction's DI scope.
            if (this.Logger is IWorkflowLogger workflowLogger)
            {
                workflowLogger.SetStage(_currentStepNumber, stageName);
            }
            
            // 3. Call the journal service, now passing the explicit stage index.
            await _journalService.RecordStageInitiatedAsync(this.MasterActionId, _currentStepNumber, stageName, stageInput);
            
            // 4. Log the beginning of the stage.
            LogInfo($"--- Beginning Stage {_currentStepNumber}/{_totalExpectedSteps}: {stageName} ---");
            
            // Create a progress reporter that will automatically calculate the overall
            // progress of the master action based on the progress of this single stage.
            var stageProgressReporter = new Progress<StageProgress>(progress =>
            {
                // Calculate the progress contributed by already completed stages.
                double completedStagesProgress = ((double)_currentStepNumber - 1) / _totalExpectedSteps * 100;
                // Calculate the progress contributed by the current stage.
                double currentStageContribution = (double)progress.ProgressPercent / _totalExpectedSteps;
                
                _masterAction.OverallProgressPercent = (int)(completedStagesProgress + currentStageContribution);
            });

            return new StageContext(this, _serviceProvider, stageName, _currentStepNumber, subActionCount, stageProgressReporter, _actionStateLock );
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

        void FinalizeAction(MasterActionStatus status)
		{
            _masterAction.CurrentStageName = null; // Clear the name as no stage is running
            _masterAction.OverallStatus = status;
            // A failed action is still 100% complete
            // A cancelled action is still 100% "complete" in its lifecycle.
            _masterAction.OverallProgressPercent = 100;
            _masterAction.EndTime = DateTime.UtcNow;

			// Set the logger context to the final stage.
			if( this.Logger is IWorkflowLogger workflowLogger )
			{
				workflowLogger.SetStage( _lastStartedStageIndex + 1, "_final" );
			}
		}

		/// <summary>
		/// Sets the Master Action to a final 'Succeeded' state.
		/// </summary>
		public void SetCompleted(string message)
        {
            LogInfo(message);

            FinalizeAction( MasterActionStatus.Succeeded );
        }

        /// <summary>
        /// Sets the Master Action to a final 'Failed' state.
        /// </summary>
        public void SetFailed(string message)
        {
            // the log must come before Finalize to appear in the current stage journal
            LogError(null, message);

            FinalizeAction( MasterActionStatus.Failed );
        }
        
        /// <summary>
        /// Sets the Master Action to a final 'Cancelled' state.
        /// </summary>
        public void SetCancelled(string message)
        {
            LogWarning(message); // Cancellation is a warning, not an error.

            FinalizeAction( MasterActionStatus.Cancelled );
        }

        /// <summary>
        /// Logs an informational message. This now simply delegates to the standard ILogger.
        /// The DI-injected IWorkflowLogger will handle adding the context.
        /// </summary>
        public void LogInfo(string message)
        {
            Logger.LogInformation(message);
            _masterAction.AddLogEntry($"[{DateTime.UtcNow:HH:mm:ss.fff} INFO] {message}");
        }

        /// <summary>
        /// Logs an informational message. This helper ensures the message is sent to the standard logger,
        /// added to the in-memory RecentLogs queue, and persisted to the journal via the UILoggingTarget.
        /// </summary>
        public void LogWarning(string message)
        {
            Logger.LogWarning(message);
            _masterAction.AddLogEntry($"[{DateTime.UtcNow:HH:mm:ss.fff} WARN] {message}");
        }

        /// <summary>
        /// Logs an error message. This helper ensures the message is sent to the standard logger,
        /// added to the in-memory RecentLogs queue, and persisted to the journal via the UILoggingTarget.
        /// </summary>
        public void LogError(Exception? ex, string message)
        {
            Logger.LogError(ex, message);
            _masterAction.AddLogEntry($"[{DateTime.UtcNow:HH:mm:ss.fff} ERROR] {message}");
        }

        public void Dispose() 
        { 
            // Nothing to dispose in this class.
        }
    }
}
