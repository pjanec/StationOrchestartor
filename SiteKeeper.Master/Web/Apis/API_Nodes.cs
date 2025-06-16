using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.DTOs.API.Environment;
using SiteKeeper.Shared.DTOs.API.Nodes;
using SiteKeeper.Shared.DTOs.API.Operations;
using SiteKeeper.Shared.DTOs.Common;
using SiteKeeper.Shared.Enums;
using SiteKeeper.Shared.Security;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace SiteKeeper.Master.Web.Apis
{
    /// <summary>
    /// Provides API endpoints for node management. This is a partial class that extends the main ApiEndpoints.
    /// The methods within this class are responsible for mapping all node-related endpoints.
    /// </summary>
    public static partial class ApiEndpoints
    {
        /// <summary>
        /// Maps all node-related endpoints, including retrieving node details, listing packages, and node actions like restart and shutdown.
        /// These endpoints are grouped under the "/api/v1/nodes" route and tagged for OpenAPI documentation.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to map endpoints to.</param>
        /// <param name="guiHostConstraint">The host string to constrain the endpoints to, ensuring they are only reachable from the intended UI host.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with mapped endpoints.</returns>
        public static IEndpointRouteBuilder MapNodesApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            var nodesGroup = app.MapGroup("/api/v1/nodes")
                .WithTags("Nodes")
                .RequireAuthorization()
                .RequireHost(guiHostConstraint);

            nodesGroup.MapGet("/{nodeName}", async (string nodeName, [FromServices] INodeService nodeService, ClaimsPrincipal user) =>
            {
                if (!user.IsObserverOrHigher()) return Results.Forbid();
                var nodeDetails = await nodeService.GetNodeDetailsAsync(nodeName);
                return nodeDetails != null ? Results.Ok(nodeDetails) : Results.NotFound(new ErrorResponse(error: "NODE_NOT_FOUND", message: $"Node '{nodeName}' not found."));
            }).WithSummary("Get Node Details").Produces<NodeDetailsResponse>().Produces<ErrorResponse>(StatusCodes.Status404NotFound);

            nodesGroup.MapGet("/{nodeName}/packages", async (string nodeName, [FromServices] INodeService nodeService, ClaimsPrincipal user) =>
            {
                if (!user.IsObserverOrHigher()) return Results.Forbid();
                var packages = await nodeService.ListNodePackagesAsync(nodeName);
                return packages != null ? Results.Ok(packages) : Results.NotFound(new ErrorResponse(error: "NODE_PACKAGES_NOT_FOUND", message: $"Packages for node '{nodeName}' not found or node does not exist."));
            }).WithSummary("List Packages on a Node").Produces<List<PackageOnNode>>().Produces<ErrorResponse>(StatusCodes.Status404NotFound);

            // POST /nodes/restart
            nodesGroup.MapPost("/restart",
                async (
                    [FromBody] NodeActionRequest request,
                    [FromServices] IMasterActionCoordinatorService opService,
                    ClaimsPrincipal user,
                    [FromServices] ILoggerFactory loggerFactory) =>
                {
                    var logger = loggerFactory.CreateLogger("SiteKeeper.Master.Web.Apis.Nodes");
                    var username = user.GetUsername();
                    logger.LogInformation("API: Node restart request received from user '{Username}'. Target AllNodes: {AllNodes}, Specific Nodes: {NodeCount}", 
                        username, request.AllNodes, request.NodeNames?.Count ?? 0);

                    if ((!request.AllNodes.HasValue || !request.AllNodes.Value) && (request.NodeNames == null || !request.NodeNames.Any()))
                    {
                        logger.LogWarning("API: Invalid node restart request from {Username}: No nodes specified and AllNodes is false.", username);
                        return Results.BadRequest(new ErrorResponse 
                        { 
                            Error = "BadRequest", 
                            Message = "Node restart action requires at least one target node or 'allNodes' to be true." 
                        });
                    }

                    var parameters = new Dictionary<string, object>
                    {
                        { "nodeNames", request.NodeNames }
                    };

                    var initiateRequest = new OperationInitiateRequest
                    {
                        OperationType = OperationType.NodeRestart,
                        Parameters = parameters,
                        Description = $"Restarting nodes: {string.Join(", ", request.NodeNames)}"
                    };

                    var operation = await opService.InitiateMasterActionAsync(initiateRequest, user);

                    var response = new OperationInitiationResponse { OperationId = operation.Id, Message = $"Node restart operation '{operation.Id}' initiated." };
                    return Results.Accepted($"/api/v1/operations/{response.OperationId}", response);
                })
                .WithName("RestartNodes")
                .WithTags("Operations") // As per Swagger
                .WithSummary("Restart Node(s)")
                .WithDescription("Initiates restart for specified node(s). Requires Operator role. *Audit Logged*.")
                .Produces<OperationInitiationResponse>(StatusCodes.Status202Accepted)
                .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsOperatorOrHigher()));

            // POST /nodes/shutdown
            nodesGroup.MapPost("/shutdown",
                async (
                    [FromBody] NodeActionRequest request, 
                    [FromServices] IMasterActionCoordinatorService opService, 
                    ClaimsPrincipal user, 
                    [FromServices] ILoggerFactory loggerFactory) =>
                {
                    var logger = loggerFactory.CreateLogger("SiteKeeper.Master.Web.Apis.Nodes");
                    var username = user.GetUsername();
                    logger.LogInformation("API: Node shutdown request received from user '{Username}'. Target AllNodes: {AllNodes}, Specific Nodes: {NodeCount}", 
                        username, request.AllNodes, request.NodeNames?.Count ?? 0);

                     if ((!request.AllNodes.HasValue || !request.AllNodes.Value) && (request.NodeNames == null || !request.NodeNames.Any()))
                    {
                        logger.LogWarning("API: Invalid node shutdown request from {Username}: No nodes specified and AllNodes is false.", username);
                        return Results.BadRequest(new ErrorResponse 
                        { 
                            Error = "BadRequest", 
                            Message = "Node shutdown action requires at least one target node or 'allNodes' to be true." 
                        });
                    }

                    var parameters = new Dictionary<string, object>
                    {
                        { "nodeNames", request.NodeNames }
                    };

                    var initiateRequest = new OperationInitiateRequest
                    {
                        OperationType = OperationType.NodeShutdown,
                        Parameters = parameters,
                        Description = $"Shutting down nodes: {string.Join(", ", request.NodeNames)}"
                    };

                    var operation = await opService.InitiateMasterActionAsync(initiateRequest, user);

                    var response = new OperationInitiationResponse { OperationId = operation.Id, Message = $"Node shutdown operation '{operation.Id}' initiated." };
                    return Results.Accepted($"/api/v1/operations/{response.OperationId}", response);
                })
                .WithName("ShutdownNodes")
                .WithTags("Operations") // As per Swagger
                .WithSummary("Shutdown Node(s)")
                .WithDescription("Initiates shutdown for specified node(s). Requires Operator role. *Audit Logged*.")
                .Produces<OperationInitiationResponse>(StatusCodes.Status202Accepted)
                .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsOperatorOrHigher()));

            nodesGroup.MapPost("/{nodeName}/control/{action}",
                async (
                    string nodeName,
                    string action,
                    ClaimsPrincipal user,
                    [FromServices] IMasterActionCoordinatorService masterActionService,
                    [FromServices] IAuditLogService auditLog,
                    HttpContext httpContext) =>
                {
                    var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("SiteKeeper.Master.Web.Apis.Nodes");
                    var username = user.GetUsername();
                    logger.LogInformation("API: Node control request received from user '{Username}'. Action: {Action}, Node: {Node}", 
                        username, action, nodeName);

                    if (!user.IsObserverOrHigher()) return Results.Forbid();

                    var parameters = new Dictionary<string, object>
                    {
                        { "nodeName", nodeName },
                        { "action", action }
                    };

                    var initiateRequest = new OperationInitiateRequest
                    {
                        OperationType = OperationType.NodeControl,
                        Parameters = parameters
                    };

                    var masterAction = await masterActionService.InitiateMasterActionAsync(initiateRequest, user);

                    await auditLog.LogActionAsync(
                        username,
                        $"NodeControlAction:{action}",
                        $"Node:{nodeName}",
                        new Dictionary<string, object> { { "operationId", masterAction.Id } },
                        AuditLogOutcome.Success.ToString(),
                        details: $"Node control action '{action}' initiated for node '{nodeName}'. Operation ID: {masterAction.Id}.",
                        clientIpAddress: httpContext.GetClientIpAddress()
                    );

                    var response = new OperationInitiationResponse
                    {
                        OperationId = masterAction.Id,
                        Message = $"Node control action '{action}' for node '{nodeName}' initiated."
                    };

                    return Results.Accepted($"/api/v1/operations/{masterAction.Id}", response);
                })
                .WithName("ControlNode")
                .WithTags("Operations")
                .WithSummary("Control Node")
                .WithDescription("Initiates a control action on a specified node. Requires Operator role. *Audit Logged*.")
                .Produces<OperationInitiationResponse>(StatusCodes.Status202Accepted)
                .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsOperatorOrHigher()));

            nodesGroup.MapPost("/control/{action}",
                async (
                    string action,
                    [FromBody] NodeActionRequest request,
                    ClaimsPrincipal user,
                    [FromServices] IMasterActionCoordinatorService masterActionService,
                    [FromServices] IAuditLogService auditLog,
                    HttpContext httpContext) =>
                {
                    var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("SiteKeeper.Master.Web.Apis.Nodes");
                    var username = user.GetUsername();
                    logger.LogInformation("API: Multi-node control request received from user '{Username}'. Action: {Action}, Nodes: {NodeCount}", 
                        username, action, request.NodeNames?.Count ?? 0);

                    if (!user.IsOperatorOrHigher()) return Results.Forbid();

                    if ((!request.AllNodes.HasValue || !request.AllNodes.Value) && (request.NodeNames == null || !request.NodeNames.Any()))
                    {
                        logger.LogWarning("API: Invalid multi-node control request from {Username}: No nodes specified and AllNodes is false.", username);
                        return Results.BadRequest(new ErrorResponse 
                        { 
                            Error = "BadRequest", 
                            Message = "Multi-node control action requires at least one target node or 'allNodes' to be true." 
                        });
                    }

                    var parameters = new Dictionary<string, object>
                    {
                        { "action", action },
                        { "nodeNames", request.NodeNames }
                    };

                    var initiateRequest = new OperationInitiateRequest
                    {
                        OperationType = OperationType.MultiNodeControl,
                        Parameters = parameters
                    };

                    var masterAction = await masterActionService.InitiateMasterActionAsync(initiateRequest, user);

                    await auditLog.LogActionAsync(
                        username,
                        $"MultiNodeControlAction:{action}",
                        "MultipleNodes",
                        new Dictionary<string, object>
                        {
                            { "nodes", request.NodeNames },
                            { "operationId", masterAction.Id }
                        },
                        AuditLogOutcome.Success.ToString(),
                        details: $"Multi-node control action '{action}' initiated for nodes: {string.Join(", ", request.NodeNames)}. Operation ID: {masterAction.Id}.",
                        clientIpAddress: httpContext.GetClientIpAddress()
                    );

                    var response = new OperationInitiationResponse
                    {
                        OperationId = masterAction.Id,
                        Message = $"Multi-node control action '{action}' for nodes '{string.Join(", ", request.NodeNames)}' initiated."
                    };
                    return Results.Accepted($"/api/v1/operations/{masterAction.Id}", response);
                })
                .WithName("ControlMultipleNodes")
                .WithTags("Operations")
                .WithSummary("Control Multiple Nodes")
                .WithDescription("Initiates a control action on multiple nodes. Requires Operator role. *Audit Logged*.")
                .Produces<OperationInitiationResponse>(StatusCodes.Status202Accepted)
                .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsOperatorOrHigher()));

            return app;
        }
    }
} 