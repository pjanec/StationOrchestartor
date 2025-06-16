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
	public class PlaceholderNodeService : INodeService
	{
		// Placeholder data store  
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

		public Task<NodeDetailsResponse?> GetNodeDetailsAsync( string nodeName )
		{
			var node = _nodesDetails.FirstOrDefault( n => n.NodeName.Equals( nodeName, StringComparison.OrdinalIgnoreCase ) );
			return Task.FromResult( node );
		}

		public Task<List<PackageOnNode>?> ListNodePackagesAsync( string nodeName )
		{
			var node = _nodesDetails.FirstOrDefault( n => n.NodeName.Equals( nodeName, StringComparison.OrdinalIgnoreCase ) );

			// fake packages for the node
			var packages = new List<PackageOnNode>
			{
				new PackageOnNode { PackageName = "ProcessingUnit-PH", CurrentVersion = "1.1", Type = PackageType.Core },
				new PackageOnNode { PackageName = "LegacyDriver-PH", CurrentVersion = "0.9", Type = PackageType.Optional }
			};

			return Task.FromResult( packages );
		}
	}
} 