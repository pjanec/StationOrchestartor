using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Abstractions.Workflow;
using SiteKeeper.Shared.Enums;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SiteKeeper.Master.Workflow.ActionHandlers
{
    public class EnvVerifyActionHandler : IMasterActionHandler
    {
        private readonly IStageHandler<MultiNodeOperationInput, MultiNodeOperationResult> _multiNodeHandler;
        private readonly IAgentConnectionManagerService _agentConnectionManager;
        public OperationType Handles => OperationType.EnvVerify;

        public EnvVerifyActionHandler(
            IStageHandler<MultiNodeOperationInput, MultiNodeOperationResult> multiNodeHandler,
            IAgentConnectionManagerService agentConnectionManager)
        {
            _multiNodeHandler = multiNodeHandler;
            _agentConnectionManager = agentConnectionManager;
        }

        public async Task ExecuteAsync(MasterActionContext context)
        {
            context.InitializeProgress(totalSteps: 1);
            context.LogInfo("Starting Environment Verification workflow...");

            try
            {
                await context.BeginStageAsync("Verification");

                var operationToRun = await OperationBuilder.CreateOperationForAllNodesAsync(
                    context: context,
                    agentConnectionManager: _agentConnectionManager,
                    operationType: OperationType.EnvVerify,
                    operationName: "Environment Verification Stage",
                    slaveTaskType: SlaveTaskType.VerifyConfiguration
                );

                var input = new MultiNodeOperationInput { OperationToExecute = operationToRun };
                var result = await _multiNodeHandler.ExecuteAsync(input, context, context.StageProgress, context.CancellationToken);

                context.MasterAction.CurrentStageOperation = result.FinalOperationState;
                if (!result.IsSuccess)
                    throw new System.Exception("Environment verification stage failed.");

                context.SetCompleted("Environment Verification completed successfully.");
            }
            catch (System.Exception ex)
            {
                context.SetFailed(ex.Message);
            }
        }
    }
} 