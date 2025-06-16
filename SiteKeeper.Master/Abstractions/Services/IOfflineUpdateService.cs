using Microsoft.AspNetCore.Http;
using SiteKeeper.Shared.DTOs.API.OfflineUpdate;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Defines the contract for a service that manages offline update processes.
    /// This includes listing update sources, handling package uploads, preparing updates, and requesting update bundles.
    /// </summary>
    /// <remarks>
    /// The offline update mechanism allows the system to be updated in environments with limited or no direct internet connectivity.
    /// This service orchestrates the various steps involved in acquiring, preparing, and applying updates using pre-downloaded packages or bundles.
    /// </remarks>
    public interface IOfflineUpdateService
    {
        /// <summary>
        /// Lists all available and configured offline update sources.
        /// Offline update sources are locations from which update packages or metadata can be retrieved (e.g., network shares, local directories).
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains an <see cref="OfflineUpdateSourceListResponse"/> with the list of sources.</returns>
        Task<OfflineUpdateSourceListResponse> ListOfflineUpdateSourcesAsync();

        /// <summary>
        /// Handles the upload of an offline update package file.
        /// </summary>
        /// <param name="packageFile">The uploaded package file.</param>
        /// <param name="uploadedByUsername">The username of the user who uploaded the package.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an <see cref="OfflinePackageUploadConfirmation"/>.</returns>
        Task<OfflinePackageUploadConfirmation> UploadOfflinePackageAsync(IFormFile packageFile, string uploadedByUsername);

        // Placeholder for other methods related to offline updates as per Swagger:
        // Task<OperationTicketResponse> PrepareOfflineUpdateAsync(OfflineUpdatePrepareRequest request, string initiatedByUsername);
        // Task<OperationTicketResponse> RequestOfflineUpdateBundleAsync(OfflineBundleRequest request, string initiatedByUsername);
    }
} 