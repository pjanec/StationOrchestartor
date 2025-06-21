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
    /// Provides API endpoints for the journal. This is a partial class that extends the main ApiEndpoints.
    /// The methods within this class are responsible for mapping all journal-related endpoints.
    /// </summary>
    public static partial class ApiEndpoints
    {
        /// <summary>
        /// Maps all journal-related endpoints.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to map endpoints to.</param>
        /// <param name="guiHostConstraint">The host string to constrain the endpoints to.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with mapped endpoints.</returns>
        public static IEndpointRouteBuilder MapJournalApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            var journalGroup = app.MapGroup("/api/v1/journal")
                                  .WithTags("Journal")
                                  .RequireAuthorization()
                                  .RequireHost(guiHostConstraint);

            journalGroup.MapGet("/", async ([AsParameters] JournalQueryParameters queryParams,
                                           [FromServices] IJournal journalService,
                                           ClaimsPrincipal user) =>
            {
                if (!user.IsObserverOrHigher())
                {
                    return Results.Forbid();
                }


				var result = await journalService.ListJournalEntriesAsync( queryParams );
				var items = result.Items.ToList();
				var totalCount = result.TotalCount;

                var response = new PaginatedJournalResponse
                {
                    Items = items.ToList(),
                    TotalItems = totalCount,
                    CurrentPage = queryParams.Page ?? 1,
                    PageSize = queryParams.PageSize ?? 20,
                    TotalPages = (totalCount > 0 && (queryParams.PageSize ?? 20) > 0)
                                 ? (int)Math.Ceiling(totalCount / (double)(queryParams.PageSize ?? 20))
                                 : 0
                };

                return Results.Ok(response);
            }).WithSummary("List Journal Entries")
              .WithDescription("Retrieves journal entries based on query parameters. Requires Observer role. Supports filtering, sorting, and pagination.")
              .Produces<PaginatedJournalResponse>(StatusCodes.Status200OK)
              .Produces(StatusCodes.Status403Forbidden)
              .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

            journalGroup.MapGet("/{journalRecordId}", async (string journalRecordId,
                                                             [FromServices] IJournal journalService,
                                                             ClaimsPrincipal user) =>
            {
                if (!user.IsObserverOrHigher())
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
