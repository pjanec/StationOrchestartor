using SiteKeeper.Shared.DTOs.API.Authentication;
using SiteKeeper.Shared.DTOs.API.Users;
using SiteKeeper.Shared.DTOs.Common;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Defines the contract for a service that manages user accounts.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Creates a new user.
        /// </summary>
        Task<ServiceResult<UserInfo>> CreateUserAsync(UserCreationRequest request);

        /// <summary>
        /// Updates an existing user.
        /// </summary>
        Task<ServiceResult<UserInfo>> UpdateUserAsync(string username, UserUpdateRequest request);

        /// <summary>
        /// Deletes a user.
        /// </summary>
        Task<ServiceResult> DeleteUserAsync(string username);

        /// <summary>
        /// Gets information for a specific user.
        /// </summary>
        Task<UserInfo?> GetUserAsync(string username);

        /// <summary>
        /// Lists all users, with optional filtering and sorting.
        /// </summary>
        Task<List<UserListItem>> ListUsersAsync(string? filterText, string? sortBy, string? sortOrder);

        /// <summary>
        /// Assigns a role to a user.
        /// </summary>
        Task<ServiceResult> AssignUserRoleAsync(string username, UserRoleAssignmentRequest request);
    }
} 