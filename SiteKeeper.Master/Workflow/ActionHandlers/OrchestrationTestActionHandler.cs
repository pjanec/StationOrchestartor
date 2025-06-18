using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Abstractions.Workflow;
using SiteKeeper.Master.Model.InternalData;
using SiteKeeper.Shared.DTOs.API.Operations;
using SiteKeeper.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Workflow.ActionHandlers
{
    /// <summary>
    /// Implements <see cref="IMasterActionHandler"/> to orchestrate the <see cref="OperationType.OrchestrationTest"/>.
    /// This handler is designed for end-to-end testing of the master-slave operation workflow.
    /// </summary>
    /// <remarks>
    /// It reads specific test parameters from the <see cref="MasterActionContext.Parameters"/>, such as
    /// desired slave behavior (<see cref="SlaveBehaviorMode"/>) and master failure simulation points (<see cref="MasterFailureMode"/>).
    /// It then constructs and executes a single-node operation using the <see cref="MultiNodeOperationStageHandler"/>,
    /// where the <see cref="SlaveTaskType.TestOrchestration"/> task on the slave agent will enact the requested simulated behavior.
    /// The handler also interacts with the <see cref="IJournalService"/> to record the test operation's lifecycle in the Change Journal.
    /// This handler is critical for verifying the robustness of the operation coordination, error handling, cancellation, and timeout mechanisms.
    /// </remarks>
    public class OrchestrationTestActionHandler : IMasterActionHandler
    {
        private readonly IStageHandler<MultiNodeOperationInput, MultiNodeOperationResult> _multiNodeHandler;
        private readonly IAgentConnectionManagerService _agentConnectionManager; // Kept for consistency, though OperationBuilder might be primary user now
        private readonly IJournalService _journalService;

        /// <summary>
        /// Gets the type of operation this handler is responsible for, which is <see cref="OperationType.OrchestrationTest"/>.
        /// </summary>
        public OperationType Handles => OperationType.OrchestrationTest;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrchestrationTestActionHandler"/> class.
        /// </summary>
        /// <param name="multiNodeHandler">The stage handler for executing multi-node operations, used here for the single test task.</param>
        /// <param name="agentConnectionManager">Service for agent communication details (though less directly used if OperationBuilder handles node selection).</param>
        /// <param name="journalService">Service for recording system change events related to the test operation.</param>
        public OrchestrationTestActionHandler(
            IStageHandler<MultiNodeOperationInput, MultiNodeOperationResult> multiNodeHandler,
            IAgentConnectionManagerService agentConnectionManager,
            IJournalService journalService)
        {
            _multiNodeHandler = multiNodeHandler;
            _agentConnectionManager = agentConnectionManager; // May not be directly used if OperationBuilder handles node discovery
            _journalService = journalService;
        }

        /// <summary>
        /// Executes the orchestration test workflow.
        /// </summary>
        /// <param name="context">The <see cref="MasterActionContext"/> providing workflow services and parameters for the test.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous execution of the workflow.</returns>
        /// <remarks>
        /// The workflow performs these main steps:
        /// <list type="number">
        ///   <item><description>Extracts and validates test parameters (<c>slaveBehavior</c>, <c>masterFailure</c>, <c>targetNodeName</c>, <c>customMessage</c>) from <see cref="MasterActionContext.Parameters"/>.</description></item>
        ///   <item><description>Simulates master failure before any stage execution if <see cref="MasterFailureMode.ThrowBeforeFirstStage"/> is requested.</description></item>
        ///   <item><description>Logs any provided <c>customMessage</c> to the master log.</description></item>
        ///   <item><description>Initiates a <see cref="ChangeEventType.SystemEvent"/> record in the Change Journal for the test.</description></item>
        ///   <item><description>Executes a "MultiNodeTestStage":
        ///     <list type="bullet">
        ///       <item><description>Constructs an <see cref="Operation"/> with a single <see cref="NodeTask"/> of type <see cref="SlaveTaskType.TestOrchestration"/>, targeted at the specified <c>targetNodeName</c>.</description></item>
        ///       <item><description>The <see cref="NodeTask.TaskPayload"/> includes all original test parameters for the slave to interpret.</description></item>
        ///       <item><description>Invokes the injected <see cref="_multiNodeHandler"/> to execute this operation.</description></item>
        ///     </list>
        ///   </description></item>
        ///   <item><description>Simulates master failure after the main stage if <see cref="MasterFailureMode.ThrowAfterFirstStage"/> is requested.</description></item>
        ///   <item><description>Based on the <see cref="MultiNodeOperationResult"/>, sets the final outcome (Succeeded, Failed, Cancelled) on the <paramref name="context"/>.</description></item>
        ///   <item><description>Finalizes the Change Journal record with the test outcome.</description></item>
        /// </list>
        /// Exceptions during the workflow are caught to ensure the Change Journal record is finalized with a failure state, then re-thrown.
        /// </remarks>
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
            if (context.Parameters.TryGetValue("customMessage", out var customMessageObj))
            {
                string? customMessage = null;
                if (customMessageObj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
                {
                    customMessage = jsonElement.GetString();
                }
                else if (customMessageObj is string str) 
                {
                    customMessage = str;
                }

                if (!string.IsNullOrEmpty(customMessage))
                {
                    context.LogInfo($"MASTER-LOG: {customMessage}");
                }
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
