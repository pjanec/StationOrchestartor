using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.DTOs.API.Authentication; // For UserInfo
using SiteKeeper.Shared.DTOs.API.Users;
using SiteKeeper.Shared.DTOs.Common;
using SiteKeeper.Shared.Enums;
using SiteKeeper.Shared.Security; // For SiteKeeperRoles constants
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Services.Placeholders
{
    public class PlaceholderUserService : IUserService
    {
        private readonly ILogger<PlaceholderUserService> _logger;
        private readonly List<UserInfo> _usersStore = new List<UserInfo>();

        public PlaceholderUserService(ILogger<PlaceholderUserService> logger)
        {
            _logger = logger;

            // Initialize with some default users
            _usersStore.Add(new UserInfo { Username = "defaultadmin", Role = UserRole.AdvancedAdmin });
            _usersStore.Add(new UserInfo { Username = "defaultop", Role = UserRole.Operator });
            _usersStore.Add(new UserInfo { Username = "defaultobs", Role = UserRole.Observer });
        }

        public Task<ServiceResult<UserInfo>> CreateUserAsync(UserCreationRequest request)
        {
            _logger.LogInformation($"Attempting to create user: {request.Username}");
            if (_usersStore.Any(u => u.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning($"Username '{request.Username}' already exists.");
                return Task.FromResult(ServiceResult<UserInfo>.Failure("USERNAME_EXISTS", "Username already exists."));
            }

            if (!Enum.TryParse<UserRole>(request.Role, true, out var userRoleEnum))
            {
                _logger.LogWarning($"Invalid role specified: {request.Role}");
                return Task.FromResult(ServiceResult<UserInfo>.Failure("INVALID_ROLE", $"Invalid role specified: {request.Role}. Valid roles are Observer, Operator, BasicAdmin, AdvancedAdmin."));
            }

            var newUserInfo = new UserInfo
            {
                Username = request.Username,
                Role = userRoleEnum 
                // In a real app, also handle password hashing and storage, DisplayName, Email, etc.
                // UserId would be generated (e.g., Guid)
            };
            _usersStore.Add(newUserInfo);
            _logger.LogInformation($"User '{request.Username}' created successfully with role {userRoleEnum}.");
            return Task.FromResult(ServiceResult<UserInfo>.Success(newUserInfo));
        }

        public Task<ServiceResult<UserInfo>> UpdateUserAsync(string username, UserUpdateRequest request)
        {
            _logger.LogInformation($"Attempting to update user: {username}");
            var userToUpdate = _usersStore.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (userToUpdate == null)
            {
                _logger.LogWarning($"User '{username}' not found for update.");
                return Task.FromResult(ServiceResult<UserInfo>.Failure("USER_NOT_FOUND", "User not found."));
            }

            bool updated = false;
            if (!string.IsNullOrEmpty(request.DisplayName)) // Assuming UserInfo might get DisplayName later
            {
                // userToUpdate.DisplayName = request.DisplayName; // If UserInfo had DisplayName
                updated = true;
            }
            if (!string.IsNullOrEmpty(request.Email)) // Assuming UserInfo might get Email later
            {
                // userToUpdate.Email = request.Email; // If UserInfo had Email
                updated = true;
            }

            if (!string.IsNullOrEmpty(request.Role))
            {
                if (!Enum.TryParse<UserRole>(request.Role, true, out var newRoleEnum))
                {
                    _logger.LogWarning($"Invalid role specified for update: {request.Role}");
                    return Task.FromResult(ServiceResult<UserInfo>.Failure("INVALID_ROLE", $"Invalid role specified: {request.Role}. Valid roles are Observer, Operator, BasicAdmin, AdvancedAdmin."));
                }
                if (userToUpdate.Role != newRoleEnum)
                {
                    userToUpdate.Role = newRoleEnum;
                    updated = true;
                }
            }
            
            if (updated)
            {
                _logger.LogInformation($"User '{username}' updated successfully.");
            }
            else
            {
                _logger.LogInformation($"User '{username}' update request processed, but no changes were made.");
            }
            return Task.FromResult(ServiceResult<UserInfo>.Success(userToUpdate));
        }

        public Task<ServiceResult> DeleteUserAsync(string username)
        {
            _logger.LogInformation($"Attempting to delete user: {username}");
            var userToRemove = _usersStore.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (userToRemove == null)
            {
                _logger.LogWarning($"User '{username}' not found for deletion.");
                return Task.FromResult(ServiceResult.Failure("USER_NOT_FOUND", "User not found."));
            }

            _usersStore.Remove(userToRemove);
            _logger.LogInformation($"User '{username}' deleted successfully.");
            return Task.FromResult(ServiceResult.Success());
        }

        public Task<UserInfo?> GetUserAsync(string username)
        {
            _logger.LogInformation($"Fetching user: {username}");
            var user = _usersStore.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (user == null) _logger.LogWarning($"User '{username}' not found.");
            return Task.FromResult(user);
        }

        public Task<List<UserListItem>> ListUsersAsync(string? filterText, string? sortBy, string? sortOrder)
        {
            _logger.LogInformation($"Listing users with filter: '{filterText}', sortBy: '{sortBy}', sortOrder: '{sortOrder}'.");
            IEnumerable<UserInfo> query = _usersStore;

            if (!string.IsNullOrWhiteSpace(filterText))
            {
                query = query.Where(u => u.Username.Contains(filterText, StringComparison.OrdinalIgnoreCase));
            }

            bool descending = sortOrder?.Equals("desc", StringComparison.OrdinalIgnoreCase) ?? false;
            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                query = sortBy.ToLowerInvariant() switch
                {
                    "username" => descending ? query.OrderByDescending(u => u.Username) : query.OrderBy(u => u.Username),
                    "role" => descending ? query.OrderByDescending(u => u.Role.ToString()) : query.OrderBy(u => u.Role.ToString()),
                    _ => query.OrderBy(u => u.Username) // Default sort
                };
            }
            else
            {
                query = query.OrderBy(u => u.Username); // Default sort if sortBy is not provided
            }

            var userListItems = query.Select(u => new UserListItem 
            { 
                Username = u.Username, 
                Role = u.Role 
            }).ToList();

            return Task.FromResult(userListItems);
        }

        public Task<ServiceResult> AssignUserRoleAsync(string username, UserRoleAssignmentRequest request)
        {
            _logger.LogInformation($"Attempting to assign role '{request.Role}' to user: {username}");
            var userToUpdate = _usersStore.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (userToUpdate == null)
            {
                _logger.LogWarning($"User '{username}' not found for role assignment.");
                return Task.FromResult(ServiceResult.Failure("USER_NOT_FOUND", "User not found."));
            }

            if (!Enum.TryParse<UserRole>(request.Role, true, out var newRoleEnum))
            {
                 _logger.LogWarning($"Invalid role specified for assignment: {request.Role}");
                return Task.FromResult(ServiceResult.Failure("INVALID_ROLE", $"Invalid role specified: {request.Role}. Valid roles are Observer, Operator, BasicAdmin, AdvancedAdmin."));
            }

            if (userToUpdate.Role == newRoleEnum)
            {
                _logger.LogInformation($"User '{username}' already has role '{newRoleEnum}'. No change made.");
                // Consider if this should be a specific success or different kind of response.
                // For now, treating as a successful no-op.
            }
            else
            {
                userToUpdate.Role = newRoleEnum;
                _logger.LogInformation($"Role '{newRoleEnum}' assigned successfully to user '{username}'.");
            }
            
            return Task.FromResult(ServiceResult.Success());
        }
    }
} 