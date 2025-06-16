using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SiteKeeper.Shared.DTOs.API.PackageManagement
{
    /// <summary>
    /// Represents the request body for initiating various package-related operations (e.g., revert deviations, refresh) on target nodes.
    /// </summary>
    /// <remarks>
    /// This DTO allows clients to specify a package and the target nodes for actions like reverting package versions
    /// to match a manifest or refreshing package contents/configurations.
    /// It's used for operations like <c>OperationType.PackageRevertDeviations</c> or <c>OperationType.PackageRefresh</c>.
    /// Based on the PackageOperationRequest schema in `web api swagger.yaml` and detailed in
    /// "SiteKeeper - API - Detailed DTO Definitions.md".
    /// </remarks>
    public class PackageOperationRequest
    {
        /// <summary>
        /// The name of the package on which the operation is to be performed.
        /// </summary>
        /// <example>"MainApplicationSuite"</example>
        [Required]
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// Optional list of specific node names where the package operation should be applied.
        /// If null or empty, the operation may be interpreted as applying to all applicable nodes
        /// where the package is installed or managed, depending on server-side logic and operation type.
        /// </summary>
        /// <example>["AppServer01", "DatabaseNode"]</example>
        public List<string>? NodeNames { get; set; }
    }
} 