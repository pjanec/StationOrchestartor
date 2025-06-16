using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.OfflineUpdate
{
    /// <summary>
    /// Confirms the successful upload of an offline update package.
    /// Aligns with the OfflinePackageUploadConfirmation schema in `web api swagger.yaml`.
    /// </summary>
    public class OfflinePackageUploadConfirmation
    {
        /// <summary>
        /// A unique identifier assigned to the uploaded package by the system.
        /// This ID can be used in subsequent operations (e.g., preparing the update).
        /// </summary>
        /// <example>"pkg-offline-20231027-abc123xyz"</example>
        [JsonPropertyName("packageId")]
        [Required]
        public string PackageId { get; set; } = string.Empty;

        /// <summary>
        /// The original name of the uploaded file.
        /// </summary>
        /// <example>"SiteKeeper_Update_v2.1.0.zip"</example>
        [JsonPropertyName("fileName")]
        [Required]
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// The size of the uploaded file in bytes.
        /// </summary>
        /// <example>10485760</example>
        [JsonPropertyName("size")]
        [Required]
        public long Size { get; set; }

        /// <summary>
        /// Timestamp of when the package upload was completed and recorded by the server.
        /// </summary>
        /// <example>"2023-10-27T15:00:00Z"</example>
        [JsonPropertyName("uploadTimestamp")]
        [Required]
        public DateTime UploadTimestamp { get; set; }

        /// <summary>
        /// An optional message providing additional information about the upload, such as next steps or warnings.
        /// </summary>
        /// <example>"Package uploaded successfully. Proceed to prepare for deployment."</example>
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
} 