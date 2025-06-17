using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Abstractions.Workflow;
using SiteKeeper.Master.Model.InternalData;
using SiteKeeper.Shared.DTOs.API.Operations;
using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Workflow.ActionHandlers
{
    /// <summary>
    /// An IMasterActionHandler specifically for the 'OrchestrationTest' operation type.
    /// This handler reads test parameters from the context to simulate various success
    /// and failure scenarios on both the master and slave sides.
    /// </summary>
    public class OrchestrationTestActionHandler : IMasterActionHandler
    {
        private readonly IStageHandler<MultiNodeOperationInput, MultiNodeOperationResult> _multiNodeHandler;
        private readonly IAgentConnectionManagerService _agentConnectionManager;
        private readonly IJournalService _journalService;

        public OperationType Handles => OperationType.OrchestrationTest;

        public OrchestrationTestActionHandler(
            IStageHandler<MultiNodeOperationInput, MultiNodeOperationResult> multiNodeHandler,
            IAgentConnectionManagerService agentConnectionManager,
            IJournalService journalService)
        {
            _multiNodeHandler = multiNodeHandler;
            _agentConnectionManager = agentConnectionManager;
            _journalService = journalService;
        }

        public async Task ExecuteAsync(MasterActionContext context)
        {
            context.InitializeProgress(totalSteps: 2); // 1. Main test stage, 2. Finalization
            context.LogInfo("Starting Orchestration Test workflow...");

            // --- 1. Extract and Validate Parameters ---
            // The parameters are deserialized from the API request into the context's dictionary.
            // We need to safely extract and parse them.
            if (!context.Parameters.TryGetValue("slaveBehavior", out var slaveBehaviorObj) || !Enum.TryParse<SlaveBehaviorMode>(slaveBehaviorObj.ToString(), out var slaveBehavior))
            {
                context.SetFailed("Invalid or missing 'slaveBehavior' parameter for OrchestrationTest.");
                return;
            }

            if (!context.Parameters.TryGetValue("masterFailure", out var masterFailureObj) || !Enum.TryParse<MasterFailureMode>(masterFailureObj.ToString(), out var masterFailure))
            {
                context.SetFailed("Invalid or missing 'masterFailure' parameter for OrchestrationTest.");
                return;
            }
            
            context.Parameters.TryGetValue("targetNodeName", out var targetNodeNameObj);
            var targetNodeName = targetNodeNameObj?.ToString();
            if (string.IsNullOrEmpty(targetNodeName))
            {
                context.SetFailed("Missing 'targetNodeName' parameter for OrchestrationTest.");
                return;
            }

            // --- 2. Simulate Master Failure (Before any stages) ---
            if (masterFailure == MasterFailureMode.ThrowBeforeFirstStage)
            {
                context.LogInfo("SIMULATOR: Throwing exception before any stage as requested by MasterFailureMode.");
                throw new InvalidOperationException("Simulated master failure before first stage.");
            }

            // --- 3. Log Custom Message (if provided) ---
            // This is used for testing master-side logging.
            if (context.Parameters.TryGetValue("customMessage", out var customMessageObj) && customMessageObj is string customMessage && !string.IsNullOrEmpty(customMessage))
            {
                context.LogInfo($"MASTER-LOG: {customMessage}");
            }

            // We create a journaled system event for this test run.
            var changeRecord = await _journalService.InitiateStateChangeAsync(new Abstractions.Services.Journaling.StateChangeInfo
            {
                Type = Abstractions.Services.Journaling.ChangeEventType.SystemEvent,
                Description = $"Initiating orchestration test (Master: {masterFailure}, Slave: {slaveBehavior})",
                InitiatedBy = context.MasterAction.InitiatedBy ?? "system-test",
                SourceMasterActionId = context.MasterActionId
            });

            try
            {
                // --- 4. Execute the Multi-Node Stage ---
                await context.BeginStageAsync("MultiNodeTestStage");

                var operationToRun = new Operation(
                    id: $"op-test-{Guid.NewGuid():N}",
                    type: OperationType.OrchestrationTest,
                    name: "Orchestration Test Stage",
                    initiatedBy: context.MasterAction.InitiatedBy);

                var taskPayload = new Dictionary<string, object>(context.Parameters);
                var nodeTask = new NodeTask(
                    taskId: $"{operationToRun.Id}-{targetNodeName}",
                    operationId: operationToRun.Id,
                    nodeName: targetNodeName,
                    taskType: SlaveTaskType.TestOrchestration,
                    taskPayload: taskPayload
                );
                operationToRun.NodeTasks.Add(nodeTask);

                var multiNodeInput = new MultiNodeOperationInput { OperationToExecute = operationToRun };
                var multiNodeResult = await _multiNodeHandler.ExecuteAsync(multiNodeInput, context, context.StageProgress, context.CancellationToken);

                context.MasterAction.CurrentStageOperation = multiNodeResult.FinalOperationState;
                await context.CompleteStageAsync(multiNodeResult);

                // --- 5. Simulate Master Failure (After first stage) ---
                if (masterFailure == MasterFailureMode.ThrowAfterFirstStage)
                {
                    context.LogInfo("SIMULATOR: Throwing exception after first stage as requested by MasterFailureMode.");
                    throw new InvalidOperationException("Simulated master failure after first stage.");
                }

                // --- 6. Finalize Workflow State ---
                await context.BeginStageAsync("Finalization");
                if (multiNodeResult.IsSuccess)
                {
                    context.SetFinalResult(multiNodeResult.FinalOperationState.NodeTasks.FirstOrDefault()?.ResultPayload);
                    context.SetCompleted("Orchestration Test completed successfully.");
                    await _journalService.FinalizeStateChangeAsync(new Abstractions.Services.Journaling.StateChangeFinalizationInfo { ChangeId = changeRecord.ChangeId, Outcome = OperationOutcome.Success, Description = "Test Succeeded", ResultArtifact = multiNodeResult });
                }
                // Add a specific check for the Cancelled status
                else if (multiNodeResult.FinalOperationState.OverallStatus == OperationOverallStatus.Cancelled)
                {
                    var cancelMessage = multiNodeResult.FinalOperationState.NodeTasks.FirstOrDefault(t => t.Status == NodeTaskStatus.Cancelled)?.StatusMessage ?? "Multi-node test stage was cancelled.";
                    // Use the SetCancelled method on the context
                    context.SetCancelled($"Orchestration Test was cancelled: {cancelMessage}");
                    await _journalService.FinalizeStateChangeAsync(new Abstractions.Services.Journaling.StateChangeFinalizationInfo { ChangeId = changeRecord.ChangeId, Outcome = OperationOutcome.Cancelled, Description = "Test Cancelled", ResultArtifact = multiNodeResult });
                }
                else  // Everything else is a failure
                {
                    var failureMessage = multiNodeResult.FinalOperationState.NodeTasks.FirstOrDefault()?.StatusMessage ?? "Multi-node test stage failed.";
                    context.SetFailed($"Orchestration Test failed: {failureMessage}");
                    await _journalService.FinalizeStateChangeAsync(new Abstractions.Services.Journaling.StateChangeFinalizationInfo { ChangeId = changeRecord.ChangeId, Outcome = OperationOutcome.Failure, Description = "Test Failed", ResultArtifact = multiNodeResult });
                }
                await context.CompleteStageAsync();
            }
            catch (Exception ex)
            {
                // Finalize the journal entry with a failure state if any exception bubbles up
                await _journalService.FinalizeStateChangeAsync(new Abstractions.Services.Journaling.StateChangeFinalizationInfo
                {
                    ChangeId = changeRecord.ChangeId,
                    Outcome = OperationOutcome.Failure,
                    Description = $"Test workflow failed with exception: {ex.Message}",
                    ResultArtifact = new { Error = ex.ToString() }
                });

                // Re-throw the exception so the MasterActionCoordinator can catch it and mark the MasterAction as Failed.
                throw;
            }
        }
    }
}
