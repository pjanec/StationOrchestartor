using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Releases
{
    /// <summary>
    /// DTO for the response when listing available release versions.
    /// As defined in swagger: #/components/schemas/ReleaseListResponse
    /// </summary>
    public class ReleaseListResponse
    {
        /// <summary>
        /// The type of environment these releases pertain to.
        /// Corresponds to the 'environmentType' property in the Swagger schema.
        /// </summary>
        /// <example>"MyProdEnvType"</example>
        [Required] // Assuming environmentType is a required part of this response
        [JsonPropertyName("environmentType")]
        public string EnvironmentType { get; set; } = string.Empty;

        /// <summary>
        /// List of available release versions' summary information.
        /// Corresponds to the 'versions' property in the Swagger schema, which is an array of 'ReleaseVersionInfo' objects.
        /// </summary>
        [Required] // A list response should ideally always have the list property, even if empty.
        [JsonPropertyName("versions")]
        public List<ReleaseVersionInfo> Versions { get; set; } = new List<ReleaseVersionInfo>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ReleaseListResponse"/> class.
        /// </summary>
        public ReleaseListResponse()
        {
            Versions = new List<ReleaseVersionInfo>();
        }
    }
} 