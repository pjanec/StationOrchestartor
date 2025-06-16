using SiteKeeper.Shared.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.SoftwareControl
{
    /// <summary>
    /// Represents the status and detailed information of a single managed application.
    /// This DTO aligns with the 'AppStatusInfo' schema defined in `web api swagger.yaml`.
    /// </summary>
    /// <remarks>
    /// This DTO is used to convey the state of an application, including its identifier, name, description,
    /// operational status, and association with nodes and plans. It also provides metrics like status age and last exit code.
    /// Modifications from previous versions:
    /// - 'AppId' renamed to 'Id' and JSON property name changed to "id" for Swagger conformance.
    /// - Extra properties 'IsEnabled' and 'Nodes' have been removed.
    /// - Missing properties 'NodeName', 'PlanName', 'StatusAgeSeconds', and 'ExitCode' have been added.
    /// The 'Status' property uses the <see cref="AppOperationalStatus"/> enum, which has been verified for conformance.
    /// </remarks>
    public class AppStatusInfo
    {
        /// <summary>
        /// Unique identifier for the application, often a composite like NodeName.AppName for uniqueness across an environment.
        /// Corresponds to the 'id' property in the Swagger schema.
        /// </summary>
        /// <example>"SIMSERVER.MainAppService"</example>
        [Required]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// The name of the node where this instance of the application primarily runs or is defined.
        /// Corresponds to the 'nodeName' property in the Swagger schema.
        /// </summary>
        /// <example>"SIMSERVER"</example>
        [Required]
        [JsonPropertyName("nodeName")]
        public string NodeName { get; set; } = string.Empty;

        /// <summary>
        /// User-friendly name of the application.
        /// Corresponds to the 'appName' property in the Swagger schema.
        /// </summary>
        /// <example>"MainAppService"</example>
        [Required]
        [JsonPropertyName("appName")]
        public string AppName { get; set; } = string.Empty;

        /// <summary>
        /// The current operational status of the application.
        /// Corresponds to the 'status' property in the Swagger schema.
        /// </summary>
        /// <example>AppOperationalStatus.Running</example>
        [Required]
        [JsonPropertyName("status")]
        public AppOperationalStatus Status { get; set; }

        /// <summary>
        /// Optional description of the application.
        /// Corresponds to the 'description' property in the Swagger schema.
        /// </summary>
        /// <example>"Core application service for main business logic."</example>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Optional name of the plan this application belongs to.
        /// Corresponds to the 'planName' property in the Swagger schema.
        /// </summary>
        /// <example>"CoreServicesPlan"</example>
        [JsonPropertyName("planName")]
        public string? PlanName { get; set; }

        /// <summary>
        /// Duration in seconds for which the application has been in its current status.
        /// Corresponds to the 'statusAgeSeconds' property in the Swagger schema.
        /// </summary>
        /// <example>3600</example>
        [Required]
        [JsonPropertyName("statusAgeSeconds")]
        public int StatusAgeSeconds { get; set; }

        /// <summary>
        /// The last exit code of the application if it has stopped or encountered an error.
        /// Null if the application is running or the exit code is not applicable/available.
        /// Corresponds to the 'exitCode' property in the Swagger schema.
        /// </summary>
        /// <example>"0"</example>
        [JsonPropertyName("exitCode")]
        public string? ExitCode { get; set; }
    }
} 