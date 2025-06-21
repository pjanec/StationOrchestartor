// System and Third-Party Libraries
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

// Project-Specific Using Statements
using SiteKeeper.Shared.DTOs.API.Operations;
using SiteKeeper.Shared.Enums;

namespace SiteKeeper.IntegrationTests
{
    /// <summary>
    /// Integration test for the end-to-end flow of the 'env-verify' operation.
    /// This test has been refactored to align with the new workflow engine architecture,
    /// which features a generic operation endpoint and a structured, stage-based status response.
    /// </summary>
    public class EnvVerifyOperationIntegrationTests : OperationIntegrationTestBase
    {
        /// <summary>
        /// This constructor is required to pass the xUnit fixture and output helper
        /// to our base class, which handles setup like HttpClient creation and authentication.
        /// </summary>
        public EnvVerifyOperationIntegrationTests(SiteKeeperHostFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
            // The base class constructor handles all necessary setup.
        }

        /// <summary>
        /// This is the main end-to-end test method for a successful 'env-verify' operation.
        /// It covers the entire lifecycle:
        /// 1. Initiation: Sends a POST request to the generic '/api/v1/operations' endpoint,
        ///    specifying OperationType.EnvVerify in the request body.
        /// 2. Polling: Periodically sends GET requests to track the operation's status until it
        ///    reaches a terminal state (Succeeded, Failed, or Cancelled).
        /// 3. Verification: Asserts that the operation completes successfully and that the final
        ///    API response is correctly structured with stage and task details.
        /// 4. Result Inspection: Drills down into the structured response to verify the specific
        ///    ResultPayload returned by the slave agent for the verification task.
        /// </summary>
        [Fact]
        public async Task EnvVerify_WhenInitiated_CompletesSuccessfullyWithStructuredResults()
        {
            // ARRANGE
            // Define the request for the generic operations endpoint.
            // We specify the operation type directly in the request body.
            var request = new OperationInitiateRequest
            {
                OperationType = OperationType.EnvVerify,
                Description = "Integration test for environment verification."
            };

            // ACT
            // 1. Initiate the 'env-verify' operation.
            // Note that we now post to the generic endpoint, not a specific one.
            var initiateResponse = await _client.PostAsJsonAsync("/api/v1/operations", request);

            // Ensure the request was accepted (HTTP 202).
            initiateResponse.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Accepted, initiateResponse.StatusCode);

            var initiationResult = await initiateResponse.Content.ReadFromJsonAsync<OperationInitiationResponse>();
            Assert.NotNull(initiationResult);
            Assert.False(string.IsNullOrEmpty(initiationResult.OperationId));
            _output.WriteLine($"Operation initiated with ID: {initiationResult.OperationId}");

            // 2. Poll for the operation's completion.
            // The base class provides a helper method for this, which waits for a terminal status.
            var finalStatus = await PollForOperationCompletion(initiationResult.OperationId, 30);

            // ASSERT
            // 3. Verify the final overall operation state.
            Assert.NotNull(finalStatus);
            Assert.Equal(MasterActionStatus.Succeeded.ToString(), finalStatus.Status.ToString());
            Assert.Equal(100, finalStatus.ProgressPercent);
            Assert.NotNull(finalStatus.EndTime);

            // 4. Verify the new, structured stage and task results.
            // The response should contain exactly one stage for this simple workflow.
            Assert.Single(finalStatus.Stages);
            var verificationStage = finalStatus.Stages.First();

            _output.WriteLine($"Verification Stage '{verificationStage.StageName}' completed successfully: {verificationStage.IsSuccess}");
            Assert.True(verificationStage.IsSuccess);
            Assert.Equal("Verification", verificationStage.StageName);

            // The stage should contain exactly one node task for our internal slave.
            Assert.Single(verificationStage.NodeTasks);
            var slaveTask = verificationStage.NodeTasks.First();

            Assert.Equal("InternalTestSlave", slaveTask.NodeName);
            Assert.Equal(NodeTaskStatus.Succeeded.ToString(), slaveTask.TaskStatus, ignoreCase: true);
            Assert.NotNull(slaveTask.Message);
            Assert.NotNull(slaveTask.ResultPayload);

            // 5. Drill down to inspect the specific ResultPayload from the slave.
            // This is the most critical assertion, as it validates the actual work done by the slave.
            var resultPayload = slaveTask.ResultPayload;
            Assert.True(resultPayload.ContainsKey("filesChecked"));
            Assert.True(resultPayload.ContainsKey("deviationsFound"));
            Assert.True(resultPayload.ContainsKey("summary"));

            // When System.Text.Json deserializes into a Dictionary<string, object>, numeric values
            // become JsonElement instances. We must explicitly get their value.
            Assert.Equal(1250, ((JsonElement)resultPayload["filesChecked"]).GetInt32());
            Assert.Equal(0, ((JsonElement)resultPayload["deviationsFound"]).GetInt32());
            Assert.Equal("All configurations and services match the manifest.", ((JsonElement)resultPayload["summary"]).GetString());

            _output.WriteLine("Integration test for EnvVerify completed successfully with new architecture!");
        }
    }
}
