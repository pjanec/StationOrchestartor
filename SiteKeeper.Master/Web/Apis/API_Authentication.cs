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
    /// Provides API endpoints for user authentication. This is a partial class that extends the main ApiEndpoints.
    /// The methods within this class are responsible for mapping all authentication-related endpoints.
    /// </summary>
    public static partial class ApiEndpoints
    {
        /// <summary>
        /// Maps authentication-related endpoints, including login, logout, and token refresh.
        /// These endpoints are grouped under the "/api/v1/auth" route and tagged for OpenAPI documentation.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to map endpoints to.</param>
        /// <param name="guiHostConstraint">The host string to constrain the endpoints to, ensuring they are only reachable from the intended UI host.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with mapped endpoints.</returns>
        public static IEndpointRouteBuilder MapAuthenticationApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            var authGroup = app.MapGroup("/api/v1/auth")
                .WithTags("Authentication")
                .RequireHost(guiHostConstraint);

            authGroup.MapPost("/login", async (UserLoginRequest loginRequest, [FromServices] IAuthenticationService authService, [FromServices] IAuditLogService auditLog, HttpContext httpContext, [FromServices] ILogger<MasterConfig> logger) =>
            {
                // This endpoint authenticates a user based on username and password.
                // It returns JWT access and refresh tokens upon successful authentication.
                // Audit logs are created for both successful and failed login attempts.
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

            authGroup.MapPost("/refresh", async (RefreshTokenRequest tokenRequest, [FromServices] IAuthenticationService authService, [FromServices] IAuditLogService auditLog, HttpContext httpContext) => 
            {
                // This endpoint refreshes an access token using a valid refresh token.
                // It returns a new access token and potentially a new refresh token.
                // Audit logs are created for refresh attempts.
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

            authGroup.MapPost("/logout", async (ClaimsPrincipal user, [FromServices] IAuthenticationService authService, [FromServices] IAuditLogService auditLog, HttpContext httpContext) =>
            {
                var username = user.GetUsername();
                var logoutResponse = await authService.LogoutAsync(username);
                
                await auditLog.LogActionAsync(
                    username: username,
                    action: "UserLogout",
                    targetResource: $"User:{username}",
                    parameters: null,
                    outcome: AuditLogOutcome.Success.ToString(),
                    details: $"User '{username}' logged out.",
                    clientIpAddress: httpContext.GetClientIpAddress());
                return Results.Ok(logoutResponse);
            }).RequireAuthorization().WithSummary("Logs a user out").Produces<LogoutResponse>(StatusCodes.Status200OK);
            
            return app;
        }
    }
} 