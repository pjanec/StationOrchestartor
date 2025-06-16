using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Operations
{
    /// <summary>
    /// Represents the request body for initiating an online environment update.
    /// This DTO is used for the POST /operations/env-update-online endpoint.
    /// </summary>
    /// <remarks>
    /// The primary piece of information required is the <see cref="TargetVersionId"/>,
    /// which specifies the version the environment should be updated to.
    /// This class aligns with the 'EnvUpdateRequest' schema defined in `web api swagger.yaml`.
    /// </remarks>
    public class EnvUpdateRequest
    {
        /// <summary>
        /// Gets or sets the target version ID for the environment update.
        /// This field is required.
        /// </summary>
        /// <example>"1.2.5"</example>
        [Required]
        [JsonPropertyName("targetVersionId")]
        public string TargetVersionId { get; set; } = string.Empty;
    }
} 