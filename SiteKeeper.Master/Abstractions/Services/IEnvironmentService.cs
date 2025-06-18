using SiteKeeper.Shared.DTOs.API.Environment;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Defines the contract for a service that provides aggregated information about the overall state
    /// and configuration of the managed environment.
    /// </summary>
    /// <remarks>
    /// This service is responsible for composing a snapshot of the environment's current status,
    /// including summaries of node health, application states, the active manifest, and recent operational activity.
    /// It typically sources data from other services such as <see cref="IAgentConnectionManagerService"/>,
    /// <see cref="IJournalService"/>, and configuration management components.
    /// The methods are primarily consumed by API controllers serving dashboard or environment overview endpoints.
    /// </remarks>
    public interface IEnvironmentService
    {
        /// <summary>
        /// Gets the overall status of the environment, providing a comprehensive snapshot for dashboards.
        /// This method is typically called by API controllers serving an endpoint like GET /api/environment/status.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains an <see cref="EnvironmentStatusResponse"/> DTO.</returns>
        Task<EnvironmentStatusResponse> GetEnvironmentStatusAsync();

        /// <summary>
        /// Retrieves a list of all nodes within the environment, along with summary information for each.
        /// This method is typically called by API controllers serving an endpoint like GET /api/environment/nodes.
        /// </summary>
        /// <param name="filterText">Optional text to filter nodes by (e.g., name, role, status).</param>
        /// <param name="sortBy">Optional field name to sort the results by (e.g., "nodeName", "agentStatus").</param>
        /// <param name="sortOrder">Optional sort order ("asc" or "desc").</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of <see cref="NodeSummary"/> DTOs.</returns>
        Task<List<NodeSummary>> ListEnvironmentNodesAsync(string? filterText, string? sortBy, string? sortOrder);

        /// <summary>
        /// Gets the currently active "pure" environment manifest, which defines the desired state of the environment.
        /// The manifest is typically sourced from configuration files or a dedicated configuration store.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="PureManifest"/> DTO.</returns>
        Task<PureManifest> GetEnvironmentManifestAsync();

        /// <summary>
        /// Gets a summary of recent journal entries, typically for display on an environment dashboard.
        /// This method likely calls <see cref="IJournalService"/> to fetch the relevant entries.
        /// </summary>
        /// <param name="limit">The maximum number of recent journal entries to return.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of <see cref="JournalEntrySummary"/> objects.</returns>
        /// <remarks>Corresponds to the GET /api/environment/recent-changes endpoint.</remarks>
        Task<List<JournalEntrySummary>> GetRecentChangesAsync(int limit);
    }
} 