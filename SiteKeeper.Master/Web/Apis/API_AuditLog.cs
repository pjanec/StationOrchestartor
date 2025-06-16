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
    /// Provides API endpoints for the audit log. This is a partial class that extends the main ApiEndpoints.
    /// The methods within this class are responsible for mapping all audit log-related endpoints.
    /// </summary>
    public static partial class ApiEndpoints
    {
        /// <summary>
        /// Maps all audit log-related endpoints.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to map endpoints to.</param>
        /// <param name="guiHostConstraint">The host string to constrain the endpoints to.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with mapped endpoints.</returns>
        public static IEndpointRouteBuilder MapAuditLogApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            var auditLogGroup = app.MapGroup("/api/v1/audit-log")
                .WithTags("AuditLog")
                .RequireAuthorization()
                .RequireHost(guiHostConstraint);

            auditLogGroup.MapGet("/", async ([AsParameters] AuditLogQueryParameters queryParams, [FromServices] IAuditLogService auditLogService, ClaimsPrincipal user, [FromServices] ILogger<MasterConfig> logger) =>
            {
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
