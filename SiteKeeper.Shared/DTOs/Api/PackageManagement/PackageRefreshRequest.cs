using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SiteKeeper.Shared.DTOs.API.PackageManagement
{
    /// <summary>
    /// Represents the request to refresh one or more packages on specified nodes or across the environment.
    /// </summary>
    /// <remarks>
    /// This DTO aligns with the 'PackageRefreshRequest' schema in `web api swagger.yaml`.
    /// Refreshing a package might involve actions like re-downloading from a source, 
    /// re-applying configurations, or clearing cached states.
    /// </remarks>
    public class PackageRefreshRequest
    {
        /// <summary>
        /// Gets or sets the list of specific package names to refresh.
        /// If this list is null or empty and <see cref="AllRefreshableInEnvironment"/> is true, 
        /// then all packages defined as refreshable in the environment are targeted.
        /// If this list is provided, only these packages will be considered for refresh, 
        /// potentially further filtered by <see cref="NodeNames"/>.
        /// </summary>
        /// <example>["CoreApp-bin", "CoreApp-conf"]</example>
        [JsonPropertyName("packageNames")]
        public List<string>? PackageNames { get; set; }

        /// <summary>
        /// Gets or sets the list of specific node names on which to refresh the packages.
        /// If this list is null or empty, the refresh operation applies to all nodes 
        /// where the targeted packages (either specified in <see cref="PackageNames"/> or all refreshable packages) 
        /// are relevant or installed.
        /// </summary>
        /// <example>["SIMSERVER", "IOS1"]</example>
        [JsonPropertyName("nodeNames")]
        public List<string>? NodeNames { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to refresh all packages defined as refreshable in the environment.
        /// If true, the operation targets all such packages. If <see cref="PackageNames"/> is also provided, 
        /// the specific service implementation will determine if it acts as a filter or if <see cref="PackageNames"/> is ignored.
        /// Defaults to false if not specified by the client, behaviorally.
        /// </summary>
        /// <example>false</example>
        [JsonPropertyName("allRefreshableInEnvironment")]
        public bool? AllRefreshableInEnvironment { get; set; }
    }
} 