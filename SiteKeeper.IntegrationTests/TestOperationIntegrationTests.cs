// System and Third-Party Libraries
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;

// Project-Specific Using Statements
using SiteKeeper.Master.Model.InternalData;
using SiteKeeper.Shared.DTOs.API.Operations;
using SiteKeeper.Shared.Enums;
using SiteKeeper.Slave;
using SiteKeeper.Master.Abstractions.Workflow;

namespace SiteKeeper.IntegrationTests
{
    /// <summary>
    /// Contains a comprehensive suite of integration tests for the 'test-op' operation.
    /// These tests validate the entire Master Action workflow, from API initiation to completion,
    /// including master-side logic, slave-side execution, various failure modes, and the
    /// integrity of the new dual-journaling system.
    /// </summary>
    public class TestOperationIntegrationTests : OperationIntegrationTestBase
    {
        public TestOperationIntegrationTests(SiteKeeperHostFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
            // Give the internal slave agent time to connect to the master after the test host starts.
            // This is a simple delay; a more robust solution might use a signaling mechanism.
            Task.Delay(5000).GetAwaiter().GetResult();
        }

        /// <summary>
        /// A private helper to centralize the initiation and polling of the 'test-op'.
        /// This is used for tests that can run to completion without intermediate steps.
        /// </summary>
        private async Task<OperationStatusResponse?> RunTestOperation(TestOpRequest request, int timeoutSeconds = 30)
        {
            var initiationResult = await InitiateTestOperation(request);
            return await PollForOperationCompletion(initiationResult.OperationId, timeoutSeconds);
        }
        
        /// <summary>
        /// A private helper to only initiate the 'test-op' and return the initiation response.
        /// This is used for tests that need to perform actions *during* the operation's execution.
        /// </summary>
        private async Task<OperationInitiationResponse> InitiateTestOperation(TestOpRequest request)
        {
            var initiateResponse = await _client.PostAsJsonAsync("/api/v1/operations/test-op", request);
            initiateResponse.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Accepted, initiateResponse.StatusCode);

            var initiationResult = await initiateResponse.Content.ReadFromJsonAsync<OperationInitiationResponse>();
            Assert.NotNull(initiationResult);
            _output.WriteLine($"Test operation initiated with ID: {initiationResult.OperationId}");
            return initiationResult;
        }

        #region Happy Path and Journaling Tests

        [Fact]
        public async Task TestOp_Succeeds_And_RecordsActionAndChangeJournals()
        {
            // ARRANGE
            _output.WriteLine("TEST: TestOp_Succeeds_And_RecordsActionAndChangeJournals");
            var request = new TestOpRequest { SlaveBehavior = SlaveBehaviorMode.Succeed, CustomMessage = "VerifyThisLogInSlaveLog" };

            // ACT
            var finalStatus = await RunTestOperation(request);

            // ASSERT - API Response
            Assert.NotNull(finalStatus);
            Assert.Equal(MasterActionStatus.Succeeded.ToString(), finalStatus.Status, ignoreCase: true);
            var stage = Assert.Single(finalStatus.Stages);
            Assert.True(stage.IsSuccess);
            var nodeTask = Assert.Single(stage.NodeTasks);
            Assert.Equal(NodeTaskStatus.Succeeded.ToString(), nodeTask.TaskStatus, ignoreCase: true);

            // ASSERT - Journal Verification (using IJournal service)
            var masterActionInfo = await GetArchivedMasterActionFromJournal(finalStatus.Id);
            Assert.NotNull(masterActionInfo);
            Assert.Equal(MasterActionStatus.Succeeded, masterActionInfo.OverallStatus);

            var slaveLogContent = await GetStageLogFromJournal(finalStatus.Id, 1, "InternalTestSlave.log");
            Assert.NotNull(slaveLogContent);
            Assert.Contains(request.CustomMessage, slaveLogContent);

            var stageResult = await GetStageResultFromJournal<List<NodeActionResult>>(finalStatus.Id, 1);
            Assert.NotNull(stageResult);
            Assert.True(stageResult.Single().IsSuccess);
        }

        [Fact]
        public async Task TestOp_Succeeds_And_Verifies_Master_Side_Logging()
        {
            // ARRANGE
            var request = new TestOpRequest { SlaveBehavior = SlaveBehaviorMode.Succeed, CustomMessage = "VerifyThisLogInMasterLog" };

            // ACT
            var finalStatus = await RunTestOperation(request);

            // ASSERT
            Assert.NotNull(finalStatus);
            Assert.Equal(MasterActionStatus.Succeeded.ToString(), finalStatus.Status, ignoreCase: true);

            // Verify logs for the initialization stage (0-_init)
            var initLogContent = await GetStageLogFromJournal(finalStatus.Id, 0, "_master.log");
            Assert.NotNull(initLogContent);
            Assert.Contains("Starting Orchestration Test workflow...", initLogContent);
            Assert.Contains(request.CustomMessage, initLogContent);

            // Verify logs for the main execution stage (1-MultiNodeTestStage)
            var stageLogContent = await GetStageLogFromJournal(finalStatus.Id, 1, "_master.log");
            Assert.NotNull(stageLogContent);
            Assert.Contains("--- Beginning Stage 1/1: MultiNodeTestStage ---", stageLogContent);

            // Verify logs for the finalization stage (2-_final)
            var finalLogContent = await GetStageLogFromJournal(finalStatus.Id, 2, "_master.log");
            Assert.NotNull(finalLogContent);
            Assert.Contains("Orchestration Test completed successfully.", finalLogContent);
        }

        #endregion

        #region Master-Side Failure Tests

        [Fact]
        public async Task TestOp_Fails_When_MasterActionHandler_Throws_Immediately()
        {
            // ARRANGE
            var request = new TestOpRequest { MasterFailure = MasterFailureMode.ThrowBeforeFirstStage };

            // ACT
            var finalStatus = await RunTestOperation(request, 15);

            // ASSERT
            Assert.NotNull(finalStatus);
            Assert.Equal(MasterActionStatus.Failed.ToString(), finalStatus.Status, ignoreCase: true);
			Assert.Contains(finalStatus.RecentLogs, log => log.Contains( "Simulated master failure before first stage" ));

            // Verify init log contains the intent to throw
            var initLog = await GetStageLogFromJournal(finalStatus.Id, 0, "_master.log");
            Assert.NotNull(initLog);
            Assert.Contains("SIMULATOR: Throwing exception before any stage", initLog);

            // Verify final log contains the actual error message  ; final == init because no stage has run
            var finalLog = await GetStageLogFromJournal(finalStatus.Id, 0, "_master.log");
            Assert.NotNull(finalLog);
            Assert.Contains("Simulated master failure before first stage", finalLog);
        }

        [Fact]
        public async Task TestOp_Fails_When_MasterActionHandler_Throws_Within_Stage()
        {
            // ARRANGE
            var request = new TestOpRequest { MasterFailure = MasterFailureMode.ThrowWithinFirstStage, SlaveBehavior = SlaveBehaviorMode.Succeed };

            // ACT
            var finalStatus = await RunTestOperation(request, 15);

            // ASSERT
            Assert.NotNull(finalStatus);
            Assert.Equal(MasterActionStatus.Failed.ToString(), finalStatus.Status, ignoreCase: true);
            Assert.True(finalStatus.Stages.First().IsSuccess); // First stage should have succeeded

            // Allow a brief moment for the background log flush in the coordinator's finally block to complete.
            await Task.Delay(500);

            // Verify stage log contains the intent to throw
            var stage1Log = await GetStageLogFromJournal(finalStatus.Id, 1, "_master.log");
            Assert.NotNull(stage1Log);
            Assert.Contains("SIMULATOR: Throwing exception within first stage", stage1Log);
            Assert.Contains("Simulated master failure within first stage", stage1Log);
        }

        [Fact]
        public async Task TestOp_Fails_When_MasterActionHandler_Throws_Between_Stages()
        {
            // ARRANGE
            var request = new TestOpRequest { MasterFailure = MasterFailureMode.ThrowAfterFirstStage, SlaveBehavior = SlaveBehaviorMode.Succeed };

            // ACT
            var finalStatus = await RunTestOperation(request, 15);

            // ASSERT
            Assert.NotNull(finalStatus);
            Assert.Equal(MasterActionStatus.Failed.ToString(), finalStatus.Status, ignoreCase: true);
            Assert.True(finalStatus.Stages.First().IsSuccess); // First stage should have succeeded

            // Allow a brief moment for the background log flush in the coordinator's finally block to complete.
            await Task.Delay(500);


			// Verify stage log contains the intent to throw
			// Because the next stage has not started yet, the error should be logged to the previous stage log.
			var stage1Log = await GetStageLogFromJournal(finalStatus.Id, 1, "_master.log");
            Assert.NotNull(stage1Log);
            Assert.Contains("SIMULATOR: Throwing exception after first stage", stage1Log);
            Assert.Contains("Simulated master failure after first stage", stage1Log);
        }

        #endregion

        #region Slave-Side Failure Tests

        [Fact]
        public async Task TestOp_Fails_When_Slave_Fails_On_Prepare()
        {
            // ARRANGE
            var request = new TestOpRequest { SlaveBehavior = SlaveBehaviorMode.FailOnPrepare, CustomMessage = "Disk space low." };

            // ACT
            var finalStatus = await RunTestOperation(request, 15);

            // ASSERT
            Assert.NotNull(finalStatus);
            Assert.Equal(MasterActionStatus.Failed.ToString(), finalStatus.Status, ignoreCase: true);
            var task = Assert.Single(finalStatus.Stages).NodeTasks.Single();
            Assert.Equal(NodeTaskStatus.NotReadyForTask.ToString(), task.TaskStatus, ignoreCase: true);
            Assert.Contains(request.CustomMessage, task.Message);
        }

        [Fact]
        public async Task TestOp_Fails_When_Slave_Fails_On_Execute()
        {
            // ARRANGE
            var request = new TestOpRequest { SlaveBehavior = SlaveBehaviorMode.FailOnExecute, CustomMessage = "Computation failed." };

            // ACT
            var finalStatus = await RunTestOperation(request);

            // ASSERT
            Assert.NotNull(finalStatus);
            Assert.Equal(MasterActionStatus.Failed.ToString(), finalStatus.Status, ignoreCase: true);
            var nodeTask = Assert.Single(finalStatus.Stages).NodeTasks.Single();
            Assert.Equal(NodeTaskStatus.Failed.ToString(), nodeTask.TaskStatus, ignoreCase: true);
            Assert.NotNull(nodeTask.ResultPayload);
            Assert.Equal("FailureRequested", ((JsonElement)nodeTask.ResultPayload["error"]).GetString());
            Assert.Equal(request.CustomMessage, ((JsonElement)nodeTask.ResultPayload["message"]).GetString());
        }

        [Fact]
        public async Task TestOp_Fails_When_Slave_Throws_On_Execute()
        {
            // ARRANGE
            var request = new TestOpRequest { SlaveBehavior = SlaveBehaviorMode.ThrowOnExecute, CustomMessage = "Critical component missing." };

            // ACT
            var finalStatus = await RunTestOperation(request);

            // ASSERT
            Assert.NotNull(finalStatus);
            Assert.Equal(MasterActionStatus.Failed.ToString(), finalStatus.Status, ignoreCase: true);
            var nodeTask = Assert.Single(finalStatus.Stages).NodeTasks.Single();
            Assert.Equal(NodeTaskStatus.Failed.ToString(), nodeTask.TaskStatus, ignoreCase: true);
            Assert.NotNull(nodeTask.ResultPayload);
            Assert.Contains("InvalidOperationException", ((JsonElement)nodeTask.ResultPayload["error"]).GetString());
            Assert.Equal(request.CustomMessage, ((JsonElement)nodeTask.ResultPayload["message"]).GetString());
        }
        
        #endregion

        #region Timeout Tests

        [Fact]
        public async Task TestOp_Fails_When_Slave_Times_Out_On_Prepare()
        {
            // ARRANGE
            var request = new TestOpRequest { SlaveBehavior = SlaveBehaviorMode.TimeoutOnPrepare };

            // ACT
            var finalStatus = await RunTestOperation(request, 45);

            // ASSERT
            Assert.NotNull(finalStatus);
            Assert.Equal(MasterActionStatus.Failed.ToString(), finalStatus.Status, ignoreCase: true);
            var nodeTask = Assert.Single(finalStatus.Stages).NodeTasks.Single();
            Assert.Equal(NodeTaskStatus.ReadinessCheckTimedOut.ToString(), nodeTask.TaskStatus, ignoreCase: true);
        }
        
        [Fact]
        public async Task TestOp_Fails_When_Slave_Times_Out_On_Execute()
        {
            // ARRANGE
            var request = new TestOpRequest { SlaveBehavior = SlaveBehaviorMode.TimeoutOnExecute, ExecutionDelaySeconds = 40 };

            // ACT
            var finalStatus = await RunTestOperation(request, 45);

            // ASSERT
            Assert.NotNull(finalStatus);
            Assert.Equal(MasterActionStatus.Failed.ToString(), finalStatus.Status, ignoreCase: true);
            var nodeTask = Assert.Single(finalStatus.Stages).NodeTasks.Single();
            Assert.Equal(NodeTaskStatus.TimedOut.ToString(), nodeTask.TaskStatus, ignoreCase: true);
        }
        #endregion

        #region Disconnection and Health Monitoring Tests
        
        [Fact]
        public async Task TestOp_Fails_When_Node_Disconnects_During_Execution()
        {
            // ARRANGE
            var slaveAgentService = _fixture.AppHost.Services.GetServices<IHostedService>().OfType<SlaveAgentService>().FirstOrDefault();
            Assert.NotNull(slaveAgentService);
            var request = new TestOpRequest { SlaveBehavior = SlaveBehaviorMode.CancelDuringExecute, ExecutionDelaySeconds = 60 };

            try
            {
                // ACT
                var initiationResult = await InitiateTestOperation(request);
                await Task.Delay(2000);
                await slaveAgentService.StopAsync(CancellationToken.None);
                var finalStatus = await PollForOperationCompletion(initiationResult.OperationId, 25);

                // ASSERT
                Assert.NotNull(finalStatus);
                Assert.Equal(MasterActionStatus.Failed.ToString(), finalStatus.Status, ignoreCase: true);
                var nodeTask = Assert.Single(finalStatus.Stages).NodeTasks.Single();
                Assert.Equal(NodeTaskStatus.NodeOfflineDuringTask.ToString(), nodeTask.TaskStatus, ignoreCase: true);
            }
            finally
            {
                await slaveAgentService.StartAsync(CancellationToken.None);
                await Task.Delay(5000);
            }
        }
        
        #endregion

        #region Cancellation Tests

        [Fact]
        public async Task TestOp_Cancels_Successfully_During_Execution()
        {
            // ARRANGE
            _output.WriteLine("TEST: TestOp_Cancels_Successfully_During_Execution");
            var request = new TestOpRequest { SlaveBehavior = SlaveBehaviorMode.CancelDuringExecute, ExecutionDelaySeconds = 20 };

            // ACT
            var initiationResult = await InitiateTestOperation(request);
            await Task.Delay(2000); // Wait for task to start
            var cancelResponse = await _client.PostAsync($"/api/v1/operations/{initiationResult.OperationId}/cancel", null);
            cancelResponse.EnsureSuccessStatusCode();

            // ASSERT
            var cancelResult = await cancelResponse.Content.ReadFromJsonAsync<OperationCancelResponse>();
            Assert.Equal(OperationCancellationRequestStatus.CancellationPending, cancelResult.Status);

            var finalStatus = await PollForOperationCompletion(initiationResult.OperationId, 30);
            Assert.NotNull(finalStatus);
            Assert.Equal(MasterActionStatus.Cancelled.ToString(), finalStatus.Status, ignoreCase: true);
            var nodeTask = Assert.Single(finalStatus.Stages).NodeTasks.Single();
            Assert.Equal(NodeTaskStatus.Cancelled.ToString(), nodeTask.TaskStatus, ignoreCase: true);
        }
        
        [Fact]
        public async Task TestOp_Resolves_When_Node_Disconnects_During_Cancellation_Acknowledgement()
        {
            // ARRANGE
            var slaveAgentService = _fixture.AppHost.Services.GetServices<IHostedService>().OfType<SlaveAgentService>().FirstOrDefault();
            Assert.NotNull(slaveAgentService);
            var request = new TestOpRequest { SlaveBehavior = SlaveBehaviorMode.CancelDuringExecute, ExecutionDelaySeconds = 60 };

            try
            {
                var initiationResult = await InitiateTestOperation(request);
                await Task.Delay(2000);

                // ACT
                await _client.PostAsync($"/api/v1/operations/{initiationResult.OperationId}/cancel", null);
                await slaveAgentService.StopAsync(CancellationToken.None);

                // ASSERT
                var finalStatus = await PollForOperationCompletion(initiationResult.OperationId, 5);
                Assert.NotNull(finalStatus);
                Assert.Equal(MasterActionStatus.Cancelled.ToString(), finalStatus.Status, ignoreCase: true);
                var nodeTask = Assert.Single(finalStatus.Stages).NodeTasks.Single();
                Assert.Equal(NodeTaskStatus.Cancelled.ToString(), nodeTask.TaskStatus, ignoreCase: true);
            }
            finally
            {
                await slaveAgentService.StartAsync(CancellationToken.None);
                await Task.Delay(5000);
            }
        }

        [Fact]
        public async Task TestOp_Cancel_ReturnsConflict_When_Operation_Already_Failed_Due_To_Offline_Node()
        {
            // ARRANGE
            var slaveAgentService = _fixture.AppHost.Services.GetServices<IHostedService>().OfType<SlaveAgentService>().FirstOrDefault();
            Assert.NotNull(slaveAgentService);
            var request = new TestOpRequest { SlaveBehavior = SlaveBehaviorMode.CancelDuringExecute, ExecutionDelaySeconds = 60 };

            try
            {
                var initiationResult = await InitiateTestOperation(request);
                await Task.Delay(2000);
                await slaveAgentService.StopAsync(CancellationToken.None);
                await Task.Delay(20000);

                // ACT
                var cancelResponse = await _client.PostAsync($"/api/v1/operations/{initiationResult.OperationId}/cancel", null);
        
                // ASSERT
                Assert.Equal(HttpStatusCode.Conflict, cancelResponse.StatusCode);
                var finalStatus = await PollForOperationCompletion(initiationResult.OperationId, 5);
                Assert.NotNull(finalStatus);
                Assert.Equal(MasterActionStatus.Failed.ToString(), finalStatus.Status, ignoreCase: true);
                var task = Assert.Single(finalStatus.Stages).NodeTasks.Single();
                Assert.Equal(NodeTaskStatus.NodeOfflineDuringTask.ToString(), task.TaskStatus, ignoreCase: true);
            }
            finally
            {
                await slaveAgentService.StartAsync(CancellationToken.None);
                await Task.Delay(5000);
            }
        }
        
        #endregion
    }
}
