using SiteKeeper.Master.Abstractions.Workflow;
using SiteKeeper.Shared.Enums;
using System.Threading.Tasks;
using System;

namespace SiteKeeper.Master.Workflow.ActionHandlers
{
    public class EnvVerifyActionHandler : IMasterActionHandler
    {
        public OperationType Handles => OperationType.EnvVerify;

        public EnvVerifyActionHandler()
        {
        }

        public async Task ExecuteAsync(MasterActionContext context)
        {
            context.InitializeProgress(totalSteps: 1);
            context.LogInfo("Starting Environment Verification workflow...");

            try
            {
                await using (var stage = await context.BeginStageAsync("Verification", subActionCount: 1))
                {
                    var result = await stage.CreateAndExecuteNodeActionAsync(
                        actionName: "Environment Verification Stage",
                        slaveTaskType: SlaveTaskType.VerifyConfiguration
                    );
                    
                    if (!result.IsSuccess)
                        throw new Exception("Environment verification stage failed.");
                }

                context.SetCompleted("Environment Verification completed successfully.");
            }
            catch (Exception ex)
            {
                context.SetFailed(ex.Message);
            }
        }
    }
} 