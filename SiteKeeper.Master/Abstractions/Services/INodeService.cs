using SiteKeeper.Shared.DTOs.API.Environment; // For NodeDetailsResponse, PackageOnNode
using SiteKeeper.Shared.DTOs.API.Nodes;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Defines the contract for a service that provides information and operations specific to individual nodes.
    /// </summary>
    public interface INodeService
    {
        /// <summary>
        /// Gets detailed information for a specific node.
        /// </summary>
        /// <remarks>
        /// The DTO for NodeDetailsResponse is not explicitly defined in the provided `web api swagger.yaml`
        /// but is mentioned in the `SiteKeeper Minimal API & SignalR Hub Handlers.md` sketch.
        /// It's assumed to be a combination of NodeSummary and additional details.
        /// For now, let's assume `SiteKeeper.Shared.DTOs.API.Environment.NodeDetailsResponse` exists or will be created.
        /// </remarks>
        Task<NodeDetailsResponse?> GetNodeDetailsAsync(string nodeName);

        /// <summary>
        /// Lists all packages installed on a specific node.
        /// </summary>
        Task<List<PackageOnNode>?> ListNodePackagesAsync(string nodeName);
    }
} 