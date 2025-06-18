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
        /// A reference to the internal 'NodeAction' object of the currently running multi-node stage.
        /// This is used to provide real-time node task details to the UI.
        /// </summary>
        public NodeAction? CurrentStageOperation { get; set; }
        
        /// <summary>
        /// A list to store the most recent log messages for this action.
        /// This is public for serialization but should only be accessed via thread-safe methods.
        /// </summary>
        public List<string> RecentLogs { get; set; } = new();


        private readonly object _logLock = new();

        public MasterAction(string id, OperationType type, string? name, string? initiatedBy, IReadOnlyDictionary<string, object> parameters)
        {
            Id = id;
            Type = type;
            Name = name;
            InitiatedBy = initiatedBy;
            StartTime = DateTime.UtcNow;
            OverallStatus = OperationOverallStatus.PendingInitiation;
            Parameters = parameters;
            RecentLogs = new List<string>();
        }

        /// <summary>
        /// Gets a boolean indicating if the Master Action has reached a terminal state.
        /// This relies on an extension method for the OperationOverallStatus enum.
        /// </summary>
        public bool IsComplete => OverallStatus.IsCompleted();

        /// <summary>
        /// Adds a log message to the RecentLogs list in a thread-safe manner.
        /// </summary>
        public void AddLogEntry(string message)
        {
            lock (_logLock)
            {
                RecentLogs.Add(message);
                while (RecentLogs.Count > 1000)
                {
                    RecentLogs.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Gets a thread-safe snapshot (a copy) of the recent logs.
        /// </summary>
        /// <returns>A new list containing the recent log messages.</returns>
        public List<string> GetRecentLogs()
        {
            lock (_logLock)
            {
                // Return a copy of the list so the original can be modified
                // while the caller is iterating over the snapshot.
                return new List<string>(RecentLogs);
            }
        }
    }
} 