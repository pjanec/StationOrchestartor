using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.DTOs.API.OfflineUpdate;
using SiteKeeper.Shared.Security;
using System.Security.Claims;

namespace SiteKeeper.Master.Web.Apis
{
    /// <summary>
    /// Defines API endpoints related to managing offline updates for the SiteKeeper system.
    /// This is a partial class, and these endpoints are registered via the main <see cref="ApiEndpoints.MapAll(WebApplication)"/> method.
    /// </summary>
    /// <remarks>
    /// This class utilizes Minimal API features to structure endpoints under the base path <c>/api/v1/offline-update</c>.
    /// General authorization is required for this group, with specific role checks applied at individual endpoints.
    /// Endpoints interact with the <see cref="IOfflineUpdateService"/> to list available offline update sources
    /// and handle the upload of offline update packages. Actions like preparing or applying these updates
    /// would typically be initiated via the Operations API after a package is uploaded or a bundle is identified.
    /// </remarks>
    public static partial class ApiEndpoints
    {
        /// <summary>
        /// Maps all API endpoints related to discovering offline update sources and uploading offline update packages.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to which the endpoints will be mapped (typically the <see cref="WebApplication"/> instance).</param>
        /// <param name="guiHostConstraint">A hostname constraint string to apply to these endpoints, ensuring they are only accessible via specific hostnames configured for GUI/API access.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with the newly mapped offline update endpoints.</returns>
        /// <remarks>
        /// This method creates a route group for <c>/api/v1/offline-update</c> which requires general authorization.
        /// It defines endpoints for:
        /// <list type="bullet">
        ///   <item><description><c>POST /upload</c>: Uploads an offline update package file. Requires Operator privileges.</description></item>
        ///   <item><description><c>GET /sources</c>: Lists available offline update sources. Requires Observer privileges.</description></item>
        /// </list>
        /// </remarks>
        public static IEndpointRouteBuilder MapOfflineUpdateApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            var offlineUpdateGroup = app.MapGroup("/api/v1/offline-update")
                .WithTags("OfflineUpdate")
                // General authorization for the group; specific endpoints also verify roles.
                .RequireAuthorization()
                .RequireHost(guiHostConstraint);
                
            // Defines POST /api/v1/offline-update/upload
            // Handles the upload of an offline update package file.
            // Requires Operator role. Calls IOfflineUpdateService.UploadOfflinePackageAsync.
            offlineUpdateGroup.MapPost("/upload",
            /// <summary>
            /// Handles the upload of an offline update package file (typically a ZIP archive).
            /// Expects a multipart/form-data request with a file part named "file".
            /// Requires Operator or higher privileges.
            /// </summary>
            /// <param name="req">The <see cref="HttpRequest"/> object providing access to the form data.</param>
            /// <param name="offlineUpdateService">The <see cref="IOfflineUpdateService"/> for processing the uploaded package.</param>
            /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user uploading the file.</param>
            /// <returns>
            /// An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with an <see cref="OfflinePackageUploadConfirmation"/> on successful upload.
            /// Returns <see cref="Results.BadRequest(string)"/> if the request is not form data, or if the file is missing or empty.
            /// Returns <see cref="Results.Forbid()"/> if unauthorized.
            /// </returns>
            async (HttpRequest req, [FromServices] IOfflineUpdateService offlineUpdateService, ClaimsPrincipal user) =>
            {
                if (!user.IsOperatorOrHigher()) return Results.Forbid(); // Authorization check

                if (!req.HasFormContentType)
                    return Results.BadRequest("Expected a multipart/form-data content type for package upload.");

                var form = await req.ReadFormAsync();
                var file = form.Files.GetFile("file"); // "file" is the expected name of the form field for the upload

                if (file is null || file.Length == 0)
                    return Results.BadRequest("No file or an empty file was uploaded. Please provide a valid package file.");

                var username = user.GetUsername() ?? "unknown_uploader";
                var result = await offlineUpdateService.UploadOfflinePackageAsync(file, username);
                return Results.Ok(result);
            })
            .WithSummary("Upload an offline update package")
            .Accepts<IFormFile>("multipart/form-data") // Describes the expected request body format for Swagger
            .Produces<OfflinePackageUploadConfirmation>()
            .Produces<string>(StatusCodes.Status400BadRequest) // For error messages as strings
            .Produces(StatusCodes.Status403Forbidden);

            // Defines GET /api/v1/offline-update/sources
            // Retrieves a list of available offline update sources (e.g., USB drives, network shares).
            // Requires Observer role. Calls IOfflineUpdateService.ListOfflineUpdateSourcesAsync.
            offlineUpdateGroup.MapGet("/sources",
            /// <summary>
            /// Retrieves a list of available offline update sources that can be scanned for update packages.
            /// Requires Observer or higher privileges.
            /// </summary>
            /// <param name="offlineUpdateService">The <see cref="IOfflineUpdateService"/> for listing available sources.</param>
            /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
            /// <returns>
            /// An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with an <see cref="OfflineUpdateSourceListResponse"/>
            /// containing the list of discovered sources, or <see cref="Results.Forbid()"/> if unauthorized.
            /// </returns>
            async ([FromServices] IOfflineUpdateService offlineUpdateService, ClaimsPrincipal user) =>
            {
                if (!user.IsObserverOrHigher()) return Results.Forbid(); // Authorization check
                var sources = await offlineUpdateService.ListOfflineUpdateSourcesAsync();
                return Results.Ok(sources);
            }).WithSummary("Get offline update sources").Produces<OfflineUpdateSourceListResponse>();

            return app;
        }
    }
}
