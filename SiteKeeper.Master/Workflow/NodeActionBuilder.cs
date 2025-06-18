using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Abstractions.Workflow;
using SiteKeeper.Master.Model.InternalData;
using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Workflow
{
    /// <summary>
    /// A static helper class to encapsulate common logic for building multi-node actions.
    /// This reduces boilerplate code within IMasterActionHandler implementations by providing a
    /// standardized way to create a NodeAction object with a list of tasks targeting all nodes.
    /// </summary>
    /// <remarks>
    /// This class is primarily used by implementations of <see cref="IMasterActionHandler"/>
    /// when a part of their workflow involves dispatching a common task to multiple, or typically all,
    /// connected slave agents. It simplifies the creation of the <see cref="NodeAction"/>
    /// data structure, which is then often processed by a <see cref="NodeCoordinator"/>
    /// or a similar mechanism that iterates through <see cref="NodeAction.NodeTasks"/>.
    /// </remarks>
    public static class NodeActionBuilder
    {
        /// <summary>
        /// Creates a new NodeAction object that targets all currently connected slave agents with a specified task.
        /// </summary>
        /// <remarks>
        /// The method performs the following steps:
        /// 1. Initializes a new <see cref="NodeAction"/> object using the provided <paramref name="operationType"/> (soon to be actionType), <paramref name="operationName"/> (soon to be actionName),
        ///    <paramref name="auditContext"/>, and user information from the <paramref name="context"/>.
        /// 2. Retrieves all currently connected slave agents using the <paramref name="agentConnectionManager"/>.
        /// 3. If no agents are connected, the method logs this information via the <paramref name="context"/> and returns the
        ///    <see cref="NodeAction"/> with an empty <see cref="NodeAction.NodeTasks"/> list.
        /// 4. For each connected agent, it creates a <see cref="NodeTask"/>.
        ///    - The <see cref="NodeTask.TaskType"/> is set to the common <paramref name="slaveTaskType"/>.
        ///    - The <see cref="NodeTask.TaskPayload"/> is determined by checking if a specific payload is provided for the agent's node name
        ///      in <paramref name="nodeSpecificPayloads"/>. If not, an empty dictionary is used as the payload.
        /// 5. Each created <see cref="NodeTask"/> is added to the <see cref="NodeAction.NodeTasks"/> list of the parent node action.
        /// 6. Finally, it logs the successful creation of the node action and the number of tasks generated via the <paramref name="context"/>.
        /// </remarks>
        /// <param name="context">The MasterActionContext of the running workflow, providing overall context like IDs and user info.</param>
        /// <param name="agentConnectionManager">The service used to retrieve the list of connected agents.</param>
        /// <param name="operationType">The high-level type of the operation/action being created (e.g., EnvVerify). This is used for journaling and context.</param>
        /// <param name="operationName">A user-friendly name for this action instance, used in logs.</param>
        /// <param name="slaveTaskType">The specific type of task that will be assigned to each slave agent.</param>
        /// <param name="auditContext">An optional dictionary of high-level business parameters (e.g., planId, targetVersion) to be stored in the journal for auditing. This data is not used for execution logic.</param>
        /// <param name="nodeSpecificPayloads">An optional dictionary that maps a specific node name to a unique payload. If a node name exists here, this payload will be used instead of a common payload.</param>
        /// <returns>A fully initialized NodeAction object ready to be executed by the NodeCoordinator.</returns>
        public static async Task<NodeAction> CreateActionForAllNodesAsync(
            MasterActionContext context,
            IAgentConnectionManagerService agentConnectionManager,
            OperationType operationType, // Parameter name can be updated later
            string operationName, // Parameter name can be updated later
            SlaveTaskType slaveTaskType,
            Dictionary<string, object>? auditContext = null,
            Dictionary<string, Dictionary<string, object>>? nodeSpecificPayloads = null)
        {
            // 1. Create the parent NodeAction object using information from the Master Action context.
            var actionId = $"na-{operationType.ToString().ToLower()}-{Guid.NewGuid():N}"; // op- prefix changed to na-
            var nodeAction = new NodeAction( // Renamed from operation
                id: actionId, // Renamed from operationId
                type: operationType,
                name: operationName,
                auditContext: auditContext,
                initiatedBy: context.MasterAction.InitiatedBy
            );

            // 2. Get all currently connected agents who will be the targets for our tasks.
            var allAgents = await agentConnectionManager.GetAllConnectedAgentsAsync();
            if (!allAgents.Any())
            {
                context.LogInfo($"NodeAction '{operationName}' created, but no connected agents were found to assign tasks to.");
                return nodeAction;
            }

            // 3. Loop through each agent to create a specific NodeTask for it.
            foreach (var agent in allAgents)
            {
                // For this helper, we assume a common task type for all nodes.
                // The payload can be node-specific if provided.
                var payloadForThisNode = nodeSpecificPayloads?.GetValueOrDefault(agent.NodeName)
                                       ?? new Dictionary<string, object>();

                var nodeTask = new NodeTask(
                    taskId: $"{actionId}-{agent.NodeName}", // Use actionId
                    actionId: nodeAction.Id, // Pass actionId to NodeTask constructor
                    nodeName: agent.NodeName,
                    taskType: slaveTaskType,
                    taskPayload: payloadForThisNode
                );
                nodeAction.NodeTasks.Add(nodeTask);
            }

            context.LogInfo($"Created NodeAction '{operationName}' ({nodeAction.Id}) with {nodeAction.NodeTasks.Count} tasks targeting all connected nodes.");
            return nodeAction;
        }
    }
}