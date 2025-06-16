using Microsoft.AspNetCore.Hosting;
using SiteKeeper.Master.Services.Placeholders;
using SiteKeeper.Shared.DTOs.API.Operations;
using SiteKeeper.Shared.Enums;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using SiteKeeper.Shared.DTOs.Common;
using Xunit;
using Xunit.Abstractions;
using System.Text.Json.Nodes;

namespace SiteKeeper.IntegrationTests
{

	/// <summary>
	/// Integration test for the end-to-end flow of the 'env-verify' operation.
	/// This test uses WebApplicationFactory to host the entire SiteKeeper application
	/// (Master + internal Slave) in-memory.
	/// </summary>
	public class EnvVerifyOperationIntegrationTests : OperationIntegrationTestBase
    {

        /// <summary>
        /// This constructor is required to pass the fixture and output helper
        /// from xUnit's dependency injection to our base class.
        /// </summary>
        public EnvVerifyOperationIntegrationTests(SiteKeeperHostFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
            // Constructor body can be empty.
        }

        /// <summary>
        /// This is the main end-to-end test method for the env-verify operation.
        /// It covers the entire lifecycle:
        /// 1. Initiation: Sends a POST request to start the operation.
        /// 2. Polling: Periodically sends GET requests to track the operation's status.
        /// 3. Verification: Asserts that the operation progresses as expected and completes successfully.
        /// 4. Result Inspection: Checks the detailed results returned by the slave.
        /// </summary>
        [Fact]
        public async Task EnvVerify_WhenInitiated_CompletesSuccessfully()
        {
            // ARRANGE
            // Wait for the internal slave to connect to the master hub.
            // In a real test suite, a more robust mechanism might be used.
            await Task.Delay(5000);

            // ACT
            // 1. Initiate the 'env-verify' operation
            var initiateResponse = await _client.PostAsJsonAsync("/api/v1/operations/env-verify", new EmptyRequest());
            initiateResponse.EnsureSuccessStatusCode(); // Throws if not 2xx
            Assert.Equal(HttpStatusCode.Accepted, initiateResponse.StatusCode);

            var initiationResult = await initiateResponse.Content.ReadFromJsonAsync<OperationInitiationResponse>();
            Assert.NotNull(initiationResult);
            Assert.False(string.IsNullOrEmpty(initiationResult.OperationId));
            _output.WriteLine($"Operation initiated with ID: {initiationResult.OperationId}");

            // 2. Poll for operation status until it completes
            OperationStatusResponse? statusResult = null;
            var operationCompleted = false;
            var polliingTimeout = TimeSpan.FromSeconds(30);
            var pollingStarted = DateTime.UtcNow;

            while (DateTime.UtcNow - pollingStarted < polliingTimeout && !operationCompleted)
            {
                await Task.Delay(1000); // Poll every second

                var statusResponse = await _client.GetAsync($"/api/v1/operations/{initiationResult.OperationId}");
                if (!statusResponse.IsSuccessStatusCode)
                {
                    _output.WriteLine($"Polling failed with status: {statusResponse.StatusCode}");
                    continue;
                }

                var responseContent = await statusResponse.Content.ReadAsStringAsync();
                _output.WriteLine($"Polling response: {responseContent}");
                statusResult = JsonSerializer.Deserialize<OperationStatusResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                Assert.NotNull(statusResult);
                _output.WriteLine($"Polling... Current Operation Status: {statusResult.Status}, Progress: {statusResult.ProgressPercent}%");

                // Check if the operation has reached a terminal state
                if (statusResult.Status == OperationOverallStatus.Succeeded.ToString() ||
                    statusResult.Status == OperationOverallStatus.Failed.ToString() ||
                    statusResult.Status == OperationOverallStatus.Cancelled.ToString())
                {
                    operationCompleted = true;
                }
            }

            // ASSERT
            // 3. Verify the final operation state
            Assert.True(operationCompleted, "Operation did not complete within the 30-second timeout.");
            Assert.NotNull(statusResult);

            // Check that the overall operation succeeded
            Assert.Equal(OperationOverallStatus.Succeeded.ToString(), statusResult.Status);
            Assert.Equal(100, statusResult.ProgressPercent);
            Assert.NotNull(statusResult.EndTime);

            // 4. Verify the state of the individual node task for the internal slave
            {
                Assert.Single(statusResult.NodeTasks);
                var slaveTask = statusResult.NodeTasks[0];
                Assert.Equal("InternalTestSlave", slaveTask.NodeName);
                Assert.Equal(NodeTaskStatus.Succeeded.ToString(), slaveTask.TaskStatus, ignoreCase: true);
                Assert.NotNull(slaveTask.Message);
            }

            // 5. Verify the detailed results returned from the slave's simulated execution
            {
                Assert.NotNull(statusResult.NodeTasks);
                Assert.Single(statusResult.NodeTasks);

                var slaveTask = statusResult.NodeTasks[0]; // Use the object directly
                Assert.NotNull(slaveTask.ResultPayload);
            
                // The payload is a Dictionary<string, object>, where values are likely JsonElements.
                // We need to parse this dictionary to access its properties.
                var payloadJson = JsonSerializer.Serialize(slaveTask.ResultPayload);
                var taskResultPayload = JsonDocument.Parse(payloadJson).RootElement;

                Assert.Equal(1250, taskResultPayload.GetProperty("filesChecked").GetInt32());
                Assert.Equal(0, taskResultPayload.GetProperty("deviationsFound").GetInt32());
                Assert.Equal("All configurations and services match the manifest.", taskResultPayload.GetProperty("summary").GetString());
            }

            _output.WriteLine("Integration test completed successfully!");        }

    }
} 