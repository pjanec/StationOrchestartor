using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.DTOs.API.AuditLog;
using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json; // Added for JsonSerializer
using System.Threading; // Added for Interlocked if used for ID, or just use Guid
using System.Threading.Tasks;

namespace SiteKeeper.Master.Services.Placeholders
{
    public class PlaceholderAuditLogService : IAuditLogService
    {
        private readonly ILogger<PlaceholderAuditLogService> _logger;
        // Store AuditLogEntry DTOs directly
        private readonly List<AuditLogEntry> _auditLogStore = new List<AuditLogEntry>();
        private long _logIdCounter = 0; // Simple ID generation for placeholder

        public PlaceholderAuditLogService(ILogger<PlaceholderAuditLogService> logger)
        {
            _logger = logger;
        }

        public Task LogActionAsync(
            string username,
            string action, // This will map to OperationType
            string? targetResource, // This will map to Target
            Dictionary<string, object>? parameters,
            string outcome, // String outcome to be parsed
            bool wasAuthorized = true, // This parameter isn't directly in AuditLogEntry, logged in details if needed
            string? details = null,
            string? clientIpAddress = null) // This parameter isn't in AuditLogEntry, logged in details if needed
        {
            Enum.TryParse<AuditLogOutcome>(outcome, true, out var parsedOutcome);
            // Consider logging if parsing fails, for now, it defaults to the first enum member (Success) if invalid.

            var entryDetails = details ?? string.Empty;
            if (!wasAuthorized)
            {
                entryDetails = $"[Unauthorized Attempt] {entryDetails}".Trim();
            }
            if (!string.IsNullOrEmpty(clientIpAddress))
            {
                // entryDetails = $"{entryDetails} (Client IP: {clientIpAddress})".Trim(); 
                // IP address is not part of the AuditLogEntry DTO per swagger. Could be logged separately or included in details if essential.
            }

            var entry = new AuditLogEntry
            {
                Id = Interlocked.Increment(ref _logIdCounter).ToString(), // Placeholder ID
                Timestamp = DateTime.UtcNow,
                User = username,
                OperationType = action,
                Target = targetResource,
                Parameters = parameters != null && parameters.Any() ? JsonSerializer.Serialize(parameters) : null,
                Outcome = parsedOutcome,
                Details = entryDetails
            };
            
            _auditLogStore.Add(entry);
            _logger.LogInformation($"Audit Logged (DTO): User '{username}' performed '{action}' on '{targetResource ?? "N/A"}', Outcome: {parsedOutcome}, Details: {entry.Details ?? "N/A"}");
            if (entry.Parameters != null)
            {
                _logger.LogInformation($"Parameters (JSON): {entry.Parameters}");
            }
            return Task.CompletedTask;
        }

        public Task<(IEnumerable<AuditLogEntry> Items, int TotalCount)> GetAuditLogsAsync(
            DateTime? startDate, 
            DateTime? endDate, 
            string? userFilter, 
            string? operationTypeFilter, 
            string? filterText, 
            string? sortBy, 
            string? sortOrder, 
            int page, 
            int pageSize)
        {
            _logger.LogInformation("Fetching audit logs (DTO based) with placeholder logic.");
            var query = _auditLogStore.AsEnumerable();

            if (startDate.HasValue)
                query = query.Where(log => log.Timestamp >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(log => log.Timestamp <= endDate.Value);
            if (!string.IsNullOrWhiteSpace(userFilter))
                query = query.Where(log => log.User != null && log.User.Contains(userFilter, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(operationTypeFilter))
                // Ensure OperationType is not null before calling .Equals
                query = query.Where(log => log.OperationType != null && log.OperationType.Equals(operationTypeFilter, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(filterText))
                // Ensure Target and Details are not null before calling .Contains
                query = query.Where(log => (log.Target != null && log.Target.Contains(filterText, StringComparison.OrdinalIgnoreCase)) || 
                                           (log.Details != null && log.Details.Contains(filterText, StringComparison.OrdinalIgnoreCase)) ||
                                           (log.OperationType != null && log.OperationType.Contains(filterText, StringComparison.OrdinalIgnoreCase)) || // Also search in OperationType
                                           (log.User != null && log.User.Contains(filterText, StringComparison.OrdinalIgnoreCase))); // And User

            // Placeholder sorting
            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                bool descending = sortOrder?.ToLower() == "desc";
                switch (sortBy.ToLowerInvariant()) // Use ToLowerInvariant for case-insensitive matching
                {
                    case "timestamp":
                        query = descending ? query.OrderByDescending(log => log.Timestamp) : query.OrderBy(log => log.Timestamp);
                        break;
                    case "user": // Mapped from "username"
                        query = descending ? query.OrderByDescending(log => log.User) : query.OrderBy(log => log.User);
                        break;
                    case "operationtype": // Mapped from "action"
                        query = descending ? query.OrderByDescending(log => log.OperationType) : query.OrderBy(log => log.OperationType);
                        break;
                    case "outcome":
                         query = descending ? query.OrderByDescending(log => log.Outcome.ToString()) : query.OrderBy(log => log.Outcome.ToString());
                         break;
                    default:
                        query = query.OrderByDescending(log => log.Timestamp); // Default sort
                        break;
                }
            }
            else
            {
                query = query.OrderByDescending(log => log.Timestamp); // Default sort
            }

            var totalCount = query.Count();
            var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Task.FromResult<(IEnumerable<AuditLogEntry>, int)>((items, totalCount));
        }
    }
} 