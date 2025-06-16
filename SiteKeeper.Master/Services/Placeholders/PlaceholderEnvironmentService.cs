using Microsoft.Extensions.Logging;
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
    /// Placeholder implementation of the <see cref="IEnvironmentService"/> interface.
    /// Provides simulated environment data for development and testing purposes.
    /// </summary>
    /// <remarks>
    /// This service returns pre-defined or generated data for environment status, node lists,
    /// manifests, and recent changes. It does not interact with any real environment components.
    /// In a production environment, this would be replaced with a concrete implementation
    /// that gathers live data from the managed environment.
    /// This service combines implementations and ensures all methods from <see cref="IEnvironmentService"/> are present.
    /// </remarks>
    public class PlaceholderEnvironmentService : IEnvironmentService
    {
        private readonly ILogger<PlaceholderEnvironmentService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaceholderEnvironmentService"/> class.
        /// </summary>
        /// <param name="logger">The logger for this service.</param>
        public PlaceholderEnvironmentService(ILogger<PlaceholderEnvironmentService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public Task<EnvironmentStatusResponse> GetEnvironmentStatusAsync()
        {
            _logger.LogInformation("Placeholder: Getting environment status.");
            var status = new EnvironmentStatusResponse
            {
                EnvironmentName = "MyProdEnv-SiteA-Placeholder",
                CurrentVersionId = "1.2.3-ph",
                SystemSoftwareStatus = SystemSoftwareOverallStatus.PartiallyRunning,
                AppsRunningSummary = new AppsRunningSummaryInfo { Running = 8, Total = 15 },
                NodesSummary = new NodesSummaryInfo { Total = 3, Online = 2, Offline = 1 },
                DiagnosticsSummary = new DiagnosticsSummaryInfo { Status = DiagnosticsOverallStatus.Warnings },
                CurrentOperation = new OngoingOperationSummary
                {
                    Id = "op-placeholder-123",
                    Name = "Placeholder Environment Scan",
                    Status = OngoingOperationStatus.InProgress,
                    ProgressPercent = 50,
                    StartTime = DateTime.UtcNow.AddMinutes(-10),
                    LatestLogSnippet = "[INFO] Scanning node IOS1..."
                },
                LastCompletedOperation = new CompletedOperationSummary
                {
                    Id = "op-placeholder-122",
                    Name = "Placeholder Backup",
                    Status = CompletedOperationFinalStatus.Success,
                    CompletedAt = DateTime.UtcNow.AddHours(-1),
                    DurationSeconds = 180
                }
            };
            return Task.FromResult(status);
        }

        /// <inheritdoc />
        /// <remarks>
        /// The NodeSummary data returned by this placeholder reflects the current structure of 
        /// SiteKeeper.Shared/DTOs/API/Environment/NodeSummary.cs. This means fields like IpAddress, 
        /// StationNumber, and IsMaster (which are in the Swagger definition) are currently omitted 
        /// because they are not present in the C# DTO.
        /// </remarks>
        public Task<List<NodeSummary>> ListEnvironmentNodesAsync(string? filterText, string? sortBy, string? sortOrder)
        {
            _logger.LogInformation("Placeholder: Listing environment nodes. Filter: {FilterText}, SortBy: {SortBy}, SortOrder: {SortOrder}", filterText, sortBy, sortOrder);
            var nodes = new List<NodeSummary>
            {
                new NodeSummary
                {
                    NodeName = "SIMSERVER-PH",
                    AgentStatus = AgentStatus.Online,
                    HealthSummary = NodeHealthSummary.OK,
                    CpuUsagePercent = 25,
                    RamUsagePercent = 40,
                },
                new NodeSummary
                {
                    NodeName = "IOS1-PH",
                    AgentStatus = AgentStatus.Online,
                    HealthSummary = NodeHealthSummary.Issues,
                    CpuUsagePercent = 60,
                    RamUsagePercent = 75,
                },
                new NodeSummary
                {
                    NodeName = "IOS2-PH",
                    AgentStatus = AgentStatus.Offline,
                    HealthSummary = NodeHealthSummary.Unknown,
                    CpuUsagePercent = null,
                    RamUsagePercent = null,
                }
            };

            // Simple placeholder for filtering
            if (!string.IsNullOrWhiteSpace(filterText))
            {
                var filterTextLower = filterText.ToLowerInvariant();
                nodes = nodes.Where(n =>
                    (n.NodeName != null && n.NodeName.ToLowerInvariant().Contains(filterTextLower))
                ).ToList();
            }
            // Placeholder for sorting - not implemented in detail for placeholder

            return Task.FromResult(nodes);
        }

        /// <inheritdoc />
        public Task<PureManifest> GetEnvironmentManifestAsync()
        {
            _logger.LogInformation("Placeholder: Getting environment manifest.");
            var manifest = new PureManifest
            {
                EnvironmentName = "MyProdEnv-SiteA-Placeholder",
                VersionId = "1.2.3-ph",
                AppliedAt = DateTime.UtcNow.AddDays(-7),
                Nodes = new List<NodeInManifest>
                {
                    new NodeInManifest
                    {
                        NodeName = "SIMSERVER-PH",
                        Packages = new List<PackageInManifest>
                        {
                            new PackageInManifest { PackageName = "CoreApp-bin", OriginalVersion = "1.5.0", Type = PackageType.Core },
                            new PackageInManifest { PackageName = "CoreApp-conf", OriginalVersion = "1.2.0", Type = PackageType.Core }
                        }
                    },
                    new NodeInManifest
                    {
                        NodeName = "IOS1-PH",
                        Packages = new List<PackageInManifest>
                        {
                            new PackageInManifest { PackageName = "CoreApp-bin", OriginalVersion = "1.5.0", Type = PackageType.Core },
                            new PackageInManifest { PackageName = "OptionalToolA-bin", OriginalVersion = "1.0.0", Type = PackageType.Optional }
                        }
                    }
                },
                OptionalPackagesDefinedInManifest = new List<PackageVersionInfo>
                {
                    new PackageVersionInfo { PackageName = "OptionalToolA-bin", OriginalVersion = "1.0.0" }
                }
            };
            return Task.FromResult(manifest);
        }

        /// <inheritdoc />
        public Task<List<JournalEntrySummary>> GetRecentChangesAsync(int limit)
        {
            _logger.LogInformation("Placeholder: Getting recent changes with limit {Limit}.", limit);

            var allChanges = new List<JournalEntrySummary>
            {
                new JournalEntrySummary
                {
                    JournalRecordId = "journal-ph-001",
                    Timestamp = DateTime.UtcNow.AddMinutes(-5),
                    OperationType = "System Software Start",
                    Summary = "System software started by admin.",
                    Outcome = "Success"
                },
                new JournalEntrySummary
                {
                    JournalRecordId = "journal-ph-002",
                    Timestamp = DateTime.UtcNow.AddHours(-1),
                    OperationType = "Node Restart",
                    Summary = "Node IOS1-PH restarted due to maintenance.",
                    Outcome = "InProgress"
                },
                new JournalEntrySummary
                {
                    JournalRecordId = "journal-ph-003",
                    Timestamp = DateTime.UtcNow.AddHours(-2),
                    OperationType = "Environment Backup",
                    Summary = "Daily environment backup initiated.",
                    Outcome = "Success"
                },
                new JournalEntrySummary
                {
                    JournalRecordId = "journal-ph-004",
                    Timestamp = DateTime.UtcNow.AddDays(-1),
                    OperationType = "Package Update",
                    Summary = "CoreApp-bin updated to 1.5.1 on SIMSERVER-PH.",
                    Outcome = "Failure"
                },
                new JournalEntrySummary
                {
                    JournalRecordId = "journal-ph-005",
                    Timestamp = DateTime.UtcNow.AddDays(-1).AddHours(-1),
                    OperationType = "Security Scan",
                    Summary = "Security scan completed on all nodes.",
                    Outcome = "Success"
                },
                 new JournalEntrySummary
                {
                    JournalRecordId = "journal-ph-006",
                    Timestamp = DateTime.UtcNow.AddMinutes(-30),
                    OperationType = "Environment Update",
                    Summary = "Online update to version 1.2.4-ph initiated.",
                    Outcome = "InProgress"
                },
                new JournalEntrySummary
                {
                    JournalRecordId = "journal-ph-007",
                    Timestamp = DateTime.UtcNow.AddHours(-5),
                    OperationType = "User Login",
                    Summary = "User \'\'\'operator_user\'\'\' logged in.",
                    Outcome = "Success"
                },
                new JournalEntrySummary
                {
                    JournalRecordId = "journal-ph-008",
                    Timestamp = DateTime.UtcNow.AddDays(-2),
                    OperationType = "Configuration Change",
                    Summary = "Network settings updated for node IOS2-PH.",
                    Outcome = "Cancelled"
                }
            };

            // Order by timestamp descending and take the specified limit
            var recentChanges = allChanges.OrderByDescending(j => j.Timestamp).Take(limit).ToList();

            return Task.FromResult(recentChanges);
        }
    }
} 