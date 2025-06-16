using SiteKeeper.Shared.DTOs.API.AuditLog; // AuditLogEntry will be the DTO used
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Service interface for logging audit trails.
    /// </summary>
    public interface IAuditLogService
    {
        /// <summary>
        /// Logs an audited action.
        /// </summary>
        /// <param name="username">The username performing the action.</param>
        /// <param name="action">A descriptive name for the action.</param>
        /// <param name="targetResource">The primary resource targeted by the action (if any).</param>
        /// <param name="parameters">Key parameters associated with the action.</param>
        /// <param name="outcome">The outcome of the action (e.g., "Success", "Failure").</param>
        /// <param name="wasAuthorized">True if the user was authorized for the action, false otherwise (even if the attempt was blocked).</param>
        /// <param name="details">Additional details or error messages.</param>
        /// <param name="clientIpAddress">The IP address of the client making the request.</param>
        Task LogActionAsync(
            string username,
            string action,
            string? targetResource,
            Dictionary<string, object>? parameters,
            string outcome,
            bool wasAuthorized = true,
            string? details = null,
            string? clientIpAddress = null);

        /// <summary>
        /// Retrieves audit logs based on specified criteria.
        /// </summary>
        /// <param name="startDate">Filter logs from this date.</param>
        /// <param name="endDate">Filter logs up to this date.</param>
        /// <param name="userFilter">Filter logs by username.</param>
        /// <param name="operationTypeFilter">Filter logs by operation type.</param>
        /// <param name="filterText">Text for fuzzy filtering across relevant fields.</param>
        /// <param name="sortBy">Column name to sort by.</param>
        /// <param name="sortOrder">Sort order (e.g., "asc", "desc").</param>
        /// <param name="page">Page number for pagination (1-indexed).</param>
        /// <param name="pageSize">Number of items per page.</param>
        /// <returns>A tuple containing the list of audit log entries and the total count for pagination.</returns>
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