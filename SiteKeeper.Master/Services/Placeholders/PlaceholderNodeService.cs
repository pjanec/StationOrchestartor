using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.DTOs.API.Environment;
using SiteKeeper.Shared.DTOs.API.Nodes;
using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Services.Placeholders
{
    /// <summary>
    /// Placeholder implementation of the <see cref="INodeService"/> interface.
    /// Provides simulated node-specific data for development and testing purposes.
    /// </summary>
    /// <remarks>
    /// This service returns predefined data for node details and package lists.
    /// The <see cref="GetNodeDetailsAsync"/> method attempts to find a node from an internal static list.
    /// The <see cref="ListNodePackagesAsync"/> method returns a fixed list of packages for any valid node found,
    /// primarily for demonstrating the API structure.
    /// </remarks>
    public class PlaceholderNodeService : INodeService
	{
		// Placeholder data store for node details
		private readonly List<NodeDetailsResponse> _nodesDetails = new List<NodeDetailsResponse>
	   {
		   new NodeDetailsResponse
		   {
			   NodeName = "SimServer-PH",
			   AgentStatus = AgentStatus.Online,
			   HealthSummary = NodeHealthSummary.OK,
			   CpuUsagePercent = 10,
			   RamUsagePercent = 20,
			   OsInfo = "Windows Server 2022 Placeholder",
			   AgentVersion = "1.0.0-ph",
			   LastSeen = DateTime.UtcNow.AddMinutes(-1)
		   },
		   new NodeDetailsResponse
		   {
			   NodeName = "IOS1-PH",
			   AgentStatus = AgentStatus.Online,
			   HealthSummary = NodeHealthSummary.Warning,
			   CpuUsagePercent = 15,
			   RamUsagePercent = 25,
			   OsInfo = "Custom Linux Distro Placeholder",
			   AgentVersion = "1.0.1-ph",
			   LastSeen = DateTime.UtcNow.AddMinutes(-2)
		   }
	   };

        /// <summary>
        /// Placeholder implementation for retrieving detailed information for a specific node.
        /// Searches a predefined list for a node matching the provided <paramref name="nodeName"/>.
        /// </summary>
        /// <param name="nodeName">The unique name of the node to retrieve details for.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a <see cref="NodeDetailsResponse"/>
        /// DTO if a matching node is found in the predefined list; otherwise, null.
        /// </returns>
        public Task<NodeDetailsResponse?> GetNodeDetailsAsync( string nodeName )
		{
			var node = _nodesDetails.FirstOrDefault( n => n.NodeName.Equals( nodeName, StringComparison.OrdinalIgnoreCase ) );
			return Task.FromResult( node );
		}

        /// <summary>
        /// Placeholder implementation for listing software packages installed on a specific node.
        /// Returns a static, predefined list of <see cref="PackageOnNode"/> DTOs if the node exists in the predefined list.
        /// </summary>
        /// <param name="nodeName">The unique name of the node (used to check if node exists in placeholder data, but the returned package list is static).</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a predefined list
        /// of <see cref="PackageOnNode"/> DTOs if the node is found; otherwise, null (or an empty list, behavior may vary slightly based on desired simulation).
        /// For this implementation, it returns a static list if the node is "known".
        /// </returns>
        public Task<List<PackageOnNode>?> ListNodePackagesAsync( string nodeName )
		{
			var node = _nodesDetails.FirstOrDefault( n => n.NodeName.Equals( nodeName, StringComparison.OrdinalIgnoreCase ) );
            if (node == null)
            {
                return Task.FromResult<List<PackageOnNode>?>(null); // Or an empty list: Task.FromResult(new List<PackageOnNode>())
            }

			// Static fake packages for any valid node in this placeholder
			var packages = new List<PackageOnNode>
			{
				new PackageOnNode { PackageName = "ProcessingUnit-PH", CurrentVersion = "1.1", Type = PackageType.Core },
				new PackageOnNode { PackageName = "LegacyDriver-PH", CurrentVersion = "0.9", Type = PackageType.Optional }
			};

			return Task.FromResult<List<PackageOnNode>?>(packages);
		}
	}
} 