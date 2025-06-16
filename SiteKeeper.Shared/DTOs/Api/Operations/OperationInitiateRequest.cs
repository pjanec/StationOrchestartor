using SiteKeeper.Shared.Enums;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.Operations
{
    /// <summary>
    /// Represents the request body for initiating a new asynchronous operation.
    /// </summary>
    /// <remarks>
    /// This DTO is used to trigger various long-running operations within the SiteKeeper system.
    /// It specifies the type of operation and can include a flexible dictionary of parameters
    /// tailored to that specific operation type (e.g., target nodes, package names, versions, file paths).
    /// Based on the OperationInitiateRequest schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class OperationInitiateRequest
    {
        /// <summary>
        /// The type of operation to be initiated.
        /// </summary>
        /// <example>OperationType.EnvUpdateOnline</example>
        [Required]
        public OperationType OperationType { get; set; }

        /// <summary>
        /// An optional user-defined description or label for this specific operation instance.
        /// This can help in identifying or categorizing operations in logs or UI displays.
        /// </summary>
        /// <example>"Urgent security patch deployment for web servers."</example>
        public string? Description { get; set; }

        /// <summary>
        /// A dictionary of parameters specific to the <see cref="OperationType"/>.
        /// The keys and expected value types in this dictionary vary depending on the operation.
        /// Examples:
        /// - For EnvUpdateOnline: {"manifestUrl": "http://...", "targetVersion": "1.3.0"}
        /// - For NodeRestart: {"nodeNames": ["Server1", "Server2"]}
        /// - For PackageChangeVersion: {"packageName": "MyLib", "targetVersion": "2.0.1", "nodeNames": ["Server1"]}
        /// The specific parameters required for each operation type should be documented alongside the API endpoint
        /// or in the definition of the OperationType itself.
        /// </summary>
        /// <example>{"targetVersion": "1.3.0", "nodeNames": ["AppServer01", "AppServer02"]}</example>
        public Dictionary<string, object>? Parameters { get; set; }
    }
} 