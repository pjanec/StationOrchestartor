using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Abstractions.Workflow;
using SiteKeeper.Shared.Enums;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SiteKeeper.Master.Workflow.ActionHandlers
{
    /// <summary>
    /// Implements <see cref="IMasterActionHandler"/> to orchestrate the <see cref="OperationType.EnvVerify"/> workflow.
    /// </summary>
    /// <remarks>
    /// This handler is responsible for verifying the integrity and configuration of the entire managed environment.
    /// It achieves this by preparing and executing a multi-node operation that dispatches
    /// <see cref="SlaveTaskType.VerifyConfiguration"/> tasks to all relevant slave agents.
    /// The <see cref="MultiNodeOperationStageHandler"/> is used to manage the execution of these distributed tasks.
    /// </remarks>
    public class EnvVerifyActionHandler : IMasterActionHandler
    {
        private readonly IStageHandler<MultiNodeOperationInput, MultiNodeOperationResult> _multiNodeHandler;
        private readonly IAgentConnectionManagerService _agentConnectionManager;

        /// <summary>
        /// Gets the type of operation this handler is responsible for, which is <see cref="OperationType.EnvVerify"/>.
        /// </summary>
        public OperationType Handles => OperationType.EnvVerify;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnvVerifyActionHandler"/> class.
        /// </summary>
        /// <param name="multiNodeHandler">The stage handler responsible for executing operations across multiple nodes.
        /// This is used to run the <see cref="SlaveTaskType.VerifyConfiguration"/> tasks on agents.</param>
        /// <param name="agentConnectionManager">The service used to retrieve information about connected agents,
        /// necessary for determining the target nodes for the verification tasks.</param>
        public EnvVerifyActionHandler(
            IStageHandler<MultiNodeOperationInput, MultiNodeOperationResult> multiNodeHandler,
            IAgentConnectionManagerService agentConnectionManager)
        {
            _multiNodeHandler = multiNodeHandler;
            _agentConnectionManager = agentConnectionManager;
        }

        /// <summary>
        /// Executes the environment verification workflow.
        /// </summary>
        /// <param name="context">The <see cref="MasterActionContext"/> providing workflow services like logging, journaling, progress reporting, and cancellation monitoring.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous execution of the workflow.</returns>
        /// <remarks>
        /// The workflow performs the following steps:
        /// <list type="number">
        ///   <item><description>Initializes progress tracking for a single main stage.</description></item>
        ///   <item><description>Logs the start of the workflow.</description></item>
        ///   <item><description>Begins a "Verification" stage within the <paramref name="context"/>.</description></item>
        ///   <item><description>Uses <see cref="OperationBuilder.CreateOperationForAllNodesAsync"/> to construct an <see cref="Operation"/> object. This operation includes <see cref="NodeTask"/>s of type <see cref="SlaveTaskType.VerifyConfiguration"/> targeted at all relevant/connected nodes.</description></item>
        ///   <item><description>Wraps the created operation in a <see cref="MultiNodeOperationInput"/>.</description></item>
        ///   <item><description>Executes the multi-node verification operation using the injected <see cref="_multiNodeHandler"/>.</description></item>
        ///   <item><description>Stores the final state of the multi-node operation in <see cref="MasterAction.CurrentStageOperation"/> for status reporting.</description></item>
        ///   <item><description>If the multi-node operation stage fails, an exception is thrown to mark the workflow as failed.</description></item>
        ///   <item><description>Sets the final outcome (completed or failed) on the <paramref name="context"/>.</description></item>
        /// </list>
        /// The handler respects cancellation requests via <paramref name="context"/>.CancellationToken, which is passed to the <see cref="_multiNodeHandler"/>.
        /// </remarks>
        public async Task ExecuteAsync(MasterActionContext context)
        {
            context.InitializeProgress(totalSteps: 1); // A single main stage for verification
            context.LogInfo("Starting Environment Verification workflow...");

            try
            {
                await context.BeginStageAsync("Verification", new { Description = "Dispatching VerifyConfiguration tasks to all nodes." });

                // Create an Operation object that defines VerifyConfiguration tasks for all relevant nodes.
                var operationToRun = await OperationBuilder.CreateOperationForAllNodesAsync(
                    context: context, // Pass parent context for ID, parameters
                    agentConnectionManager: _agentConnectionManager, // To get list of nodes
                    operationType: OperationType.EnvVerify, // The type of the parent operation
                    operationName: "Environment Verification Stage", // Name for this specific sub-operation
                    slaveTaskType: SlaveTaskType.VerifyConfiguration // The task type for slaves
                    // TaskPayload can be added here if VerifyConfiguration tasks need specific input
                );

                var input = new MultiNodeOperationInput { OperationToExecute = operationToRun };
                var result = await _multiNodeHandler.ExecuteAsync(input, context, context.StageProgress, context.CancellationToken);

                context.MasterAction.CurrentStageOperation = result.FinalOperationState; // Store detailed outcome
                if (!result.IsSuccess)
                {
                    throw new System.Exception("Environment verification stage failed. Check individual node task statuses for details.");
                }

                context.SetCompleted("Environment Verification completed successfully.");
            }
            catch (TaskCanceledException)
            {
                context.SetCancelled("Environment Verification was cancelled by user request during the verification stage.");
            }
            catch (System.Exception ex)
            {
                context.SetFailed($"Environment Verification failed: {ex.Message}");
            }
        }
    }
} 