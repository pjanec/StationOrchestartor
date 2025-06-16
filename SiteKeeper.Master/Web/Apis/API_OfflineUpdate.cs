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
    public static partial class ApiEndpoints
    {
        public static IEndpointRouteBuilder MapOfflineUpdateApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            var offlineUpdateGroup = app.MapGroup("/api/v1/offline-update")
                .WithTags("OfflineUpdate")
                .RequireAuthorization()
                .RequireHost(guiHostConstraint);
                
            offlineUpdateGroup.MapPost("/upload", async (HttpRequest req, [FromServices] IOfflineUpdateService offlineUpdateService, ClaimsPrincipal user) =>
            {
                if (!user.IsOperatorOrHigher()) return Results.Forbid();
                if (!req.HasFormContentType)
                    return Results.BadRequest("Expected a form content type.");

                var form = await req.ReadFormAsync();
                var file = form.Files.GetFile("file");

                if (file is null || file.Length == 0)
                    return Results.BadRequest("No file or empty file uploaded.");

                var username = user.GetUsername() ?? "unknown";
                var result = await offlineUpdateService.UploadOfflinePackageAsync(file, username);
                return Results.Ok(result);
            })
            .WithSummary("Upload an offline update package")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<OfflinePackageUploadConfirmation>();

            offlineUpdateGroup.MapGet("/sources", async ([FromServices] IOfflineUpdateService offlineUpdateService, ClaimsPrincipal user) =>
            {
                if (!user.IsObserverOrHigher()) return Results.Forbid();
                var sources = await offlineUpdateService.ListOfflineUpdateSourcesAsync();
                return Results.Ok(sources);
            }).WithSummary("Get offline update sources").Produces<OfflineUpdateSourceListResponse>();

            return app;
        }
    }
}
