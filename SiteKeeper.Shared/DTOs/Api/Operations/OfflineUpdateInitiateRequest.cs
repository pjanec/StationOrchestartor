using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Operations
{
    /// <summary>
    /// Represents the request to initiate an offline environment update using a selected bundle.
    /// </summary>
    /// <remarks>
    /// This DTO is used for the POST /operations/offline-update/initiate endpoint.
    /// It aligns with the 'OfflineUpdateInitiateRequest' schema in `web api swagger.yaml`.
    /// </remarks>
    public class OfflineUpdateInitiateRequest
    {
        /// <summary>
        /// Gets or sets the ID of the offline update bundle (obtained from a prior scan) 
        /// that should be used for the environment update.
        /// This field is required.
        /// </summary>
        /// <example>"bundle-001-D"</example>
        [Required]
        [JsonPropertyName("selectedBundleId")]
        public string SelectedBundleId { get; set; } = string.Empty;

        // Future optional properties for the update process:
        // /// <summary>
        // /// Gets or sets a flag indicating whether to force the update, potentially ignoring certain non-critical warnings.
        // /// </summary>
        // [JsonPropertyName("force")]
        // public bool? Force { get; set; }

        // /// <summary>
        // /// Gets or sets a list of specific update steps to skip, if applicable to the bundle type.
        // /// </summary>
        // [JsonPropertyName("skipSteps")]
        // public List<string>? SkipSteps { get; set; }
    }
} 