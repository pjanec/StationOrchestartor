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
    /// Placeholder implementation of the <see cref="IPlanControlService"/> interface.
    /// Provides simulated data and behavior for application plan management functionalities for development and testing.
    /// </summary>
    /// <remarks>
    /// This service returns a predefined list of application plans and includes basic filtering and sorting capabilities.
    /// In a real production environment, this service would interact with actual plan management logic and
    /// would likely coordinate plan-based actions (start, stop, restart) via the <see cref="IMasterActionCoordinatorService"/>.
    /// </remarks>
    public class PlaceholderPlanControlService : IPlanControlService
    {
        private readonly ILogger<PlaceholderPlanControlService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaceholderPlanControlService"/> class.
        /// </summary>
        /// <param name="logger">The logger for recording service activity and placeholder notifications.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="logger"/> is null.</exception>
        public PlaceholderPlanControlService(ILogger<PlaceholderPlanControlService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Placeholder implementation for listing defined application plans and their current aggregated statuses.
        /// Returns a predefined list of <see cref="PlanInfo"/> DTOs and applies basic filtering and sorting.
        /// </summary>
        /// <param name="filterText">Optional text used to filter plans by Id, Name, or Description (case-insensitive contains).</param>
        /// <param name="sortBy">Optional field name to sort the results by (e.g., "name", "status"). Defaults to sorting by name if not specified or invalid.</param>
        /// <param name="sortOrder">Optional sort order ("asc" or "desc"). Defaults to ascending if not "desc".</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="PlanListResponse"/>
        /// with a list of predefined <see cref="PlanInfo"/> DTOs, potentially filtered and sorted.</returns>
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