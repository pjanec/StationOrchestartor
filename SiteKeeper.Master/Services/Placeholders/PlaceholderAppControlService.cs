using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.DTOs.API.SoftwareControl;
using SiteKeeper.Shared.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace SiteKeeper.Master.Services.Placeholders
{
    /// <summary>
    /// Placeholder implementation of the <see cref="IAppControlService"/>.
    /// Provides a predefined list of application statuses for development and testing purposes.
    /// </summary>
    /// <remarks>
    /// This service simulates the behavior of a real application control service by returning
    /// a static list of <see cref="AppStatusInfo"/> objects. It includes basic
    /// filtering (by ID, AppName, NodeName, Description, PlanName) and sorting capabilities
    /// to mimic interactions with a live service.
    /// </remarks>
    public class PlaceholderAppControlService : IAppControlService
    {
        /// <summary>
        /// Placeholder implementation for listing manageable applications and their current statuses.
        /// Returns a predefined list of <see cref="AppStatusInfo"/> objects and applies basic filtering and sorting.
        /// </summary>
        /// <param name="filterText">Optional text used to filter the application list.
        /// The filter is applied to Id, AppName, NodeName, Description, and PlanName fields.</param>
        /// <param name="sortBy">Optional field name to sort the results by (e.g., "id", "appname", "nodename").</param>
        /// <param name="sortOrder">Optional sort order ("asc" or "desc").</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of predefined <see cref="AppStatusInfo"/> DTOs, potentially filtered and sorted.</returns>
        public Task<List<AppStatusInfo>> ListAppsAsync(string? filterText, string? sortBy, string? sortOrder)
        {
            // This placeholder service now populates the AppStatusInfo DTO according to the
            // conformant definition, which includes Id (NodeName.AppName), NodeName, AppName,
            // Status, Description, PlanName, StatusAgeSeconds, and ExitCode.
            // Properties like IsEnabled and a list of Nodes have been removed from AppStatusInfo.
            var apps = new List<AppStatusInfo>
            {
                new AppStatusInfo
                {
                    Id = "SimServer-PH.CoreAppServer-PH", // Composite ID: NodeName.AppName
                    NodeName = "SimServer-PH",
                    AppName = "CoreAppServer-PH",
                    Description = "Main business logic server application.",
                    Status = AppOperationalStatus.Running,
                    PlanName = "CoreServices-PH",
                    StatusAgeSeconds = 3600, // Example: 1 hour
                    ExitCode = null
                },
                new AppStatusInfo
                {
                    Id = "SimServer-PH.MonitoringService-PH", // Composite ID
                    NodeName = "SimServer-PH",
                    AppName = "MonitoringService-PH",
                    Description = "Collects and reports system metrics.",
                    Status = AppOperationalStatus.Stopped,
                    PlanName = "Monitoring-PH",
                    StatusAgeSeconds = 86400, // Example: 1 day
                    ExitCode = "0"
                },
                new AppStatusInfo
                {
                    Id = "IOS1-PH.DataProcessingWorker-PH", // Composite ID
                    NodeName = "IOS1-PH",
                    AppName = "DataProcessingWorker-PH",
                    Description = "Handles background data processing tasks.",
                    Status = AppOperationalStatus.Error,
                    PlanName = "DataPipeline-PH",
                    StatusAgeSeconds = 300, // Example: 5 minutes
                    ExitCode = "-1"
                }
            };

            if (!string.IsNullOrWhiteSpace(filterText))
            {
                apps = apps.Where(a =>
                    a.Id.Contains(filterText, StringComparison.OrdinalIgnoreCase) || // Search by composite Id
                    a.AppName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                    a.NodeName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                    (a.Description != null && a.Description.Contains(filterText, StringComparison.OrdinalIgnoreCase)) ||
                    (a.PlanName != null && a.PlanName.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }
            // Basic sorting example (can be expanded)
            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                switch (sortBy.ToLowerInvariant())
                {
                    case "id":
                        apps = sortOrder?.ToLowerInvariant() == "desc" ? apps.OrderByDescending(a => a.Id).ToList() : apps.OrderBy(a => a.Id).ToList();
                        break;
                    case "appname":
                        apps = sortOrder?.ToLowerInvariant() == "desc" ? apps.OrderByDescending(a => a.AppName).ToList() : apps.OrderBy(a => a.AppName).ToList();
                        break;
                    case "nodename":
                        apps = sortOrder?.ToLowerInvariant() == "desc" ? apps.OrderByDescending(a => a.NodeName).ToList() : apps.OrderBy(a => a.NodeName).ToList();
                        break;
                    // Add other sortable properties as needed
                }
            }

            return Task.FromResult(apps);
        }
    }
} 