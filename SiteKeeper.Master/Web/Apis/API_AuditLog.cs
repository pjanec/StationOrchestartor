using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SiteKeeper.Master;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Web.Apis.QueryParameters;
using SiteKeeper.Shared.DTOs.API.AuditLog;
using SiteKeeper.Shared.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace SiteKeeper.Master.Web.Apis
{
    /// <summary>
    /// Defines API endpoints related to accessing and querying audit logs.
    /// This is a partial class, and these endpoints are registered via the main <see cref="ApiEndpoints.MapAll(WebApplication)"/> method.
    /// </summary>
    /// <remarks>
    /// This class utilizes Minimal API features such as <see cref="RouteGroupBuilder.MapGroup(string)"/> to structure endpoints
    /// under the base path <c>/api/v1/audit-log</c>.
    /// General authorization is required for the group, and specific endpoints may have more granular role checks.
    /// Endpoints primarily interact with the <see cref="IAuditLogService"/> to retrieve audit log data.
    /// </remarks>
    public static partial class ApiEndpoints
    {
        /// <summary>
        /// Maps all API endpoints related to audit log retrieval.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to which the endpoints will be mapped (typically the <see cref="WebApplication"/> instance).</param>
        /// <param name="guiHostConstraint">A hostname constraint string to apply to these endpoints, ensuring they are only accessible via specific hostnames configured for GUI/API access.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with the newly mapped audit log endpoints.</returns>
        /// <remarks>
        /// This method creates a route group for <c>/api/v1/audit-log</c>.
        /// Currently, it defines one main endpoint:
        /// <list type="bullet">
        ///   <item><description>GET /: For querying audit log entries with pagination, filtering, and sorting. Requires at least BasicAdmin privileges.</description></item>
        /// </list>
        /// </remarks>
        public static IEndpointRouteBuilder MapAuditLogApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            var auditLogGroup = app.MapGroup("/api/v1/audit-log")
                .WithTags("AuditLog")
                // General authorization for the group; specific endpoints might have stricter checks.
                .RequireAuthorization()
                .RequireHost(guiHostConstraint);

            // Defines GET /api/v1/audit-log
            // Retrieves a paginated list of audit log entries based on query parameters.
            // Requires BasicAdmin role. Calls IAuditLogService.GetAuditLogsAsync.
            // Uses AuditLogQueryParameters for binding query string values.
            auditLogGroup.MapGet("/", async ([AsParameters] AuditLogQueryParameters queryParams, [FromServices] IAuditLogService auditLogService, ClaimsPrincipal user, [FromServices] ILogger<MasterConfig> logger) =>
            {
                // Authorization: Requires BasicAdmin or higher to view audit logs.
                if (!user.IsBasicAdminOrHigher()) return Results.Forbid();

                int page = queryParams.Page ?? 1;
                int pageSize = queryParams.PageSize ?? 50;

                (IEnumerable<AuditLogEntry> items, int totalCount) = await auditLogService.GetAuditLogsAsync(
                    queryParams.StartDate,
                    queryParams.EndDate,
                    queryParams.User,
                    queryParams.OperationType,
                    queryParams.FilterText,
                    queryParams.SortBy,
                    queryParams.SortOrder,
                    page,
                    pageSize
                );

                return Results.Ok(new PaginatedAuditLogResponse
                {
                    TotalItems = totalCount,
                    TotalPages = (totalCount > 0 && pageSize > 0) ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0,
                    CurrentPage = page,
                    PageSize = pageSize,
                    Items = items.ToList()
                });
            }).WithSummary("Get Operation Audit Log").Produces<PaginatedAuditLogResponse>();

            return app;
        }
    }
}
