using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Web.Apis.QueryParameters;
using SiteKeeper.Shared.DTOs.API.Journal;
using SiteKeeper.Shared.DTOs.Common;
using SiteKeeper.Shared.Security;
using System;
using System.Linq;
using System.Security.Claims;

namespace SiteKeeper.Master.Web.Apis
{
    /// <summary>
    /// Defines API endpoints related to accessing and querying the system's operational and change journals.
    /// This is a partial class, and these endpoints are registered via the main <see cref="ApiEndpoints.MapAll(WebApplication)"/> method.
    /// </summary>
    /// <remarks>
    /// This class utilizes Minimal API features to structure endpoints under the base path <c>/api/v1/journal</c>.
    /// General authorization is required for this group, with specific endpoints typically requiring at least Observer privileges.
    /// Endpoints interact primarily with the <see cref="IJournalService"/> to retrieve journal data.
    /// </remarks>
    public static partial class ApiEndpoints
    {
        /// <summary>
        /// Maps all API endpoints related to accessing the system journal.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to which the endpoints will be mapped (typically the <see cref="WebApplication"/> instance).</param>
        /// <param name="guiHostConstraint">A hostname constraint string to apply to these endpoints, ensuring they are only accessible via specific hostnames configured for GUI/API access.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with the newly mapped journal endpoints.</returns>
        /// <remarks>
        /// This method creates a route group for <c>/api/v1/journal</c> which requires general authorization.
        /// It defines endpoints for:
        /// <list type="bullet">
        ///   <item><description><c>GET /</c>: Retrieves a paginated list of journal entries (summaries). Requires Observer privileges.</description></item>
        ///   <item><description><c>GET /{journalRecordId}</c>: Retrieves detailed information for a specific journal entry. Requires Observer privileges.</description></item>
        /// </list>
        /// </remarks>
        public static IEndpointRouteBuilder MapJournalApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            var journalGroup = app.MapGroup("/api/v1/journal")
                                  .WithTags("Journal")
                                   // General authorization for the group; specific endpoints also verify roles.
                                  .RequireAuthorization()
                                  .RequireHost(guiHostConstraint);

            // Defines GET /api/v1/journal
            // Retrieves a paginated list of journal entry summaries based on query parameters.
            // Requires Observer role. Calls IJournalService.ListJournalEntriesAsync.
            // Uses JournalQueryParameters for binding query string values.
            journalGroup.MapGet("/",
            /// <summary>
            /// Retrieves a paginated list of journal entry summaries, allowing filtering and sorting.
            /// Requires Observer or higher privileges.
            /// </summary>
            /// <param name="queryParams">The <see cref="JournalQueryParameters"/> binding to query string values for filtering, sorting, and pagination.</param>
            /// <param name="journalService">The <see cref="IJournalService"/> for fetching journal entries.</param>
            /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
            /// <returns>
            /// An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with a <see cref="PaginatedJournalResponse"/>
            /// containing the list of entries and pagination details, or <see cref="Results.Forbid()"/> if unauthorized.
            /// </returns>
            async ([AsParameters] JournalQueryParameters queryParams, // Uses [AsParameters] for binding from query
                                           [FromServices] IJournalService journalService,
                                           ClaimsPrincipal user) =>
            {
                if (!user.IsObserverOrHigher()) // Authorization check
                {
                    return Results.Forbid();
                }

				var result = await journalService.ListJournalEntriesAsync( queryParams );
				var items = result.Items.ToList(); // Ensure items are materialized for the response DTO
				var totalCount = result.TotalCount;

                var response = new PaginatedJournalResponse
                {
                    Items = items,
                    TotalItems = totalCount,
                    CurrentPage = queryParams.Page ?? 1, // Use defaults if not provided
                    PageSize = queryParams.PageSize ?? 20, // Use defaults if not provided
                    TotalPages = (totalCount > 0 && (queryParams.PageSize ?? 20) > 0)
                                 ? (int)Math.Ceiling(totalCount / (double)(queryParams.PageSize ?? 20))
                                 : 0
                };

                return Results.Ok(response);
            }).WithSummary("List Journal Entries")
              .WithDescription("Retrieves journal entries based on query parameters. Requires Observer role. Supports filtering, sorting, and pagination.")
              .Produces<PaginatedJournalResponse>(StatusCodes.Status200OK)
              .Produces(StatusCodes.Status403Forbidden)
              .Produces<ErrorResponse>(StatusCodes.Status400BadRequest); // For invalid query parameters, though not explicitly handled here

            // Defines GET /api/v1/journal/{journalRecordId}
            // Retrieves detailed information for a specific journal entry by its ID.
            // Requires Observer role. Calls IJournalService.GetJournalEntryDetailsAsync.
            journalGroup.MapGet("/{journalRecordId}",
            /// <summary>
            /// Retrieves detailed information for a specific journal entry, identified by its <paramref name="journalRecordId"/>.
            /// Requires Observer or higher privileges.
            /// </summary>
            /// <param name="journalRecordId">The unique identifier of the journal entry to retrieve.</param>
            /// <param name="journalService">The <see cref="IJournalService"/> for fetching journal entry details.</param>
            /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
            /// <returns>
            /// An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with a <see cref="JournalEntry"/> DTO on success.
            /// Returns <see cref="Results.NotFound(object?)"/> with an <see cref="ErrorResponse"/> if the entry is not found.
            /// Returns <see cref="Results.Forbid()"/> if unauthorized.
            /// </returns>
            async (string journalRecordId,
                                                             [FromServices] IJournalService journalService,
                                                             ClaimsPrincipal user) =>
            {
                if (!user.IsObserverOrHigher()) // Authorization check
                {
                    return Results.Forbid();
                }

                var entry = await journalService.GetJournalEntryDetailsAsync(journalRecordId);

                if (entry == null)
                {
                    return Results.NotFound(new ErrorResponse(error: "NotFound", message: $"Journal entry with ID '{journalRecordId}' not found."));
                }

                return Results.Ok(entry);
            }).WithSummary("Get Journal Entry Details")
              .WithDescription("Retrieves detailed information for a specific journal entry by its ID. Requires Observer role.")
              .Produces<JournalEntry>(StatusCodes.Status200OK)
              .Produces(StatusCodes.Status403Forbidden)
              .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

            return app;
        }
    }
}
