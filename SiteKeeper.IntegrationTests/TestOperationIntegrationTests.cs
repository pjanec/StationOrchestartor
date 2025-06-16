using SiteKeeper.Master.Model.InternalData;
using SiteKeeper.Shared.DTOs.API.Operations;
using SiteKeeper.Shared.Enums;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using System.IO;
using SiteKeeper.Master.Abstractions.Services.Journaling;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Text.Json;
using SiteKeeper.Master.Abstractions.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SiteKeeper.Slave;

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
        /// </summary>
        /// <param name="request">The request DTO detailing the desired test behavior.</param>
        /// <param name="timeoutSeconds">The timeout for polling the operation's completion.</param>
        /// <returns>The final status response of the operation.</returns>
        private async Task<OperationStatusResponse?> RunTestOperation(TestOpRequest request, int timeoutSeconds = 30)
        {
            var initiateResponse = await _client.PostAsJsonAsync("/api/v1/operations/test-op", request);
            initiateResponse.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Accepted, initiateResponse.StatusCode);

            var initiationResult = await initiateResponse.Content.ReadFromJsonAsync<OperationInitiationResponse>();
            Assert.NotNull(initiationResult);
            _output.WriteLine($"Test operation initiated with ID: {initiationResult.OperationId}");

            return await PollForOperationCompletion(initiationResult.OperationId, timeoutSeconds);
        }

        #region Happy Path and Journaling Tests

        /// <summary>
        /// Tests the full "happy path" of the test operation and verifies the integrity of both
        /// the Action Journal and the Change Journal.
        /// </summary>
        [Fact]
        public async Task TestOp_Succeeds_And_RecordsActionAndChangeJournals()
        {
            // ARRANGE
            _output.WriteLine("TEST: TestOp_Succeeds_And_RecordsActionAndChangeJournals");
            var request = new TestOpRequest
            {
                SlaveBehavior = SlaveBehaviorMode.Succeed,
                CustomMessage = "VerifyThisLogInSlaveLog" // This message will be logged by the slave
            };

            // ACT
            var finalStatus = await RunTestOperation(request);

            // ASSERT - API Response
            Assert.NotNull(finalStatus);
            Assert.Equal(OperationOverallStatus.Succeeded.ToString(), finalStatus.Status, ignoreCase: true);
            Assert.Equal(100, finalStatus.ProgressPercent);
            Assert.Single(finalStatus.NodeTasks);
            var nodeTask = finalStatus.NodeTasks[0];
            Assert.Equal(NodeTaskStatus.Succeeded.ToString(), nodeTask.TaskStatus, ignoreCase: true);
            Assert.NotNull(nodeTask.ResultPayload);

            // ASSERT - Action Journal (Verbose Debugging)
            var actionJournalDir = FindActionJournalDirectory(finalStatus.Id);
            Assert.NotNull(actionJournalDir);

            // 1. Check master_action_info.json
            var masterActionInfo = await ReadJournalJsonFile<MasterAction>(Path.Combine(actionJournalDir, "master_action_info.json"));
            Assert.NotNull(masterActionInfo);
            Assert.Equal(OperationOverallStatus.Succeeded, masterActionInfo.OverallStatus);
            Assert.Equal(finalStatus.Id, masterActionInfo.Id);

            // 2. Check stage logs for the message passed to the slave
            var slaveLogContent = await GetStageLogContent(actionJournalDir, "MultiNodeTestStage", "InternalTestSlave.log");
            Assert.NotNull(slaveLogContent);
            Assert.Contains(request.CustomMessage, slaveLogContent);

            // 3. Check stage results for the slave's result payload
            var stageResultPath = Path.Combine(actionJournalDir, "stages", "1-MultiNodeTestStage", "results", "stage_result.json");
            var stageResult = await ReadJournalJsonFile<MultiNodeOperationResult>(stageResultPath);
            Assert.NotNull(stageResult);
            Assert.True(stageResult.IsSuccess);
            Assert.NotNull(stageResult.FinalOperationState.NodeTasks.First().ResultPayload);


            // ASSERT - Change Journal (High-Level Audit)
            var changeJournalPath = Path.Combine(_journalRootPath, "system_changes_index.log");
            Assert.True(File.Exists(changeJournalPath));
            var changeJournalLines = await File.ReadAllLinesAsync(changeJournalPath);
            
            var initiatedRecordLine = changeJournalLines.LastOrDefault(l => l.Contains(finalStatus.Id) && l.Contains("SystemEventInitiated"));
            var completedRecordLine = changeJournalLines.LastOrDefault(l => l.Contains(finalStatus.Id) && l.Contains("\"Outcome\":\"Success\""));

            Assert.NotNull(initiatedRecordLine);
            Assert.NotNull(completedRecordLine);

            var completedRecord = JsonSerializer.Deserialize<SystemChangeRecord>(completedRecordLine, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(completedRecord);
            Assert.Equal("Success", completedRecord.Outcome);
            Assert.Equal(finalStatus.Id, completedRecord.SourceMasterActionId);
        }

        /// <summary>
        /// Verifies that logs generated directly by the MasterActionHandler (using context.LogInfo)
        /// are correctly recorded in the _master.log file within the appropriate stage directory.
        /// </summary>
        [Fact]
        public async Task TestOp_Succeeds_And_Verifies_Master_Side_Logging()
        {
            // ARRANGE
            _output.WriteLine("TEST: TestOp_Succeeds_And_Verifies_Master_Side_Logging");
            var request = new TestOpRequest
            {
                SlaveBehavior = SlaveBehaviorMode.Succeed,
                CustomMessage = "VerifyThisLogInMasterLog" // This message will be logged by the master handler
            };

            // ACT
            var finalStatus = await RunTestOperation(request);

            // ASSERT
            Assert.NotNull(finalStatus);
            Assert.Equal(OperationOverallStatus.Succeeded.ToString(), finalStatus.Status, ignoreCase: true);

            var actionJournalDir = FindActionJournalDirectory(finalStatus.Id);
            Assert.NotNull(actionJournalDir);

            // The log is generated before the "MultiNodeTestStage" begins.
            // Check the log file for the initial stage (_init).
            var masterLogContent = await GetStageLogContent(actionJournalDir, "_init", "_master.log");
            Assert.NotNull(masterLogContent);
            Assert.Contains(request.CustomMessage, masterLogContent);
        }

        #endregion

        #region Master-Side Failure Tests

        /// <summary>
        /// Verifies that if the IMasterActionHandler throws an exception immediately, the
        /// operation is correctly marked as Failed and the journals reflect this.
        /// </summary>
        [Fact]
        public async Task TestOp_Fails_When_MasterActionHandler_Throws_Immediately()
        {
            // ARRANGE
            _output.WriteLine("TEST: TestOp_Fails_When_MasterActionHandler_Throws_Immediately");
            var request = new TestOpRequest
            {
                MasterFailure = MasterFailureMode.ThrowBeforeFirstStage,
                SlaveBehavior = SlaveBehaviorMode.Succeed // Slave behavior is irrelevant here
            };

            // ACT
            var finalStatus = await RunTestOperation(request, 15);

            // ASSERT
            Assert.NotNull(finalStatus);
            Assert.Equal(OperationOverallStatus.Failed.ToString(), finalStatus.Status, ignoreCase: true);
            Assert.Contains("Simulated master failure before first stage", finalStatus.RecentLogs.LastOrDefault());

            // Verify Action Journal records the failure
            var actionJournalDir = FindActionJournalDirectory(finalStatus.Id);
            Assert.NotNull(actionJournalDir);
            var masterLog = await GetStageLogContent(actionJournalDir, "_init", "_master.log");
            Assert.NotNull(masterLog);
            Assert.Contains("Simulated master failure before first stage", masterLog);
        }

        /// <summary>
        /// Verifies that if the IMasterActionHandler throws an exception after a successful stage,
        /// the operation is correctly marked as Failed and journals reflect this.
        /// </summary>
        [Fact]
        public async Task TestOp_Fails_When_MasterActionHandler_Throws_Between_Stages()
        {
            // ARRANGE
            _output.WriteLine("TEST: TestOp_Fails_When_MasterActionHandler_Throws_Between_Stages");
            var request = new TestOpRequest
            {
                MasterFailure = MasterFailureMode.ThrowAfterFirstStage,
                SlaveBehavior = SlaveBehaviorMode.Succeed
            };

            // ACT
            var finalStatus = await RunTestOperation(request, 15);

            // ASSERT
            Assert.NotNull(finalStatus);
            Assert.Equal(OperationOverallStatus.Failed.ToString(), finalStatus.Status, ignoreCase: true);
            Assert.Contains("Simulated master failure after first stage", finalStatus.RecentLogs.LastOrDefault());

            // Verify Action Journal
            var actionJournalDir = FindActionJournalDirectory(finalStatus.Id);
            Assert.NotNull(actionJournalDir);

            // The first stage should have completed successfully
            var stageResultPath = Path.Combine(actionJournalDir, "stages", "1-MultiNodeTestStage", "results", "stage_result.json");
            Assert.True(File.Exists(stageResultPath));

            // The exception should be logged in the log for the stage that threw it ("MultiNodeTestStage")
            var masterLog = await GetStageLogContent(actionJournalDir, "MultiNodeTestStage", "_master.log");
            Assert.NotNull(masterLog);
            Assert.Contains("Simulated master failure after first stage", masterLog);
        }

        #endregion

        #region Slave-Side Failure Tests

        /// <summary>
        /// Verifies that if the slave reports it is not ready for a task, the operation
        /// is marked as Failed.
        /// </summary>
        [Fact]
        public async Task TestOp_Fails_When_Slave_Fails_On_Prepare()
        {
            // ARRANGE
            _output.WriteLine("TEST: TestOp_Fails_When_Slave_Fails_On_Prepare");
            var request = new TestOpRequest
            {
                SlaveBehavior = SlaveBehaviorMode.FailOnPrepare,
                CustomMessage = "Disk space low."
            };

            // ACT
            var finalStatus = await RunTestOperation(request, 15);

            // ASSERT
            Assert.NotNull(finalStatus);
            Assert.Equal(OperationOverallStatus.Failed.ToString(), finalStatus.Status, ignoreCase: true);
            Assert.Single(finalStatus.NodeTasks);
            var nodeTask = finalStatus.NodeTasks[0];
            Assert.Equal(NodeTaskStatus.NotReadyForTask.ToString(), nodeTask.TaskStatus, ignoreCase: true);
            Assert.Contains(request.CustomMessage, nodeTask.Message);
        }

        /// <summary>
        /// Verifies that if the slave fails during task execution, the operation is
        /// marked as Failed and the error payload is recorded.
        /// </summary>
        [Fact]
        public async Task TestOp_Fails_When_Slave_Fails_On_Execute()
        {
            // ARRANGE
            _output.WriteLine("TEST: TestOp_Fails_When_Slave_Fails_On_Execute");
            var request = new TestOpRequest
            {
                SlaveBehavior = SlaveBehaviorMode.FailOnExecute,
                CustomMessage = "Critical component missing."
            };

            // ACT
            var finalStatus = await RunTestOperation(request);

            // ASSERT
            Assert.NotNull(finalStatus);
            Assert.Equal(OperationOverallStatus.Failed.ToString(), finalStatus.Status, ignoreCase: true);
            var nodeTask = finalStatus.NodeTasks.Single();
            Assert.Equal(NodeTaskStatus.Failed.ToString(), nodeTask.TaskStatus, ignoreCase: true);
            Assert.Contains("Task reported failure by executive code", nodeTask.Message);
            
            Assert.NotNull(nodeTask.ResultPayload);
            Assert.True(nodeTask.ResultPayload.TryGetValue("error", out var errorElement));
            Assert.Equal("InvalidOperationException", errorElement.ToString());
            Assert.True(nodeTask.ResultPayload.TryGetValue("message", out var messageElement));
            Assert.Equal(request.CustomMessage, messageElement.ToString());
        }

        /// <summary>
        /// Verifies that the master correctly times out if the slave never responds to a
        /// readiness check instruction.
        /// </summary>
        [Fact]
        public async Task TestOp_Fails_When_Slave_Times_Out_On_Prepare()
        {
            // ARRANGE
            _output.WriteLine("TEST: TestOp_Fails_When_Slave_Times_Out_On_Prepare");
            var request = new TestOpRequest
            {
                SlaveBehavior = SlaveBehaviorMode.TimeoutOnPrepare
            };

            // ACT
            // The MultiNodeOperationStageHandler has an internal 30-second timeout for readiness.
            var finalStatus = await RunTestOperation(request, 45);

            // ASSERT
            Assert.NotNull(finalStatus);
            Assert.Equal(OperationOverallStatus.Failed.ToString(), finalStatus.Status, ignoreCase: true);
            var nodeTask = finalStatus.NodeTasks.Single();
            Assert.Equal(NodeTaskStatus.ReadinessCheckTimedOut.ToString(), nodeTask.TaskStatus, ignoreCase: true);
        }
        
        /// <summary>
        /// Verifies that the master correctly times out if the slave starts a task but never
        /// reports completion within the configured timeout period.
        /// </summary>
        [Fact]
        public async Task TestOp_Fails_When_Slave_Times_Out_On_Execute()
        {
            // ARRANGE
            _output.WriteLine("TEST: TestOp_Fails_When_Slave_Times_Out_On_Execute");
            var request = new TestOpRequest
            {
                SlaveBehavior = SlaveBehaviorMode.TimeoutOnExecute,
                ExecutionDelaySeconds = 40 // Set a long delay to exceed the master's timeout
            };

            // ACT
            // The SlaveTaskInstruction has a default timeout of 30s in the handler.
            var finalStatus = await RunTestOperation(request, 45);

            // ASSERT
            Assert.NotNull(finalStatus);
            Assert.Equal(OperationOverallStatus.Failed.ToString(), finalStatus.Status, ignoreCase: true);
            var nodeTask = finalStatus.NodeTasks.Single();
            Assert.Equal(NodeTaskStatus.TimedOut.ToString(), nodeTask.TaskStatus, ignoreCase: true);
        }

        #endregion

        #region Disconnection and Health Monitoring Tests
        
        /// <summary>
        /// Verifies that if a slave node disconnects during a running task, the master's
        /// health monitor detects this and correctly fails the operation.
        /// </summary>
        [Fact]
        public async Task TestOp_Fails_When_Slave_Disconnects_During_Execution()
        {
            // ARRANGE
            _output.WriteLine("TEST: TestOp_Fails_When_Slave_Disconnects_During_Execution");
            var slaveAgentService = _fixture.AppHost.Services.GetServices<IHostedService>().OfType<SlaveAgentService>().Single();
            var request = new TestOpRequest
            {
                SlaveBehavior = SlaveBehaviorMode.CancelDuringExecute, // This mode just waits, perfect for a long-running task
                ExecutionDelaySeconds = 60
            };

            try
            {
                // ACT
                // 1. Initiate the operation but don't wait for it
                var initiateResponse = await _client.PostAsJsonAsync("/api/v1/operations/test-op", request);
                initiateResponse.EnsureSuccessStatusCode();
                var initiationResult = await initiateResponse.Content.ReadFromJsonAsync<OperationInitiationResponse>();
                _output.WriteLine($"Long-running operation initiated: {initiationResult!.OperationId}");

                // 2. Wait for the task to start on the slave
                await Task.Delay(2000);

                // 3. Simulate the slave disconnecting by stopping its service
                _output.WriteLine("Simulating slave disconnect by stopping SlaveAgentService...");
                await slaveAgentService.StopAsync(CancellationToken.None);
                _output.WriteLine("SlaveAgentService stopped.");

                // 4. Poll for the operation's completion. The health monitor runs every 15s,
                //    so it should detect the offline node and fail the operation.
                var finalStatus = await PollForOperationCompletion(initiationResult.OperationId, 35);

                // ASSERT
                Assert.NotNull(finalStatus);
                Assert.Equal(OperationOverallStatus.Failed.ToString(), finalStatus.Status, ignoreCase: true);
                var nodeTask = finalStatus.NodeTasks.Single();
                Assert.Equal(NodeTaskStatus.NodeOfflineDuringTask.ToString(), nodeTask.TaskStatus, ignoreCase: true);
                Assert.Contains("Node went offline", nodeTask.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                // ENSURE CLEAN STATE: Restart the slave agent service so other tests are not affected.
                _output.WriteLine("Restarting SlaveAgentService for test cleanup...");
                await slaveAgentService.StartAsync(CancellationToken.None);
                await Task.Delay(5000); // Give it time to reconnect
                _output.WriteLine("SlaveAgentService restarted.");
            }
        }
        
        /// <summary>
        /// Verifies that a cancellation request for a task on a disconnected node completes
        /// immediately without waiting for a timeout.
        /// </summary>
        [Fact]
        public async Task TestOp_Cancels_Immediately_When_Node_Is_Already_Offline()
        {
            // ARRANGE
            _output.WriteLine("TEST: TestOp_Cancels_Immediately_When_Node_Is_Already_Offline");
            var slaveAgentService = _fixture.AppHost.Services.GetServices<IHostedService>().OfType<SlaveAgentService>().Single();
            var request = new TestOpRequest
            {
                SlaveBehavior = SlaveBehaviorMode.CancelDuringExecute,
                ExecutionDelaySeconds = 60
            };

            try
            {
                // 1. Initiate the long-running operation
                var initiateResponse = await _client.PostAsJsonAsync("/api/v1/operations/test-op", request);
                var initiationResult = await initiateResponse.Content.ReadFromJsonAsync<OperationInitiationResponse>();
                _output.WriteLine($"Long-running operation initiated: {initiationResult!.OperationId}");

                // 2. Simulate disconnect
                await Task.Delay(2000);
                _output.WriteLine("Simulating slave disconnect...");
                await slaveAgentService.StopAsync(CancellationToken.None);
                
                // 3. Wait for the NodeHealthMonitor to detect the offline state. It runs every 15s. We wait for 20s to be safe.
                _output.WriteLine("Waiting for health monitor to detect disconnection...");
                await Task.Delay(20000);

                // ACT
                // 4. Request cancellation for the operation with the offline node.
                _output.WriteLine($"Requesting cancellation for operation on offline node: {initiationResult.OperationId}");
                var cancelResponse = await _client.PostAsync($"/api/v1/operations/{initiationResult.OperationId}/cancel", null);
                cancelResponse.EnsureSuccessStatusCode();

                // 5. Poll for completion. This should be very fast.
                var finalStatus = await PollForOperationCompletion(initiationResult.OperationId, 10); // Use a short timeout

                // ASSERT
                Assert.NotNull(finalStatus);
                Assert.Equal(OperationOverallStatus.Cancelled.ToString(), finalStatus.Status, ignoreCase: true);
                var nodeTask = finalStatus.NodeTasks.Single();
                // The status should be 'Cancelled' because the cancellation monitor sees the node is offline and doesn't wait.
                Assert.Equal(NodeTaskStatus.Cancelled.ToString(), nodeTask.TaskStatus, ignoreCase: true);
            }
            finally
            {
                // ENSURE CLEAN STATE
                _output.WriteLine("Restarting SlaveAgentService for test cleanup...");
                await slaveAgentService.StartAsync(CancellationToken.None);
                await Task.Delay(5000);
                _output.WriteLine("SlaveAgentService restarted.");
            }
        }

        #endregion

        #region Cancellation Tests

        /// <summary>
        /// Tests the full cancellation flow, from API request to the final 'Cancelled' status.
        /// </summary>
        [Fact]
        public async Task TestOp_Cancels_Successfully_During_Execution()
        {
            // ARRANGE
            _output.WriteLine("TEST: TestOp_Cancels_Successfully_During_Execution");
            var request = new TestOpRequest
            {
                SlaveBehavior = SlaveBehaviorMode.CancelDuringExecute,
                ExecutionDelaySeconds = 20 // Ensure it runs long enough to be cancelled
            };

            // ACT
            var initiateResponse = await _client.PostAsJsonAsync("/api/v1/operations/test-op", request);
            initiateResponse.EnsureSuccessStatusCode();
            var initiationResult = await initiateResponse.Content.ReadFromJsonAsync<OperationInitiationResponse>();
            _output.WriteLine($"Cancellable operation initiated with ID: {initiationResult.OperationId}");

            await Task.Delay(2000);

            _output.WriteLine($"Requesting cancellation for operation ID: {initiationResult.OperationId}");
            var cancelResponse = await _client.PostAsync($"/api/v1/operations/{initiationResult.OperationId}/cancel", null);
            cancelResponse.EnsureSuccessStatusCode();

            // ASSERT
            var cancelResult = await cancelResponse.Content.ReadFromJsonAsync<OperationCancelResponse>();
            Assert.Equal(OperationCancellationRequestStatus.CancellationPending, cancelResult.Status);

            var finalStatus = await PollForOperationCompletion(initiationResult.OperationId, 30);
            Assert.NotNull(finalStatus);
            Assert.Equal(OperationOverallStatus.Cancelled.ToString(), finalStatus.Status, ignoreCase: true);
            var nodeTask = finalStatus.NodeTasks.Single();
            Assert.Equal(NodeTaskStatus.Cancelled.ToString(), nodeTask.TaskStatus, ignoreCase: true);
        }

        #endregion
    }
}
