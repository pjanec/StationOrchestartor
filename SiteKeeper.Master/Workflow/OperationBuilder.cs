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
    /// A static helper class to encapsulate common logic for building multi-node operations.
    /// This reduces boilerplate code within IMasterActionHandler implementations by providing a
    /// standardized way to create an Operation object with a list of tasks targeting all nodes.
    /// </summary>
    public static class OperationBuilder
    {
        /// <summary>
        /// Creates a new Operation object that targets all currently connected slave agents with a specified task.
        /// </summary>
        /// <param name="context">The MasterActionContext of the running workflow, providing overall context like IDs and user info.</param>
        /// <param name="agentConnectionManager">The service used to retrieve the list of connected agents.</param>
        /// <param name="operationType">The high-level type of the operation being created (e.g., EnvVerify). This is used for journaling and context.</param>
        /// <param name="operationName">A user-friendly name for this operation instance, used in logs.</param>
        /// <param name="slaveTaskType">The specific type of task that will be assigned to each slave agent.</param>
        /// <param name="auditContext">An optional dictionary of high-level business parameters (e.g., planId, targetVersion) to be stored in the journal for auditing. This data is not used for execution logic.</param>
        /// <param name="nodeSpecificPayloads">An optional dictionary that maps a specific node name to a unique payload. If a node name exists here, this payload will be used instead of a common payload.</param>
        /// <returns>A fully initialized Operation object ready to be executed by the MultiNodeOperationStageHandler.</returns>
        public static async Task<Operation> CreateOperationForAllNodesAsync(
            MasterActionContext context,
            IAgentConnectionManagerService agentConnectionManager,
            OperationType operationType,
            string operationName,
            SlaveTaskType slaveTaskType,
            Dictionary<string, object>? auditContext = null,
            Dictionary<string, Dictionary<string, object>>? nodeSpecificPayloads = null)
        {
            // 1. Create the parent Operation object using information from the Master Action context.
            var operationId = $"op-{operationType.ToString().ToLower()}-{Guid.NewGuid():N}";
            var operation = new Operation(
                id: operationId,
                type: operationType,
                name: operationName,
                auditContext: auditContext, // Use the new AuditContext field
                initiatedBy: context.MasterAction.InitiatedBy
            );

            // 2. Get all currently connected agents who will be the targets for our tasks.
            var allAgents = await agentConnectionManager.GetAllConnectedAgentsAsync();
            if (!allAgents.Any())
            {
                context.LogInfo($"Operation '{operationName}' created, but no connected agents were found to assign tasks to.");
                return operation;
            }

            // 3. Loop through each agent to create a specific NodeTask for it.
            foreach (var agent in allAgents)
            {
                // For this helper, we assume a common task type for all nodes.
                // The payload can be node-specific if provided.
                var payloadForThisNode = nodeSpecificPayloads?.GetValueOrDefault(agent.NodeName)
                                       ?? new Dictionary<string, object>();

                var nodeTask = new NodeTask(
                    taskId: $"{operationId}-{agent.NodeName}",
                    operationId: operation.Id,
                    nodeName: agent.NodeName,
                    taskType: slaveTaskType,
                    taskPayload: payloadForThisNode
                );
                operation.NodeTasks.Add(nodeTask);
            }

            context.LogInfo($"Created operation '{operationName}' ({operation.Id}) with {operation.NodeTasks.Count} tasks targeting all connected nodes.");
            return operation;
        }
    }
} 