using SiteKeeper.Shared.DTOs.API.Packages;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.Environment
{
    /// <summary>
    /// Represents the "pure" state of an environment version, detailing expected packages and versions per node.
    /// As defined in swagger: #/components/schemas/PureManifest
    /// </summary>
    public class PureManifest
    {
        /// <summary>
        /// The name of the environment.
        /// </summary>
        /// <example>"MyProdEnv-SiteA"</example>
        [JsonPropertyName("environmentName")]
        public string EnvironmentName { get; set; } = string.Empty;

        /// <summary>
        /// The version identifier for this manifest.
        /// </summary>
        /// <example>"1.2.3"</example>
        [JsonPropertyName("versionId")]
        public string VersionId { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when this manifest version was applied or defined.
        /// </summary>
        /// <example>"2025-05-20T10:00:00Z"</example>
        [JsonPropertyName("appliedAt")]
        public DateTime AppliedAt { get; set; }

        /// <summary>
        /// The list of nodes and their package configurations defined in this manifest.
        /// </summary>
        [JsonPropertyName("nodes")]
        public List<NodeInManifest> Nodes { get; set; } = new();

        /// <summary>
        /// List of optional packages that are explicitly part of this pure manifest version.
        /// </summary>
        [JsonPropertyName("optionalPackagesDefinedInManifest")]
        public List<PackageVersionInfo> OptionalPackagesDefinedInManifest { get; set; } = new();
    }
} 