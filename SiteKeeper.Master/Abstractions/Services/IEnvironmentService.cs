using SiteKeeper.Shared.DTOs.API.Environment;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Defines the contract for a service that provides information about the overall environment.
    /// </summary>
    public interface IEnvironmentService
    {
        /// <summary>
        /// Gets the overall status of the environment.
        /// </summary>
        Task<EnvironmentStatusResponse> GetEnvironmentStatusAsync();

        /// <summary>
        /// Lists all nodes in the environment, with optional filtering and sorting.
        /// </summary>
        Task<List<NodeSummary>> ListEnvironmentNodesAsync(string? filterText, string? sortBy, string? sortOrder);

        /// <summary>
        /// Gets the pure environment manifest.
        /// </summary>
        Task<PureManifest> GetEnvironmentManifestAsync();

        /// <summary>
        /// Gets a summary of recent journal entries for the environment dashboard.
        /// </summary>
        /// <param name="limit">The maximum number of recent changes to return.</param>
        /// <returns>A list of <see cref="JournalEntrySummary"/> objects representing recent changes.</returns>
        /// <remarks>Corresponds to the GET /environment/recent-changes endpoint.</remarks>
        Task<List<JournalEntrySummary>> GetRecentChangesAsync(int limit);
    }
} 