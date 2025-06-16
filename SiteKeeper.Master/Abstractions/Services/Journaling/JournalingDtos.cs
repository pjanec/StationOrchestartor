using SiteKeeper.Shared.Enums;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System;

namespace SiteKeeper.Master.Abstractions.Services.Journaling
{
    /// <summary>
    /// Enum defining the types of significant state changes to be recorded in the Change Journal.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ChangeEventType
    {
        Update,
        Backup,
        Restore,
        Revert,
        ManualConfigurationChange,
        SystemEvent
    }

    /// <summary>
    /// Contains the initial information to create a new state change record in the Change Journal.
    /// This is passed to the IJournalService to initiate a tracked change.
    /// </summary>
    public class StateChangeInfo
    {
        public ChangeEventType Type { get; set; }
        public string Description { get; set; }
        public string InitiatedBy { get; set; }
        public string SourceMasterActionId { get; set; }
    }

    /// <summary>
    /// The result returned after initiating a state change record. It provides the unique ID
    /// of the new change record and, if the change was a backup, the path for storing artifacts.
    /// </summary>
    public class StateChangeCreationResult
    {
        public string ChangeId { get; set; }
        public string? BackupArtifactPath { get; set; }
    }

    /// <summary>
    /// Contains the final information needed to finalize a state change record by appending a 'Completed' event.
    /// </summary>
    public class StateChangeFinalizationInfo
    {
        public string ChangeId { get; set; }
        public OperationOutcome Outcome { get; set; }
        public string Description { get; set; }
        public object ResultArtifact { get; set; }
    }

    /// <summary>
    /// The DTO representing a single, immutable record in the system_changes_index.log file.
    /// </summary>
    public class SystemChangeRecord
    {
        public DateTime Timestamp { get; set; }
        public string ChangeId { get; set; }
        public string EventType { get; set; }
        public string? Outcome { get; set; }
        public string Description { get; set; }
        public string ArtifactPath { get; set; }
        public string SourceMasterActionId { get; set; }
    }
} 