// SiteKeeper.Master/Workflow/ActionHandlers/OrchestrationTestActionHandler.cs

using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Abstractions.Services.Journaling;
using SiteKeeper.Master.Abstractions.Workflow;
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
    /// An IMasterActionHandler specifically for the 'OrchestrationTest' operation type.
    /// This handler reads test parameters from the context to simulate various success
    /// and failure scenarios on both the master and slave sides, using the new StageContext pattern.
    /// </summary>
    public class OrchestrationTestActionHandler : IMasterActionHandler
    {
        private readonly IJournal _journalService;

        /// <summary>
        /// Gets the specific API operation type that this handler is responsible for.
        /// </summary>
        public OperationType Handles => OperationType.OrchestrationTest;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrchestrationTestActionHandler"/> class.
        /// With the new StageContext architecture, this handler only needs the IJournal service
        /// for creating high-level change records. Other services are resolved by the StageContext itself.
        /// </summary>
        public OrchestrationTestActionHandler(IJournal journalService)
        {
            _journalService = journalService;
        }

        /// <summary>
        /// Executes the entire test workflow. It parses parameters from the initial request,
        /// simulates master-side failures if requested, and uses a StageContext to execute
        /// a NodeAction that instructs the slave on how to behave.
        /// </summary>
        public async Task ExecuteAsync(MasterActionContext context)
        {
            context.LogInfo("Starting Orchestration Test workflow...");
            context.InitializeProgress(totalSteps: 1); // This entire handler represents one logical step.

            #region Parameter Extraction and Validation

            // Safely extract and parse all parameters from the context.
            // When deserialized from JSON, numeric values become JsonElement of kind Number,
            // booleans become kind True/False, and enums become kind String.
            // We need to handle these conversions robustly.

            // Extract SlaveBehaviorMode
            if (!context.Parameters.TryGetValue("slaveBehavior", out var slaveBehaviorObj) || !Enum.TryParse<SlaveBehaviorMode>(slaveBehaviorObj.ToString(), out var slaveBehavior))
            {
                context.SetFailed("Invalid or missing 'slaveBehavior' parameter for OrchestrationTest.");
                return;
            }

            // Extract MasterFailureMode
            if (!context.Parameters.TryGetValue("masterFailure", out var masterFailureObj) || !Enum.TryParse<MasterFailureMode>(masterFailureObj.ToString(), out var masterFailure))
            {
                context.SetFailed("Invalid or missing 'masterFailure' parameter for OrchestrationTest.");
                return;
            }

            // Extract TargetNodeName
            context.Parameters.TryGetValue("targetNodeName", out var targetNodeNameObj);
            var targetNodeName = (targetNodeNameObj as JsonElement?)?.GetString() ?? targetNodeNameObj?.ToString();
            if (string.IsNullOrEmpty(targetNodeName))
            {
                context.SetFailed("Invalid or missing 'targetNodeName' parameter for OrchestrationTest.");
                return;
            }
            
            #endregion

            // First, check for immediate failure simulation BEFORE creating any journal records.
            if (masterFailure == MasterFailureMode.ThrowBeforeFirstStage)
            {
                context.LogInfo("SIMULATOR: Throwing exception before any stage as requested by MasterFailureMode.");
                throw new InvalidOperationException("Simulated master failure before first stage.");
            }
            
            // Log any custom message passed in the parameters for testing master-side logging.
            if (context.Parameters.TryGetValue("customMessage", out var customMessageObj))
            {
                var customMessage = (customMessageObj as JsonElement?)?.GetString() ?? customMessageObj?.ToString();
                if (!string.IsNullOrEmpty(customMessage))
                {
                    context.LogInfo($"MASTER-LOG: {customMessage}");
                }
            }

            // Now, create the journal record since immediate failure was not requested.
            var changeRecord = await _journalService.InitiateStateChangeAsync(new StateChangeInfo
            {
                Type = ChangeEventType.SystemEvent,
                Description = $"Initiating orchestration test (Master: {masterFailure}, Slave: {slaveBehavior})",
                InitiatedBy = context.MasterAction.InitiatedBy ?? "system-test",
                SourceMasterActionId = context.MasterActionId
            });

            var testOpRequest = JsonSerializer.Deserialize<TestOpRequest>(
                JsonSerializer.Serialize(context.Parameters), 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? new TestOpRequest();



            try
            {
                // Check if we are running the new parallel test mode.
                if (testOpRequest.RunInParallel == true)
                {
                    await using (var stage = await context.BeginStageAsync("Parallel Action Stage"))
                    {
                        var parallelActionInputs = new List<NodeActionInput>
                        {
                            new (
                                ActionName: "Succeeding Parallel Action",
                                SlaveTaskType: SlaveTaskType.TestOrchestration,
                                NodeSpecificPayloads: new() { { "InternalTestSlave", new() { { "slaveBehavior", SlaveBehaviorMode.Succeed } } } }
                            ),
                            new (
                                ActionName: "Failing Parallel Action",
                                SlaveTaskType: SlaveTaskType.TestOrchestration,
                                NodeSpecificPayloads: new() { { "InternalTestSlave", new() { { "slaveBehavior", SlaveBehaviorMode.FailOnExecute } } } }
                            )
                        };

                        var parallelResults = await stage.CreateAndExecuteNodeActionsInParallelAsync(parallelActionInputs);

                        if (parallelResults.Any(r => !r.IsSuccess))
                        {
                            throw new Exception("One or more parallel actions failed.");
                        }

                        // If successful, set the final result payload on the master action.
                        context.SetFinalResult(parallelResults);
                    }
                }
                else
                {
                    // Begin a single logical stage, telling it to expect ONE sub-action (the node action).
                    await using (var stage = await context.BeginStageAsync("MultiNodeTestStage", subActionCount: 1))
                    {
                        // The payload for the slave task is the full set of original parameters.
                        var taskPayload = new Dictionary<string, object>(context.Parameters);

                        // Create and execute the NodeAction via the StageContext.
                        // We use `nodeSpecificPayloads` to ensure the task payload is only sent to our one target node.
                        var multiNodeResult = await stage.CreateAndExecuteNodeActionAsync(
                            "Orchestration Test Stage",
                            SlaveTaskType.TestOrchestration,
                            nodeSpecificPayloads: new() { { targetNodeName, taskPayload } }
                        );

                        // Simulate master-side failure before the stage completes, if requested.
                        if (masterFailure == MasterFailureMode.ThrowWithinFirstStage)
                        {
                            stage.LogInfo("SIMULATOR: Throwing exception within first stage as requested by MasterFailureMode.");
                            throw new InvalidOperationException("Simulated master failure within first stage.");
                        }
                    
                        // If the node action itself was not successful, determine if it was a failure or a cancellation.
                        if (!multiNodeResult.IsSuccess)
                        {
                            // Check if the underlying reason for the non-success was a cancellation.
                            if (multiNodeResult.FinalState.OverallStatus == NodeActionOverallStatus.Cancelled)
                            {
                                // Throw the correct exception type to be handled by the cancellation catch block.
                                var cancelMessage = multiNodeResult.FinalState.NodeTasks.FirstOrDefault(t => t.Status == NodeTaskStatus.Cancelled)?.StatusMessage ?? "Multi-node test stage was cancelled.";
                                throw new OperationCanceledException(cancelMessage);
                            }
    
                            // Otherwise, it's a genuine failure. Throw a generic exception for the failure catch block.
                            var failureMessage = multiNodeResult.FinalState.NodeTasks.FirstOrDefault()?.StatusMessage ?? "Multi-node test stage failed.";
                            throw new Exception(failureMessage);
                        }

                        // If successful, set the final result payload on the master action.
                        context.SetFinalResult(multiNodeResult.FinalState.NodeTasks.FirstOrDefault(t => t.NodeName == targetNodeName)?.ResultPayload);

                    } // The stage is disposed and finalized here.

                    // Simulate master-side failure before the stage completes, if requested.
                    if (masterFailure == MasterFailureMode.ThrowAfterFirstStage)
                    {
                        context.LogInfo("SIMULATOR: Throwing exception after first stage as requested by MasterFailureMode.");
                        throw new InvalidOperationException("Simulated master failure after first stage.");
                    }
                    

                }

                // If we reach here, the entire workflow was successful.
                context.SetCompleted("Orchestration Test completed successfully.");
                await _journalService.FinalizeStateChangeAsync(new StateChangeFinalizationInfo { ChangeId = changeRecord.ChangeId, Outcome = OperationOutcome.Success, Description = "Test Succeeded", ResultArtifact = context.MasterAction.FinalResultPayload ?? new {} });
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation specifically if needed, otherwise the general catch will get it.
                context.SetCancelled("Orchestration Test was cancelled.");
                await _journalService.FinalizeStateChangeAsync(new StateChangeFinalizationInfo { ChangeId = changeRecord.ChangeId, Outcome = OperationOutcome.Cancelled, Description = "Test Cancelled", ResultArtifact = new { Error = "Operation was cancelled." } });
            }
            catch (Exception ex)
            {
                // Any exception thrown during the 'try' block will be caught here.
                // This marks the master action as failed and finalizes the journal record.
                context.SetFailed(ex.Message);
                await _journalService.FinalizeStateChangeAsync(new StateChangeFinalizationInfo
                {
                    ChangeId = changeRecord.ChangeId,
                    Outcome = OperationOutcome.Failure,
                    Description = $"Test workflow failed with exception: {ex.Message}",
                    ResultArtifact = new { Error = ex.ToString() }
                });
            }
        }
    }
}
