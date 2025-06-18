using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.DTOs.API.Authentication;
using SiteKeeper.Shared.DTOs.Common;
using SiteKeeper.Shared.Enums;
using SiteKeeper.Shared.Security;
using System.Collections.Generic;
using System.Security.Claims;
using SiteKeeper.Master;

namespace SiteKeeper.Master.Web.Apis
{
    /// <summary>
    /// Defines API endpoints related to user authentication, including login, token refresh, and logout.
    /// This is a partial class, and these endpoints are registered via the main <see cref="ApiEndpoints.MapAll(WebApplication)"/> method.
    /// </summary>
    /// <remarks>
    /// This class utilizes Minimal API features to structure endpoints under the base path <c>/api/v1/auth</c>.
    /// Endpoints interact with the <see cref="IAuthenticationService"/> for core authentication logic
    /// and the <see cref="IAuditLogService"/> to record authentication attempts and outcomes.
    /// </remarks>
    public static partial class ApiEndpoints
    {
        /// <summary>
        /// Maps all API endpoints related to user authentication.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to which the endpoints will be mapped (typically the <see cref="WebApplication"/> instance).</param>
        /// <param name="guiHostConstraint">A hostname constraint string to apply to these endpoints, ensuring they are only accessible via specific hostnames configured for GUI/API access.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with the newly mapped authentication endpoints.</returns>
        /// <remarks>
        /// This method creates a route group for <c>/api/v1/auth</c> and defines the following endpoints:
        /// <list type="bullet">
        ///   <item><description>POST /login: Authenticates a user and returns JWTs.</description></item>
        ///   <item><description>POST /refresh: Refreshes an access token using a refresh token.</description></item>
        ///   <item><description>POST /logout: Logs out an authenticated user (requires authorization).</description></item>
        /// </list>
        /// </remarks>
        public static IEndpointRouteBuilder MapAuthenticationApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            var authGroup = app.MapGroup("/api/v1/auth")
                .WithTags("Authentication")
                .RequireHost(guiHostConstraint);

            // Defines POST /api/v1/auth/login
            // Authenticates a user with username and password.
            // On success, returns AuthResponse (access/refresh tokens, user info).
            // On failure, returns 401 Unauthorized. Logs attempts to IAuditLogService.
            authGroup.MapPost("/login",
            /// <summary>
            /// Handles user login requests. Authenticates credentials using <see cref="IAuthenticationService"/>.
            /// On successful authentication, returns JWT access and refresh tokens, along with user information.
            /// Logs both successful and failed login attempts to the audit log via <see cref="IAuditLogService"/>.
            /// </summary>
            /// <param name="loginRequest">The <see cref="UserLoginRequest"/> DTO containing the username and password.</param>
            /// <param name="authService">The <see cref="IAuthenticationService"/> for validating credentials and generating tokens.</param>
            /// <param name="auditLog">The <see cref="IAuditLogService"/> for recording login attempts.</param>
            /// <param name="httpContext">The <see cref="HttpContext"/> for accessing request details like client IP address.</param>
            /// <param name="logger">A logger for this endpoint, typically for debugging or internal logging.</param>
            /// <returns>
            /// An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with an <see cref="AuthResponse"/> on success,
            /// or <see cref="Results.Json(object?, System.Text.Json.JsonSerializerOptions?, string?, int?)"/> with an <see cref="ErrorResponse"/>
            /// and status code 401 (Unauthorized) on failure.
            /// </returns>
            async (UserLoginRequest loginRequest, [FromServices] IAuthenticationService authService, [FromServices] IAuditLogService auditLog, HttpContext httpContext, [FromServices] ILogger<MasterConfig> logger) =>
            {
                var authResult = await authService.LoginAsync(loginRequest.Username, loginRequest.Password);
                
                if (authResult is not null)
                {
                    // Log successful login attempt.
                    await auditLog.LogActionAsync(
                        username: loginRequest.Username,
                        action: "UserLoginSuccess", 
                        targetResource: $"User:{loginRequest.Username}",
                        parameters: null, 
                        outcome: AuditLogOutcome.Success.ToString(),
                        details: $"User '{loginRequest.Username}' logged in successfully.",
                        clientIpAddress: httpContext.GetClientIpAddress());
                    return Results.Ok(authResult);
                }
                else
                {
                    // Log failed login attempt.
                    await auditLog.LogActionAsync(
                        username: loginRequest.Username,
                        action: "UserLoginFailure", 
                        targetResource: $"User:{loginRequest.Username}",
                        parameters: null,
                        outcome: AuditLogOutcome.Failure.ToString(),
                        details: $"User '{loginRequest.Username}' login failed due to invalid credentials.",
                        clientIpAddress: httpContext.GetClientIpAddress());
                    // Return 401 Unauthorized as per Swagger for invalid credentials.
                    return Results.Json(new ErrorResponse(error: "Unauthorized", message: "Invalid username or password."), statusCode: StatusCodes.Status401Unauthorized);
                }
            }).WithSummary("User Login")
              .WithDescription("Authenticates a user and returns JWT access and refresh tokens along with user role.")
              .Produces<AuthResponse>(StatusCodes.Status200OK)
              .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized) 
              .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

            // Defines POST /api/v1/auth/refresh
            // Refreshes an access token using a provided refresh token.
            // On success, returns NewAccessTokenResponse.
            // On failure (e.g., invalid refresh token), returns 401 Unauthorized. Logs attempts to IAuditLogService.
            authGroup.MapPost("/refresh",
            /// <summary>
            /// Handles requests to refresh a JWT access token using a valid refresh token.
            /// Uses <see cref="IAuthenticationService"/> to validate the refresh token and issue a new access token.
            /// Logs both successful and failed token refresh attempts to the audit log via <see cref="IAuditLogService"/>.
            /// </summary>
            /// <param name="tokenRequest">The <see cref="RefreshTokenRequest"/> DTO containing the refresh token.</param>
            /// <param name="authService">The <see cref="IAuthenticationService"/> for validating the refresh token and generating a new access token.</param>
            /// <param name="auditLog">The <see cref="IAuditLogService"/> for recording token refresh attempts.</param>
            /// <param name="httpContext">The <see cref="HttpContext"/> for accessing request details and user context.</param>
            /// <returns>
            /// An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with a <see cref="NewAccessTokenResponse"/> on success,
            /// or <see cref="Results.Json(object?, System.Text.Json.JsonSerializerOptions?, string?, int?)"/> with an <see cref="ErrorResponse"/>
            /// and status code 401 (Unauthorized) if the refresh token is invalid or expired.
            /// </returns>
            async (RefreshTokenRequest tokenRequest, [FromServices] IAuthenticationService authService, [FromServices] IAuditLogService auditLog, HttpContext httpContext) =>
            {
                var result = await authService.RefreshTokenAsync(tokenRequest.RefreshToken);
                
                var usernameAttemptingRefresh = httpContext.User.GetUsername() ?? "unknown_user_from_token";

                if (result != null)
                {
                    // Log successful token refresh.
                    await auditLog.LogActionAsync(
                        username: usernameAttemptingRefresh, 
                        action: "TokenRefreshSuccess", 
                        targetResource: "Token", 
                        parameters: null, 
                        outcome: AuditLogOutcome.Success.ToString(), 
                        details: "Access token refreshed successfully.",
                        clientIpAddress: httpContext.GetClientIpAddress());
                    return Results.Ok(result);
                }
                // Log failed token refresh.
                await auditLog.LogActionAsync(
                    username: usernameAttemptingRefresh,
                    action: "TokenRefreshFailure",
                    targetResource: "Token", 
                    parameters: null,
                    outcome: AuditLogOutcome.Failure.ToString(), 
                    details: "Invalid or expired refresh token.",
                    clientIpAddress: httpContext.GetClientIpAddress());
                // Return 401 Unauthorized as per Swagger for invalid refresh token.
                return Results.Json(new ErrorResponse(error: "Unauthorized", message: "Invalid refresh token."), statusCode: StatusCodes.Status401Unauthorized);
            }).WithSummary("Refresh Access Token")
              .WithDescription("Obtains a new JWT access token using a valid refresh token.")
              .Produces<NewAccessTokenResponse>(StatusCodes.Status200OK)
              .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized) 
              .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

            // Defines POST /api/v1/auth/logout
            // Logs out an authenticated user. Requires authorization.
            // Calls IAuthenticationService.LogoutAsync and logs the action.
            authGroup.MapPost("/logout",
            /// <summary>
            /// Handles user logout requests for an authenticated user.
            /// Calls the <see cref="IAuthenticationService"/> to invalidate the user's session (e.g., by revoking refresh tokens).
            /// Logs the logout action to the audit log via <see cref="IAuditLogService"/>.
            /// This endpoint requires prior authorization.
            /// </summary>
            /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user performing the logout.</param>
            /// <param name="authService">The <see cref="IAuthenticationService"/> for performing logout operations.</param>
            /// <param name="auditLog">The <see cref="IAuditLogService"/> for recording the logout event.</param>
            /// <param name="httpContext">The <see cref="HttpContext"/> for accessing request details.</param>
            /// <returns>An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with a <see cref="LogoutResponse"/> on success.</returns>
            async (ClaimsPrincipal user, [FromServices] IAuthenticationService authService, [FromServices] IAuditLogService auditLog, HttpContext httpContext) =>
            {
                var username = user.GetUsername(); // GetUsername() extension method handles null user.Identity.Name
                var logoutResponse = await authService.LogoutAsync(username ?? "unknown"); // Pass username to service
                
                await auditLog.LogActionAsync(
                    username: username ?? "unknown_user_on_logout",
                    action: "UserLogout",
                    targetResource: $"User:{username ?? "unknown"}",
                    parameters: null,
                    outcome: AuditLogOutcome.Success.ToString(), // Logout action itself is typically successful if called
                    details: $"User '{username ?? "unknown"}' logged out successfully.",
                    clientIpAddress: httpContext.GetClientIpAddress());
                return Results.Ok(logoutResponse);
            }).RequireAuthorization().WithSummary("Logs a user out").Produces<LogoutResponse>(StatusCodes.Status200OK);
            
            return app;
        }
    }
} 