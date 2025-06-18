using SiteKeeper.Shared.DTOs.API.Environment; // For NodeDetailsResponse, PackageOnNode
using SiteKeeper.Shared.DTOs.API.Nodes;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Defines the contract for a service that provides detailed information and facilitates operations specific to individual managed nodes.
    /// </summary>
    /// <remarks>
    /// This service is responsible for retrieving in-depth details about specific nodes,
    /// such as their configuration, installed software packages, and potentially runtime status aspects
    /// not covered by general health monitoring in <see cref="INodeHealthMonitorService"/>.
    /// It is primarily consumed by API controllers serving node-specific informational endpoints (e.g., GET /api/nodes/{nodeName}).
    /// Operations on nodes (like restart, shutdown) are typically coordinated by <see cref="IMasterActionCoordinatorService"/>.
    /// </remarks>
    public interface INodeService
    {
        /// <summary>
        /// Gets detailed information for a specific node, extending beyond the summary provided by <see cref="IEnvironmentService.ListEnvironmentNodesAsync"/>.
        /// This method is typically called by API controllers serving an endpoint like GET /api/nodes/{nodeName}.
        /// </summary>
        /// <param name="nodeName">The unique name of the node to query.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a <see cref="NodeDetailsResponse"/>
        /// DTO if the node is found; otherwise, null.
        /// </returns>
        /// <remarks>
        /// The DTO <see cref="NodeDetailsResponse"/> is assumed to combine <see cref="NodeSummary"/> with additional details
        /// like OS info, agent version, and last seen time.
        /// </remarks>
        Task<NodeDetailsResponse?> GetNodeDetailsAsync(string nodeName);

        /// <summary>
        /// Lists all software packages currently installed on a specific node.
        /// This method is typically called by API controllers serving an endpoint like GET /api/nodes/{nodeName}/packages.
        /// </summary>
        /// <param name="nodeName">The unique name of the node whose packages are to be listed.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a list of <see cref="PackageOnNode"/>
        /// DTOs if the node is found and package information can be retrieved; otherwise, null or an empty list.
        /// The information might be retrieved by querying the agent on the node directly or from a recently cached state.
        /// </returns>
        Task<List<PackageOnNode>?> ListNodePackagesAsync(string nodeName);
    }
} 