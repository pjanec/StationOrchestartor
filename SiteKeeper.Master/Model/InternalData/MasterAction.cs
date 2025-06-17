using SiteKeeper.Shared.Enums;
using SiteKeeper.Shared.Enums.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SiteKeeper.Master.Model.InternalData
{
    /// <summary>
    /// Represents the state and context of a single, high-level workflow instance.
    /// This is the central object that tracks the entire lifecycle of a user-initiated action,
    /// from start to finish, including all its stages, overall progress, and final results.
    /// </summary>
    public class MasterAction
    {
        /// <summary>
        /// A unique identifier for this specific Master Action instance.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The high-level type of the workflow being executed (e.g., EnvUpdateOnline).
        /// </summary>
        public OperationType Type { get; set; }

        /// <summary>
        /// An optional user-provided name or description for this action.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The user who initiated this action.
        /// </summary>
        public string? InitiatedBy { get; set; }

        /// <summary>
        /// The initial parameters that the action was started with, passed from the API request.
        /// I have added a public setter here to align with the usage in MasterActionCoordinatorService,
        /// as outlined in the provided documentation.
        /// </summary>
        public IReadOnlyDictionary<string, object> Parameters { get; set; }

        /// <summary>
        /// The UTC timestamp when the action was initiated.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// The UTC timestamp when the action reached a terminal state (Succeeded, Failed, Cancelled).
        /// Null if the action is still in progress.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// The aggregated overall status of the entire Master Action.
        /// </summary>
        public OperationOverallStatus OverallStatus { get; set; }

        /// <summary>
        /// The calculated overall progress of the Master Action, as a percentage (0-100).
        /// </summary>
        public int OverallProgressPercent { get; set; }
        
        /// <summary>
        /// The final, aggregate result payload of the entire workflow.
        /// This is used for actions that produce a specific output, like a scan result.
        /// </summary>
        public object? FinalResultPayload { get; set; }

        /// <summary>
        /// A reference to the internal 'Operation' object of the currently running multi-node stage.
        /// This is used to provide real-time node task details to the UI.
        /// </summary>
        public Operation? CurrentStageOperation { get; set; }
        
        /// <summary>
        /// A thread-safe queue to store the most recent log messages for this action.
        /// </summary>
        private readonly ConcurrentQueue<string> _recentLogs = new();

        public MasterAction(string id, OperationType type, string? name, string? initiatedBy, IReadOnlyDictionary<string, object> parameters)
        {
            Id = id;
            Type = type;
            Name = name;
            InitiatedBy = initiatedBy;
            StartTime = DateTime.UtcNow;
            OverallStatus = OperationOverallStatus.PendingInitiation;
            Parameters = parameters;
        }

        /// <summary>
        /// Gets a boolean indicating if the Master Action has reached a terminal state.
        /// This relies on an extension method for the OperationOverallStatus enum.
        /// </summary>
        public bool IsComplete => OverallStatus.IsCompleted();

        /// <summary>
        /// Adds a log message to the recent logs queue, maintaining a fixed size of 1000 entries
        /// to prevent excessive memory usage.
        /// </summary>
        public void AddLogEntry(string message)
        {
            _recentLogs.Enqueue(message);
            // Keep the log buffer from growing indefinitely.
            while (_recentLogs.Count > 1000)
            {
                _recentLogs.TryDequeue(out _);
            }
        }

        /// <summary>
        /// Retrieves a snapshot of the most recent log messages.
        /// </summary>
        public List<string> GetRecentLogs()
        {
            return _recentLogs.ToList();
        }
    }
} 