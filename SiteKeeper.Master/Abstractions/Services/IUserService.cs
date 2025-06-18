using SiteKeeper.Shared.DTOs.API.Authentication;
using SiteKeeper.Shared.DTOs.API.Users;
using SiteKeeper.Shared.DTOs.Common;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Abstractions.Services
{
    /// <summary>
    /// Defines the contract for a service that manages user accounts within the SiteKeeper system.
    /// </summary>
    /// <remarks>
    /// This service is responsible for all CRUD (Create, Read, Update, Delete) operations related to users,
    /// as well as managing user role assignments. It is typically consumed by API controllers that expose
    /// user management endpoints (e.g., under /api/users). Operations return <see cref="ServiceResult"/>
    /// or <see cref="ServiceResult{T}"/> to indicate outcomes and provide data or error details.
    /// </remarks>
    public interface IUserService
    {
        /// <summary>
        /// Creates a new user based on the provided details in the <see cref="UserCreationRequest"/>.
        /// This method is typically called by an API endpoint like POST /api/users.
        /// </summary>
        /// <param name="request">The DTO containing information for the new user (username, password, role, etc.).</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a <see cref="ServiceResult{T}"/>
        /// indicating success or failure, and including the created <see cref="UserInfo"/> on success.
        /// </returns>
        Task<ServiceResult<UserInfo>> CreateUserAsync(UserCreationRequest request);

        /// <summary>
        /// Updates an existing user's information, identified by their username.
        /// This method is typically called by an API endpoint like PUT /api/users/{username}.
        /// </summary>
        /// <param name="username">The username of the user to update.</param>
        /// <param name="request">The DTO containing the fields to update (e.g., display name, email, role).</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a <see cref="ServiceResult{T}"/>
        /// indicating success or failure, and including the updated <see cref="UserInfo"/> on success.
        /// </returns>
        Task<ServiceResult<UserInfo>> UpdateUserAsync(string username, UserUpdateRequest request);

        /// <summary>
        /// Deletes a user, identified by their username.
        /// This method is typically called by an API endpoint like DELETE /api/users/{username}.
        /// </summary>
        /// <param name="username">The username of the user to delete.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a <see cref="ServiceResult"/>
        /// indicating the success or failure of the deletion.
        /// </returns>
        Task<ServiceResult> DeleteUserAsync(string username);

        /// <summary>
        /// Retrieves detailed information for a specific user, identified by their username.
        /// This method is typically called by an API endpoint like GET /api/users/{username}.
        /// </summary>
        /// <param name="username">The username of the user to retrieve.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the <see cref="UserInfo"/> DTO
        /// for the specified user, or null if the user is not found.
        /// </returns>
        Task<UserInfo?> GetUserAsync(string username);

        /// <summary>
        /// Retrieves a list of all users, with optional filtering and sorting capabilities.
        /// This method is typically called by an API endpoint like GET /api/users.
        /// </summary>
        /// <param name="filterText">Optional text to filter users by (e.g., username, display name, email).</param>
        /// <param name="sortBy">Optional field name to sort the results by (e.g., "username", "role").</param>
        /// <param name="sortOrder">Optional sort order ("asc" or "desc").</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a list of <see cref="UserListItem"/> DTOs.
        /// </returns>
        Task<List<UserListItem>> ListUsersAsync(string? filterText, string? sortBy, string? sortOrder);

        /// <summary>
        /// Assigns a specified role to a user, identified by their username.
        /// This method is typically called by an API endpoint like PUT /api/users/{username}/role.
        /// </summary>
        /// <param name="username">The username of the user whose role is to be assigned.</param>
        /// <param name="request">The DTO specifying the new role to assign.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a <see cref="ServiceResult"/>
        /// indicating the success or failure of the role assignment.
        /// </returns>
        Task<ServiceResult> AssignUserRoleAsync(string username, UserRoleAssignmentRequest request);
    }
} 