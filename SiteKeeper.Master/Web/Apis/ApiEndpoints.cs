using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.DTOs.API.Authentication;
using SiteKeeper.Shared.DTOs.API.Environment;
using SiteKeeper.Shared.DTOs.API.Operations;
using SiteKeeper.Shared.DTOs.API.PackageManagement;
using SiteKeeper.Shared.DTOs.API.Nodes;
using SiteKeeper.Shared.DTOs.API.SoftwareControl;
using SiteKeeper.Shared.DTOs.API.OfflineUpdate;
using SiteKeeper.Shared.DTOs.API.Diagnostics;
using SiteKeeper.Shared.DTOs.API.AuditLog;
using SiteKeeper.Shared.DTOs.API.Releases;
using SiteKeeper.Shared.DTOs.API.Journal;
using SiteKeeper.Shared.DTOs.Common;
using SiteKeeper.Shared.Enums;
using SiteKeeper.Shared.Security;
using SiteKeeper.Master.Web.Apis.QueryParameters;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SiteKeeper.Master.Web.Apis
{
    /// <summary>
    /// Registers Minimal API endpoints for the SiteKeeper Master application.
    /// </summary>
    /// <remarks>
    /// This static class contains extension methods to map all API endpoints defined in the Swagger specification.
    /// Endpoints are grouped by functionality and tagged accordingly for Swagger UI.
    /// Authorization is typically required for most endpoint groups.
    /// Placeholder services are injected, and their methods are expected to align with the API contracts.
    /// </remarks>
    public static partial class ApiEndpoints // Made partial for potentially splitting endpoint definitions
    {
        /// <summary>
        /// Maps all SiteKeeper API endpoints to the <see cref="WebApplication"/>.
        /// This method orchestrates the registration of all API endpoint groups by calling mapping methods
        /// defined in the various partial class files.
        /// </summary>
        /// <param name="app">The <see cref="WebApplication"/> to map endpoints to.</param>
        /// <param name="guiHostConstraint">The host string (e.g., "*:5001") to constrain all GUI APIs to.</param>
        /// <returns>The <see cref="WebApplication"/> with mapped endpoints.</returns>
        public static WebApplication MapSiteKeeperApiEndpoints(this WebApplication app, string guiHostConstraint)
        {
            var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
            var apiLogger = loggerFactory.CreateLogger("SiteKeeper.Master.ApiEndpoints");

            // Map all endpoint groups from their respective partial classes.
            // This approach keeps the startup logic clean and delegates the endpoint definitions
            // to specialized files, improving maintainability and separation of concerns.
            app.MapAuthenticationApi(guiHostConstraint);
            app.MapEnvironmentApi(guiHostConstraint);
            app.MapNodesApi(guiHostConstraint);
            app.MapAppsAndPlansApi(guiHostConstraint);
            app.MapPackagesApi(guiHostConstraint);
            app.MapOperationsApi(guiHostConstraint);
            app.MapDiagnosticsApi(guiHostConstraint);
            app.MapJournalApi(guiHostConstraint);
            app.MapAuditLogApi(guiHostConstraint);
            app.MapReleasesApi(guiHostConstraint);
            app.MapOfflineUpdateApi(guiHostConstraint);

            return app;
        }
    }

    /// <summary>
    /// Contains extension methods for HttpContext.
    /// </summary>
    public static class HttpContextExtensions
    {
        /// <summary>
        /// Retrieves the client's IP address from the <see cref="HttpContext"/>.
        /// </summary>
        /// <param name="httpContext">The current <see cref="HttpContext"/>.</param>
        /// <returns>The client's IP address string.</returns>
        /// <remarks>
        /// This is a simplified implementation. In a production environment, this method should be enhanced 
        /// to handle scenarios involving proxies (by checking the 'X-Forwarded-For' header) and other network configurations 
        /// to accurately determine the original client IP.
        /// </remarks>
        public static string GetClientIpAddress(this HttpContext httpContext) =>
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown_ip";
    }
} 