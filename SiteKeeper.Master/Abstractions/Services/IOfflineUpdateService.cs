using Microsoft.AspNetCore.Http;
using SiteKeeper.Shared.DTOs.API.OfflineUpdate;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Defines the contract for a service that manages offline update processes for the SiteKeeper environment.
    /// This includes discovering available offline update sources, handling the upload of update packages,
    /// preparing these packages for deployment, and potentially requesting the generation or retrieval of offline update bundles.
    /// </summary>
    /// <remarks>
    /// The offline update mechanism allows the system to be updated in environments with limited or no direct internet connectivity.
    /// This service orchestrates the various steps involved in acquiring, preparing, and applying updates using pre-downloaded packages or bundles.
    /// It is typically consumed by API controllers exposing functionalities under endpoints like /api/offline-update.
    /// </remarks>
    public interface IOfflineUpdateService
    {
        /// <summary>
        /// Lists all available and configured offline update sources from which update packages or metadata can be retrieved
        /// (e.g., network shares, local directories, removable drives).
        /// This method is typically called by API controllers serving an endpoint like GET /api/offline-update/sources.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains an <see cref="OfflineUpdateSourceListResponse"/> DTO with the list of discovered sources.</returns>
        Task<OfflineUpdateSourceListResponse> ListOfflineUpdateSourcesAsync();

        /// <summary>
        /// Handles the upload of an offline update package file (e.g., a ZIP archive containing software components).
        /// The service is responsible for validating, storing, and cataloging the uploaded package.
        /// This method is typically called by API controllers serving an endpoint like POST /api/offline-update/upload.
        /// </summary>
        /// <param name="packageFile">The <see cref="IFormFile"/> instance representing the uploaded package file, usually from an HTTP multipart/form-data request.</param>
        /// <param name="uploadedByUsername">The username of the user who uploaded the package, for auditing purposes.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an <see cref="OfflinePackageUploadConfirmation"/> DTO upon successful upload.</returns>
        Task<OfflinePackageUploadConfirmation> UploadOfflinePackageAsync(IFormFile packageFile, string uploadedByUsername);

        // Placeholder for other methods related to offline updates as per Swagger:
        // Task<OperationTicketResponse> PrepareOfflineUpdateAsync(OfflineUpdatePrepareRequest request, string initiatedByUsername); // Likely triggers an operation via IMasterActionCoordinatorService
        // Task<OperationTicketResponse> RequestOfflineUpdateBundleAsync(OfflineBundleRequest request, string initiatedByUsername); // Likely triggers an operation
    }
} 