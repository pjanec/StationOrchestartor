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
    /// Defines API endpoints related to managed nodes, including retrieving details, listing packages, and initiating node-level actions.
    /// This is a partial class, and these endpoints are registered via the main <see cref="ApiEndpoints.MapAll(WebApplication)"/> method.
    /// </summary>
    /// <remarks>
    /// This class utilizes Minimal API features to structure endpoints under the base path <c>/api/v1/nodes</c>.
    /// General authorization is required for this group, with specific role checks (e.g., Observer, Operator) applied at individual endpoints.
    /// Endpoints interact with services like <see cref="INodeService"/> for information retrieval and
    /// <see cref="IMasterActionCoordinatorService"/> for initiating actions like restart or shutdown.
    /// Audit logging is performed for action-initiating endpoints.
    /// </remarks>
    public static partial class ApiEndpoints
    {
        /// <summary>
        /// Maps all API endpoints related to node management and information retrieval.
        /// </summary>
        /// <param name="app">The <see cref="IEndpointRouteBuilder"/> to which the endpoints will be mapped (typically the <see cref="WebApplication"/> instance).</param>
        /// <param name="guiHostConstraint">A hostname constraint string to apply to these endpoints, ensuring they are only accessible via specific hostnames configured for GUI/API access.</param>
        /// <returns>The <see cref="IEndpointRouteBuilder"/> with the newly mapped node endpoints.</returns>
        /// <remarks>
        /// This method creates a route group for <c>/api/v1/nodes</c> which requires general authorization.
        /// It defines endpoints for:
        /// <list type="bullet">
        ///   <item><description><c>GET /{nodeName}</c>: Retrieves detailed information for a specific node. Requires Observer privileges.</description></item>
        ///   <item><description><c>GET /{nodeName}/packages</c>: Lists packages installed on a specific node. Requires Observer privileges.</description></item>
        ///   <item><description><c>POST /restart</c>: Initiates a restart operation for specified node(s). Requires Operator privileges.</description></item>
        ///   <item><description><c>POST /shutdown</c>: Initiates a shutdown operation for specified node(s). Requires Operator privileges.</description></item>
        ///   <item><description><c>POST /{nodeName}/control/{action}</c>: Initiates a generic control action on a specific node. Requires Operator privileges.</description></item>
        ///   <item><description><c>POST /control/{action}</c>: Initiates a generic control action on multiple specified nodes. Requires Operator privileges.</description></item>
        /// </list>
        /// </remarks>
        public static IEndpointRouteBuilder MapNodesApi(this IEndpointRouteBuilder app, string guiHostConstraint)
        {
            var nodesGroup = app.MapGroup("/api/v1/nodes")
                .WithTags("Nodes")
                // General authorization for the group; specific endpoints also verify roles.
                .RequireAuthorization()
                .RequireHost(guiHostConstraint);

            // Defines GET /api/v1/nodes/{nodeName}
            // Retrieves detailed information for a specific node.
            // Requires Observer role. Calls INodeService.GetNodeDetailsAsync.
            nodesGroup.MapGet("/{nodeName}",
            /// <summary>
            /// Retrieves detailed information for a specific node, identified by its name.
            /// Requires Observer or higher privileges.
            /// </summary>
            /// <param name="nodeName">The unique name of the node to retrieve details for.</param>
            /// <param name="nodeService">The <see cref="INodeService"/> for fetching node details.</param>
            /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
            /// <returns>
            /// An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with a <see cref="NodeDetailsResponse"/> on success.
            /// Returns <see cref="Results.NotFound(object?)"/> with an <see cref="ErrorResponse"/> if the node is not found.
            /// Returns <see cref="Results.Forbid()"/> if unauthorized.
            /// </returns>
            async (string nodeName, [FromServices] INodeService nodeService, ClaimsPrincipal user) =>
            {
                if (!user.IsObserverOrHigher()) return Results.Forbid();
                var nodeDetails = await nodeService.GetNodeDetailsAsync(nodeName);
                return nodeDetails != null ? Results.Ok(nodeDetails) : Results.NotFound(new ErrorResponse(error: "NODE_NOT_FOUND", message: $"Node '{nodeName}' not found."));
            }).WithSummary("Get Node Details").Produces<NodeDetailsResponse>().Produces<ErrorResponse>(StatusCodes.Status404NotFound);

            // Defines GET /api/v1/nodes/{nodeName}/packages
            // Retrieves a list of packages installed on a specific node.
            // Requires Observer role. Calls INodeService.ListNodePackagesAsync.
            nodesGroup.MapGet("/{nodeName}/packages",
            /// <summary>
            /// Retrieves a list of software packages installed on a specific node, identified by its name.
            /// Requires Observer or higher privileges.
            /// </summary>
            /// <param name="nodeName">The unique name of the node whose packages are to be listed.</param>
            /// <param name="nodeService">The <see cref="INodeService"/> for fetching package information.</param>
            /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
            /// <returns>
            /// An <see cref="IResult"/> that is <see cref="Results.Ok(object?)"/> with a list of <see cref="PackageOnNode"/> DTOs on success.
            /// Returns <see cref="Results.NotFound(object?)"/> with an <see cref="ErrorResponse"/> if the node is not found or has no package information.
            /// Returns <see cref="Results.Forbid()"/> if unauthorized.
            /// </returns>
            async (string nodeName, [FromServices] INodeService nodeService, ClaimsPrincipal user) =>
            {
                if (!user.IsObserverOrHigher()) return Results.Forbid();
                var packages = await nodeService.ListNodePackagesAsync(nodeName);
                return packages != null ? Results.Ok(packages) : Results.NotFound(new ErrorResponse(error: "NODE_PACKAGES_NOT_FOUND", message: $"Packages for node '{nodeName}' not found or node does not exist."));
            }).WithSummary("List Packages on a Node").Produces<List<PackageOnNode>>().Produces<ErrorResponse>(StatusCodes.Status404NotFound);

            // POST /nodes/restart
            // Initiates a restart operation for specified node(s).
            // Requires Operator role. Calls IMasterActionCoordinatorService.InitiateMasterActionAsync.
            // Request body: NodeActionRequest. Response: 202 Accepted with OperationInitiationResponse.
            nodesGroup.MapPost("/restart",
                /// <summary>
                /// Initiates a restart operation for the node(s) specified in the <see cref="NodeActionRequest"/>.
                /// Requires Operator or higher privileges.
                /// </summary>
                /// <param name="request">The <see cref="NodeActionRequest"/> specifying which nodes to restart (either a list of names or all nodes).</param>
                /// <param name="opService">The <see cref="IMasterActionCoordinatorService"/> to initiate the node restart operation.</param>
                /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
                /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> for creating a logger instance.</param>
                /// <returns>
                /// An <see cref="IResult"/> that is <see cref="Results.Accepted(string, object)"/> with an <see cref="OperationInitiationResponse"/> on successful initiation.
                /// Returns <see cref="Results.BadRequest(object)"/> if the request is invalid (e.g., no nodes specified).
                /// Returns <see cref="Results.Forbid()"/> if unauthorized (handled by group policy).
                /// </returns>
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
                        // Ensure NodeNames is passed, even if null (handler should expect nullable list if AllNodes is true)
                        { "nodeNames", request.NodeNames ?? new List<string>() },
                        { "allNodes", request.AllNodes ?? false }
                    };

                    var initiateRequest = new OperationInitiateRequest
                    {
                        OperationType = OperationType.NodeRestart,
                        Parameters = parameters,
                        Description = request.AllNodes == true ? "Restarting all applicable nodes." : $"Restarting nodes: {string.Join(", ", request.NodeNames ?? new List<string>())}"
                    };

                    var operation = await opService.InitiateMasterActionAsync(initiateRequest, user);
                    // Audit logging for node actions like restart/shutdown is typically handled by the MasterActionHandler itself.

                    var response = new OperationInitiationResponse { OperationId = operation.Id, Message = $"Node restart operation '{operation.Id}' initiated." };
                    return Results.Accepted($"/api/v1/operations/{response.OperationId}", response);
                })
                .WithName("RestartNodes")
                .WithTags("Operations") // As per Swagger, though grouped under Nodes, actions are Operations
                .WithSummary("Restart Node(s)")
                .WithDescription("Initiates restart for specified node(s). Requires Operator role. Node action operations are audit logged by the workflow.")
                .Produces<OperationInitiationResponse>(StatusCodes.Status202Accepted)
                .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsOperatorOrHigher()));

            // POST /nodes/shutdown
            // Initiates a shutdown operation for specified node(s).
            // Requires Operator role. Calls IMasterActionCoordinatorService.InitiateMasterActionAsync.
            // Request body: NodeActionRequest. Response: 202 Accepted with OperationInitiationResponse.
            nodesGroup.MapPost("/shutdown",
                /// <summary>
                /// Initiates a shutdown operation for the node(s) specified in the <see cref="NodeActionRequest"/>.
                /// Requires Operator or higher privileges.
                /// </summary>
                /// <param name="request">The <see cref="NodeActionRequest"/> specifying which nodes to shut down.</param>
                /// <param name="opService">The <see cref="IMasterActionCoordinatorService"/> to initiate the node shutdown operation.</param>
                /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
                /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> for creating a logger instance.</param>
                /// <returns>
                /// An <see cref="IResult"/> that is <see cref="Results.Accepted(string, object)"/> with an <see cref="OperationInitiationResponse"/> on successful initiation.
                /// Returns <see cref="Results.BadRequest(object)"/> if the request is invalid.
                /// Returns <see cref="Results.Forbid()"/> if unauthorized.
                /// </returns>
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
                        { "nodeNames", request.NodeNames ?? new List<string>() },
                        { "allNodes", request.AllNodes ?? false }
                    };

                    var initiateRequest = new OperationInitiateRequest
                    {
                        OperationType = OperationType.NodeShutdown,
                        Parameters = parameters,
                        Description = request.AllNodes == true ? "Shutting down all applicable nodes." : $"Shutting down nodes: {string.Join(", ", request.NodeNames ?? new List<string>())}"
                    };

                    var operation = await opService.InitiateMasterActionAsync(initiateRequest, user);
                    // Audit logging for node actions like restart/shutdown is typically handled by the MasterActionHandler itself.

                    var response = new OperationInitiationResponse { OperationId = operation.Id, Message = $"Node shutdown operation '{operation.Id}' initiated." };
                    return Results.Accepted($"/api/v1/operations/{response.OperationId}", response);
                })
                .WithName("ShutdownNodes")
                .WithTags("Operations")
                .WithSummary("Shutdown Node(s)")
                .WithDescription("Initiates shutdown for specified node(s). Requires Operator role. Node action operations are audit logged by the workflow.")
                .Produces<OperationInitiationResponse>(StatusCodes.Status202Accepted)
                .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .RequireAuthorization(policy => policy.RequireAssertion(context => context.User.IsOperatorOrHigher()));

            // Defines POST /api/v1/nodes/{nodeName}/control/{action}
            // Initiates a generic control action on a specific node.
            // Requires Operator role. Calls IMasterActionCoordinatorService and IAuditLogService.
            nodesGroup.MapPost("/{nodeName}/control/{action}",
                /// <summary>
                /// Initiates a generic control action on a specified node.
                /// The nature of the action is determined by the <paramref name="action"/> route parameter.
                /// Requires Operator or higher privileges.
                /// </summary>
                /// <param name="nodeName">The name of the target node.</param>
                /// <param name="action">The control action to perform (e.g., "trigger-vnc", "force-check-in").</param>
                /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
                /// <param name="masterActionService">The <see cref="IMasterActionCoordinatorService"/> to initiate the control operation.</param>
                /// <param name="auditLog">The <see cref="IAuditLogService"/> for recording the action.</param>
                /// <param name="httpContext">The <see cref="HttpContext"/> for accessing request details.</param>
                /// <returns>
                /// An <see cref="IResult"/> that is <see cref="Results.Accepted(string, object)"/> with an <see cref="OperationInitiationResponse"/>.
                /// Returns <see cref="Results.Forbid()"/> if unauthorized.
                /// </returns>
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

                    // Group policy already requires OperatorOrHigher, this is an additional check if needed, or could be more specific.
                    // For generic control, OperatorOrHigher is likely sufficient.
                    if (!user.IsOperatorOrHigher()) return Results.Forbid();

                    var parameters = new Dictionary<string, object>
                    {
                        { "nodeName", nodeName }, // Specific node target
                        { "controlAction", action } // The specific control action requested
                    };

                    var initiateRequest = new OperationInitiateRequest
                    {
                        OperationType = OperationType.NodeControl, // Generic type for single node control
                        Parameters = parameters,
                        Description = $"Performing action '{action}' on node '{nodeName}'"
                    };

                    var masterAction = await masterActionService.InitiateMasterActionAsync(initiateRequest, user);

                    await auditLog.LogActionAsync(
                        username: username ?? "unknown",
                        action: $"NodeControl::{action}", // Audit log action type
                        targetResource: $"Node:{nodeName}",
                        parameters: new Dictionary<string, object> { { "operationId", masterAction.Id }, { "requestedAction", action } },
                        outcome: AuditLogOutcome.Success.ToString(), // Audit success of initiation
                        details: $"Node control action '{action}' initiated for node '{nodeName}'. MasterActionId: {masterAction.Id}.",
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

            // Defines POST /api/v1/nodes/control/{action}
            // Initiates a generic control action on multiple specified nodes.
            // Requires Operator role. Calls IMasterActionCoordinatorService and IAuditLogService.
            // Request body: NodeActionRequest.
            nodesGroup.MapPost("/control/{action}",
                /// <summary>
                /// Initiates a generic control action on multiple specified nodes.
                /// The nature of the action is determined by the <paramref name="action"/> route parameter.
                /// Node targets are specified in the <see cref="NodeActionRequest"/> body.
                /// Requires Operator or higher privileges.
                /// </summary>
                /// <param name="action">The control action to perform on the specified nodes.</param>
                /// <param name="request">The <see cref="NodeActionRequest"/> specifying the target nodes.</param>
                /// <param name="user">The <see cref="ClaimsPrincipal"/> representing the authenticated user.</param>
                /// <param name="masterActionService">The <see cref="IMasterActionCoordinatorService"/> to initiate the control operation.</param>
                /// <param name="auditLog">The <see cref="IAuditLogService"/> for recording the action.</param>
                /// <param name="httpContext">The <see cref="HttpContext"/> for accessing request details.</param>
                /// <returns>
                /// An <see cref="IResult"/> that is <see cref="Results.Accepted(string, object)"/> with an <see cref="OperationInitiationResponse"/>.
                /// Returns <see cref="Results.BadRequest(object)"/> if the request is invalid.
                /// Returns <see cref="Results.Forbid()"/> if unauthorized.
                /// </returns>
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

                    // Group policy already requires OperatorOrHigher
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
                        { "controlAction", action }, // The specific control action from path
                        { "nodeNames", request.NodeNames ?? new List<string>() },
                        { "allNodes", request.AllNodes ?? false }
                    };

                    var initiateRequest = new OperationInitiateRequest
                    {
                        OperationType = OperationType.MultiNodeControl, // Generic type for multi-node control
                        Parameters = parameters,
                        Description = $"Performing action '{action}' on nodes: {(request.AllNodes == true ? "All" : string.Join(", ", request.NodeNames ?? new List<string>()))}"
                    };

                    var masterAction = await masterActionService.InitiateMasterActionAsync(initiateRequest, user);

                    await auditLog.LogActionAsync(
                        username: username ?? "unknown",
                        action: $"MultiNodeControl::{action}", // Audit log action type
                        targetResource: request.AllNodes == true ? "AllNodes" : $"Nodes:{string.Join(",", request.NodeNames ?? new List<string>())}",
                        parameters: new Dictionary<string, object>
                        {
                            { "nodes", request.NodeNames ?? new List<string>() },
                            { "allNodes", request.AllNodes ?? false },
                            { "operationId", masterAction.Id },
                            { "requestedAction", action }
                        },
                        outcome: AuditLogOutcome.Success.ToString(), // Audit success of initiation
                        details: $"Multi-node control action '{action}' initiated for specified nodes. MasterActionId: {masterAction.Id}.",
                        clientIpAddress: httpContext.GetClientIpAddress()
                    );

                    var response = new OperationInitiationResponse
                    {
                        OperationId = masterAction.Id,
                        Message = $"Multi-node control action '{action}' for specified nodes initiated."
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