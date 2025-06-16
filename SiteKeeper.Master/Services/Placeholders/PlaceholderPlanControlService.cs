using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.DTOs.API.SoftwareControl;
using SiteKeeper.Shared.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace SiteKeeper.Master.Services.Placeholders
{
    /// <summary>
    /// Placeholder implementation for <see cref="IPlanControlService"/>.
    /// Provides example data for plan listings and simulates plan control operations.
    /// </summary>
    /// <remarks>
    /// This service is intended for development and testing purposes.
    /// In a production environment, this would interact with actual plan management logic,
    /// potentially coordinating with an <see cref="IOperationCoordinatorService"/> for actions.
    /// </remarks>
    public class PlaceholderPlanControlService : IPlanControlService
    {
        private readonly ILogger<PlaceholderPlanControlService> _logger;

        public PlaceholderPlanControlService(ILogger<PlaceholderPlanControlService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Lists all defined application plans with their current aggregated statuses (Placeholder).
        /// </summary>
        public Task<PlanListResponse> ListPlansAsync(string? filterText, string? sortBy, string? sortOrder)
        {
            _logger.LogInformation("Listing plans with filter: '{FilterText}', sortBy: '{SortBy}', sortOrder: '{SortOrder}' (Placeholder).", filterText, sortBy, sortOrder);

            var allPlans = new List<PlanInfo>
            {
                new PlanInfo
                {
                    Id = "plan-core-services-ph",
                    Name = "Core Services (Placeholder)",
                    Description = "Starts all essential core applications and backend services.",
                    Status = PlanOperationalStatus.Running, 
                },
                new PlanInfo
                {
                    Id = "plan-data-pipeline-ph",
                    Name = "Data Processing Pipeline (Placeholder)",
                    Description = "Manages the data ingestion and processing workers.",
                    Status = PlanOperationalStatus.PartiallyRunning,
                },
                new PlanInfo
                {
                    Id = "plan-aux-tools-ph",
                    Name = "Auxiliary Tools (Placeholder)",
                    Description = "Optional auxiliary tools and utilities.",
                    Status = PlanOperationalStatus.NotRunning,
                }
            };

            IEnumerable<PlanInfo> filteredPlans = allPlans;

            if (!string.IsNullOrWhiteSpace(filterText))
            {
                filteredPlans = allPlans.Where(p => 
                    (p.Name != null && p.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase)) ||
                    (p.Id != null && p.Id.Contains(filterText, StringComparison.OrdinalIgnoreCase)) ||
                    (p.Description != null && p.Description.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                );
            }
            
            // Basic sorting example (can be expanded)
            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                bool descending = "desc".Equals(sortOrder, StringComparison.OrdinalIgnoreCase);
                switch (sortBy.ToLowerInvariant())
                {
                    case "name":
                        filteredPlans = descending ? filteredPlans.OrderByDescending(p => p.Name) : filteredPlans.OrderBy(p => p.Name);
                        break;
                    case "status":
                        filteredPlans = descending ? filteredPlans.OrderByDescending(p => p.Status) : filteredPlans.OrderBy(p => p.Status);
                        break;
                    default:
                        _logger.LogWarning("Unsupported sortBy parameter: {SortBy}", sortBy);
                        break;
                }
            }
            else
            {
                // Default sort by name if nothing specified
                 filteredPlans = filteredPlans.OrderBy(p => p.Name);
            }

            var response = new PlanListResponse
            {
                Plans = filteredPlans.ToList()
            };

            return Task.FromResult(response);
        }
    }
} 