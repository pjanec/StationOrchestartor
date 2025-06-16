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

namespace SiteKeeper.IntegrationTests
{
    /// <summary>
    /// An abstract base class for all operation-related integration tests.
    /// It handles common setup like HttpClient creation, authentication, and provides
    /// helper methods for polling operations and verifying journal entries.
    /// </summary>
    [Collection("SiteKeeperHost")]
    public abstract class OperationIntegrationTestBase : IDisposable
    {
        protected readonly HttpClient _client;
        protected readonly ITestOutputHelper _output;
        protected readonly SiteKeeperHostFixture _fixture;
        protected readonly IJournalService _journalService;
        protected readonly string _journalRootPath;
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public OperationIntegrationTestBase(SiteKeeperHostFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            _client = new HttpClient();
            
            _client.BaseAddress = new Uri("http://localhost:5001"); 

            // Get services and configuration needed for verification directly from the running host's DI container.
            _journalService = _fixture.AppHost.Services.GetRequiredService<IJournalService>();
            var config = _fixture.AppHost.Services.GetRequiredService<IOptions<MasterConfig>>().Value;
            
            // Construct the full path to the environment's journal root for file-based verification.
            _journalRootPath = Path.Combine(config.JournalRootPath, config.EnvironmentName);

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
                if (Enum.TryParse<OperationOverallStatus>(statusResult.Status, true, out var currentStatus) && currentStatus.IsCompleted())
                {
                    operationCompleted = true;
                }
            }
            Assert.True(operationCompleted, $"Operation '{operationId}' did not complete within the {timeoutSeconds}-second timeout.");
            return statusResult;
        }
        
        /// <summary>
        /// Finds the full path to the Action Journal directory for a given Master Action (Operation) ID.
        /// </summary>
        /// <param name="masterActionId">The ID of the Master Action.</param>
        /// <returns>The full directory path if found; otherwise, null.</returns>
        protected string? FindActionJournalDirectory(string masterActionId)
        {
            var actionJournalRoot = Path.Combine(_journalRootPath, "ActionJournal");
            if (!Directory.Exists(actionJournalRoot))
            {
                _output.WriteLine($"Action Journal root directory not found at: {actionJournalRoot}");
                return null;
            }

            // Folder name format: {timestamp}-{masterActionId}
            var dirs = Directory.GetDirectories(actionJournalRoot, $"*-{masterActionId}");
            var dirPath = dirs.FirstOrDefault();

            if (dirPath == null)
            {
                _output.WriteLine($"Could not find journal directory for MasterActionId: {masterActionId}");
            }
            return dirPath;
        }

        /// <summary>
        /// Reads and deserializes a JSON file from a specified path.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the JSON into.</typeparam>
        /// <param name="filePath">The full path to the JSON file.</param>
        /// <returns>The deserialized object, or null if the file doesn't exist or deserialization fails.</returns>
        protected async Task<T?> ReadJournalJsonFile<T>(string filePath) where T : class
        {
            if (!File.Exists(filePath))
            {
                _output.WriteLine($"Journal file not found: {filePath}");
                return null;
            }
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }

        /// <summary>
        /// A helper to find and read a specific log file from the Action Journal for a given operation and stage.
        /// </summary>
        /// <param name="actionJournalDir">The root directory for the specific Master Action's journal.</param>
        /// <param name="stageName">The name of the stage (e.g., "MultiNodeTestStage").</param>
        /// <param name="logFileName">The name of the log file (e.g., "_master.log" or "InternalTestSlave.log").</param>
        /// <returns>The content of the log file as a string, or null if not found.</returns>
        protected async Task<string?> GetStageLogContent(string actionJournalDir, string stageName, string logFileName)
        {
            var stagesDir = Path.Combine(actionJournalDir, "stages");
            var stageDir = Directory.GetDirectories(stagesDir, $"*-{stageName}").FirstOrDefault();

            if (stageDir == null)
            {
                _output.WriteLine($"Could not find stage directory for stage name '{stageName}'");
                return null;
            }

            var logFilePath = Path.Combine(stageDir, "logs", logFileName);
            if (!File.Exists(logFilePath))
            {
                _output.WriteLine($"Log file not found: {logFilePath}");
                return null;
            }

            return await File.ReadAllTextAsync(logFilePath);
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
