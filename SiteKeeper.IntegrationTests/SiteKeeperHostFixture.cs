using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SiteKeeper.ConsoleHost;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Workflow;

namespace SiteKeeper.IntegrationTests
{
    /// <summary>
    /// An xUnit Collection Fixture responsible for starting the SiteKeeper Host once
    /// for all tests in the collection and shutting it down when they are complete.
    /// This ensures all tests run against a single, shared application instance.
    /// </summary>
    public class SiteKeeperHostFixture : IAsyncLifetime
    {
        public IHost AppHost { get; private set; }
        private readonly string _testJournalRootPath;

        public SiteKeeperHostFixture()
        {
            // Create a unique, temporary directory for this specific test run's journals.
            // This ensures test isolation and prevents clutter.
            _testJournalRootPath = Path.Combine(Path.GetTempPath(), $"SiteKeeperTests_{Guid.NewGuid()}");
        }

        public async Task InitializeAsync()
        {
            var testConfig = new Dictionary<string, string?>
            {
                { "SiteKeeperMode", "All" },
                { "MasterConfig:GuiPort", "5001" },
                { "MasterConfig:AgentPort", "5002" },
                { "SlaveConfig:AgentName", "InternalTestSlave" },
                { "SlaveConfig:MasterHost", "localhost" },
                { "SlaveConfig:MasterAgentPort", "5002" },
                // Use the isolated journal path for this test run.
                { "MasterConfig:JournalRootPath", _testJournalRootPath },
                // Use a consistent environment name for predictable subfolder creation.
                { "MasterConfig:EnvironmentName", "TestEnv" }
            };

            var contentRoot = Directory.GetCurrentDirectory();
            var builder = Program.CreateAppBuilder(Array.Empty<string>(), contentRoot);
            builder.Environment.EnvironmentName = "Development";
            builder.Configuration.AddInMemoryCollection(testConfig);
            
            // In addition to the handlers registered by Program.cs's Scan operation,
            // we explicitly register our new test handler here. This ensures it's available for DI.
            // Note: The main application's Scan will already pick up production handlers.
            // This is how we add our test-specific one to the pool.
            builder.Services.AddScoped<IMasterActionHandler, SiteKeeper.Master.Workflow.ActionHandlers.OrchestrationTestActionHandler>();

            NLogTestConfiguration.ConfigureNLogToDebug();
            
            var app = builder.Build();
            app.ConfigureSiteKeeperPipeline();
            AppHost = app;

            await AppHost.StartAsync();
        }

        public async Task DisposeAsync()
        {
            await AppHost.StopAsync();
            AppHost.Dispose();

            // Clean up the temporary journal directory after the test run is complete.
            try
            {
                if (Directory.Exists(_testJournalRootPath))
                {
                    Directory.Delete(_testJournalRootPath, recursive: true);
                }
            }
            catch (Exception ex)
            {
                // Log cleanup errors, but don't fail the test run because of them.
                Console.WriteLine($"Error cleaning up test journal directory '{_testJournalRootPath}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// This class definition is required by xUnit to recognize the collection fixture.
    /// It associates the "SiteKeeperHost" collection with our fixture.
    /// </summary>
    [CollectionDefinition("SiteKeeperHost")]
    public class SiteKeeperHostCollection : ICollectionFixture<SiteKeeperHostFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
