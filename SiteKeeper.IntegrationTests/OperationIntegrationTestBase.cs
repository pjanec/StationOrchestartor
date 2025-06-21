using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master;
using SiteKeeper.Shared.DTOs.API.Authentication;
using SiteKeeper.Shared.DTOs.API.Operations;
using SiteKeeper.Shared.Enums;
using SiteKeeper.Shared.Enums.Extensions;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;
using System.Collections.Generic;
using System.Linq;
using SiteKeeper.Master.Model.InternalData;
using System.Text.Json.Serialization;

namespace SiteKeeper.IntegrationTests
{
    [Collection("SiteKeeperHost")]
    public abstract class OperationIntegrationTestBase : IDisposable
    {
        protected readonly HttpClient _client;
        protected readonly ITestOutputHelper _output;
        protected readonly SiteKeeperHostFixture _fixture;
        protected readonly IJournal _journalService; // The service is now injected.
        protected readonly JsonSerializerOptions _jsonOptions;

        public OperationIntegrationTestBase(SiteKeeperHostFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            _client = new HttpClient { BaseAddress = new Uri("http://localhost:5001") };

            // Resolve the IJournal service from the running host's DI container.
            _journalService = _fixture.AppHost.Services.GetRequiredService<IJournal>();
            
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() }};

            AuthenticateClient().GetAwaiter().GetResult();
        }

        private async Task AuthenticateClient()
        {
            try
            {
                var loginRequest = new UserLoginRequest { Username = "advancedadmin", Password = "password" };
                var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
                
                response.EnsureSuccessStatusCode();

                var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
                Assert.NotNull(authResponse);
                
                    // Set the authentication header for all subsequent requests from this client.
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResponse.AccessToken);
                _output.WriteLine("Client successfully authenticated for test run.");
            }
            catch(Exception ex)
            {
                _output.WriteLine($"FATAL: Failed to authenticate client during test setup: {ex.Message}");
                throw; // Fail the test run if authentication fails.
            }
        }

        /// <summary>
        /// A reusable helper method to poll an operation's status until it reaches a terminal state.
        /// </summary>
        protected async Task<OperationStatusResponse?> PollForOperationCompletion(string operationId, int timeoutSeconds)
        {
            OperationStatusResponse? statusResult = null;
            var operationCompleted = false;
            var pollingTimeout = TimeSpan.FromSeconds(timeoutSeconds);
            var pollingStarted = DateTime.UtcNow;
            _output.WriteLine($"Polling for operation '{operationId}' completion (timeout: {timeoutSeconds}s)...");
            while (DateTime.UtcNow - pollingStarted < pollingTimeout && !operationCompleted)
            {
                await Task.Delay(1000); // Poll every second
                var statusResponse = await _client.GetAsync($"/api/v1/operations/{operationId}");
                if (!statusResponse.IsSuccessStatusCode)
                {
                    _output.WriteLine($"Polling for '{operationId}' failed with status: {statusResponse.StatusCode}");
                    // Continue polling in case it's a transient issue.
                    continue;
                }
                var responseContent = await statusResponse.Content.ReadAsStringAsync();
                statusResult = JsonSerializer.Deserialize<OperationStatusResponse>(responseContent, _jsonOptions);
                Assert.NotNull(statusResult);
                _output.WriteLine($"Polling '{operationId}'... Status: {statusResult.Status}, Progress: {statusResult.ProgressPercent}%");
                // Use the OperationOverallStatus enum for reliable check
                if (Enum.TryParse<NodeActionOverallStatus>(statusResult.Status, true, out var currentStatus) && currentStatus.IsCompleted())
                {
                    operationCompleted = true;
                }
            }
            Assert.True(operationCompleted, $"Operation '{operationId}' did not complete within the {timeoutSeconds}-second timeout.");
            return statusResult;
        }
        
        #region New Journal Service Test Helpers

        /// <summary>
        /// Retrieves the complete, archived MasterAction object by calling the IJournal service.
        /// This replaces reading 'master_action_info.json' from disk.
        /// </summary>
        protected Task<MasterAction?> GetArchivedMasterActionFromJournal(string operationId)
        {
            return _journalService.GetArchivedMasterActionAsync(operationId);
        }

        /// <summary>
        /// Retrieves the detailed result of a specific stage by calling the IJournal service.
        /// This replaces reading 'stage_result.json' from disk.
        /// </summary>
        protected Task<T?> GetStageResultFromJournal<T>(string operationId, int stageIndex) where T : class
        {
            // Note: This requires extending IJournal and Journal.cs to expose this functionality.
            // Assuming this is done as per our discussion.
            // The journal service would find the correct folder and file and deserialize it.
            return _fixture.AppHost.Services.GetRequiredService<IJournal>()
                           .GetArchivedStageResultAsync<T>(operationId, stageIndex);
        }

        /// <summary>
        /// Retrieves the content of a specific log file from a specific stage by calling the IJournal service.
        /// This replaces reading log files directly from the disk.
        /// </summary>
        protected Task<string?> GetStageLogFromJournal(string operationId, int stageIndex, string logFileName)
        {
            // Note: This requires extending IJournal and Journal.cs to expose this functionality.
            return _fixture.AppHost.Services.GetRequiredService<IJournal>()
                           .GetArchivedStageLogContentAsync(operationId, stageIndex, logFileName);
        }

        #endregion

        public void Dispose()
        {
            _client.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}