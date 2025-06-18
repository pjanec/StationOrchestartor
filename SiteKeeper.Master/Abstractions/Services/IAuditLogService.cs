using SiteKeeper.Shared.DTOs.API.AuditLog; // AuditLogEntry will be the DTO used
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
using SiteKeeper.Shared.Enums; // For AuditLogOutcome if used directly

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Service interface for creating and retrieving audit trail records for significant system events and user actions.
    /// </summary>
    /// <remarks>
    /// The <see cref="LogActionAsync"/> method is typically invoked by other services (e.g., <see cref="IUserService"/>, <see cref="IMasterActionCoordinatorService"/>)
    /// or API controllers whenever an auditable action occurs.
    /// The <see cref="GetAuditLogsAsync"/> method is primarily called by API controllers serving endpoints for querying and displaying audit logs,
    /// using <see cref="AuditLogQueryParameters"/> as input and returning <see cref="PaginatedAuditLogResponse"/>.
    /// </remarks>
    public interface IAuditLogService
    {
        /// <summary>
        /// Logs an audited action, capturing details about the event, user, and outcome.
        /// </summary>
        /// <param name="username">The username of the user who performed the action. Can be "System" for automated actions.</param>
        /// <param name="action">A descriptive name or type for the action (e.g., "UserLogin", "StartSystemSoftware", "UpdateConfiguration").</param>
        /// <param name="targetResource">Optional. The primary resource targeted by the action (e.g., "User:john.doe", "Node:Server01", "Environment:Production").</param>
        /// <param name="parameters">Optional. Key parameters associated with the action, often serialized to JSON for storage. This provides context for the action.</param>
        /// <param name="outcome">The outcome of the action, typically represented by a string value from <see cref="AuditLogOutcome"/> (e.g., "Success", "Failure").</param>
        /// <param name="wasAuthorized">Indicates if the user was authorized for the action. Defaults to true. Set to false for unauthorized attempts.</param>
        /// <param name="details">Optional. Additional details, error messages, or contextual information related to the event.</param>
        /// <param name="clientIpAddress">Optional. The IP address of the client from which the action was initiated.</param>
        /// <returns>A task representing the asynchronous logging operation.</returns>
        Task LogActionAsync(
            string username,
            string action,
            string? targetResource,
            Dictionary<string, object>? parameters,
            string outcome, // Consider using AuditLogOutcome enum directly if service layer handles enum-to-string mapping
            bool wasAuthorized = true,
            string? details = null,
            string? clientIpAddress = null);

        /// <summary>
        /// Retrieves a paginated list of audit log entries based on specified filtering and sorting criteria.
        /// This method typically supports the functionality exposed by the GET /api/audit-log endpoint.
        /// </summary>
        /// <param name="startDate">Optional. Filter logs from this UTC date.</param>
        /// <param name="endDate">Optional. Filter logs up to this UTC date.</param>
        /// <param name="userFilter">Optional. Filter entries by the username who performed the action.</param>
        /// <param name="operationTypeFilter">Optional. Filter entries by the type of operation performed (corresponds to the 'action' in LogActionAsync).</param>
        /// <param name="filterText">Optional. Text for fuzzy filtering across relevant fields like username, action, target, or details.</param>
        /// <param name="sortBy">Optional. Specifies the field to sort by (e.g., "Timestamp", "User", "OperationType"). Defaults to "Timestamp" if null.</param>
        /// <param name="sortOrder">Optional. Specifies the sort order ("asc" for ascending, "desc" for descending). Defaults to "desc" if null.</param>
        /// <param name="page">Page number for pagination (1-indexed). Defaults to 1.</param>
        /// <param name="pageSize">Number of items per page for pagination. Defaults to a system-defined page size.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is a tuple containing the list of <see cref="AuditLogEntry"/> items for the current page and the total count of items matching the query.</returns>
        Task<(IEnumerable<AuditLogEntry> Items, int TotalCount)> GetAuditLogsAsync(
            DateTime? startDate, 
            DateTime? endDate, 
            string? userFilter, 
            string? operationTypeFilter, 
            string? filterText, 
            string? sortBy, 
            string? sortOrder, 
            int page, 
            int pageSize);
    }
} 