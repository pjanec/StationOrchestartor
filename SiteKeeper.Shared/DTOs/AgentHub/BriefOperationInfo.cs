using SiteKeeper.Shared.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.AgentHub
{
    /// <summary>
    /// Provides brief, summary information about an operation relevant to an agent.
    /// </summary>
    /// <remarks>
    /// This DTO is used within <see cref="MasterStateForAgent"/> to inform an agent about
    /// operations that might involve it or that provide context for its tasks.
    /// It includes the operation's ID and its general type.
    /// </remarks>
    public class BriefOperationInfo
    {
        /// <summary>
        /// The unique identifier of the operation.
        /// </summary>
        /// <example>"op-envupdate-abc123"</example>
        [Required]
        public string OperationId { get; set; } = string.Empty;

        /// <summary>
        /// The type of the operation.
        /// </summary>
        /// <example>OperationType.EnvUpdateOnline</example>
        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public OperationType OperationType { get; set; }
    }
} 