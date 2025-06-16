/*
using FluentAssertions;
using Xunit;
using SiteKeeper.TestOps;
using SiteKeeper.Tests.Infrastructure;
using SiteKeeper.Shared.Enums;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SiteKeeper.Master.Abstractions.Workflow;
using SiteKeeper.Master.Workflow.DTOs;
using SiteKeeper.Slave.Models;
using SiteKeeper.IntegrationTests;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Shared.DTOs.API.Operations;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace SiteKeeper.IntegrationTests
{
    /// <summary>
    ///     Comprehensive end‑to‑end tests that cover the master‑action workflow
    ///     for the new architecture using a single internal slave.
    ///     
    ///     Each test spins up the complete SiteKeeperHost (incl. in‑memory API,
    ///     SignalR hubs, internal slave, journal service) and validates both
    ///     the outward‑facing status and the on‑disk journal artefacts.
    /// </summary>
    public class TestOperationIntegrationTests_ChatGPTo3 : IClassFixture<SiteKeeperHostFixture>
    {
        private readonly SiteKeeperHostFixture _host;

        public TestOperationIntegrationTests_ChatGPTo3(SiteKeeperHostFixture host) => _host = host;

        [Fact(DisplayName = "1. Happy‑path succeeds end‑to‑end")]
        public async Task Should_complete_TestOp_successfully()
        {
            var (outcome, folder) = await _host.StartTestOpAsync(TestOpBehavior.None);
            outcome.Should().Be(OperationOutcome.Success);

            await _host.AssertJournalAsync(folder, info =>
            {
                info.Outcome.Should().Be("Succeeded");
            });
        }

        [Fact(DisplayName = "2. Master action handler throws → operation fails")]
        public async Task Should_fail_when_master_handler_throws()
        {
            var (outcome, folder) = await _host.StartTestOpAsync(TestOpBehavior.MasterThrows);
            outcome.Should().Be(OperationOutcome.Failure;

            File.ReadAllText(Path.Combine(folder, "logs", "_master.log"))
                .Should().Contain(nameof(BoomException));
        }

        [Fact(DisplayName = "3. Slave task throws → operation fails but journal exists")]
        public async Task Should_fail_when_slave_task_throws()
        {
            var (outcome, folder) = await _host.StartTestOpAsync(TestOpBehavior.NodeThrows);
            outcome.Should().Be(OperationOutcome.Failure);

            var resultsDir = Path.Combine(folder, "results");
            resultsDir.Should().Exist();
            Directory.EnumerateFiles(resultsDir).Should().ContainSingle("Partial node result missing");
        }

        [Fact(DisplayName = "4. Slave timeout → operation marked failed (TimedOut)")]
        public async Task Should_timeout_when_slave_is_slow()
        {
            // Intentionally keep delay small so test runs fast.
            var (outcome, _) = await _host.StartTestOpAsync(TestOpBehavior.NodeTimeout, delayMs: 2_000);
            outcome.Should().Be(OperationOutcome.Failure);
        }

        [Fact(DisplayName = "5. Log‑flush mechanism keeps order")]
        public async Task Should_flush_and_order_logs_across_stages()
        {
            var (outcome, folder) = await _host.StartTestOpAsync(TestOpBehavior.SpamLogs, logCount: 200);
            outcome.Should().Be(OperationOutcome.Success);

            var masterLog = File.ReadAllLines(Path.Combine(folder, "logs", "_master.log"));
            // Last spam log must come before the flush marker that MultiNodeStage writes.
            var lastSpamIndex = Array.FindLastIndex(masterLog, l => l.Contains("Spam log"));
            var flushIndex = Array.FindIndex(masterLog, l => l.Contains("FlushLogsAsync"));
            (flushIndex > lastSpamIndex).Should().BeTrue();
        }
    }
}


namespace SiteKeeper.TestOps
{
    /// <summary>
    ///     Strongly‑typed parameters that are passed from the <c>OperationInitiateRequest</c>
    ///     into the <c>TestOpHandler</c> (master) and <c>TestSlaveTaskHandler</c> (slave).
    ///     
    ///     Keeping the DTO inside the <c>TestOps</c> assembly ensures that production
    ///     code stays completely untouched.
    /// </summary>
    public sealed class TestOpParameters
    {
        /// <summary>
        ///     Combined behaviour flags that define how the test operation should
        ///     behave.
        /// </summary>
        [JsonPropertyName("behavior")]
        public TestOpBehavior Behavior { get; set; } = TestOpBehavior.None;

        /// <summary>
        ///     When <see cref="TestOpBehavior.SpamLogs"/> is set this defines how
        ///     many log lines should be emitted.
        /// </summary>
        [JsonPropertyName("logCount")]
        public int LogCount { get; set; } = 0;

        /// <summary>
        ///     Generic delay in milliseconds that is applied when
        ///     <see cref="TestOpBehavior.NodeTimeout"/> or
        ///     <see cref="TestOpBehavior.LongRunning"/> is active.
        /// </summary>
        [JsonPropertyName("delayMs")]
        public int DelayMs { get; set; } = 0;
    }
}

namespace SiteKeeper.TestOps
{
    /// <summary>
    ///     Simple domain‑specific exception that signals an intentional failure
    ///     injected by the test‑operation. Using a dedicated type allows the
    ///     integration tests to assert against <see cref="Exception.GetType"/>
    ///     rather than brittle message strings.
    /// </summary>
    public sealed class BoomException : Exception
    {
        public BoomException(string message) : base(message)
        {
        }

        public BoomException(string message, Exception? inner) : base(message, inner)
        {
        }
    }
}


namespace SiteKeeper.TestOps
{
    /// <summary>
    ///     Master‑side handler that orchestrates a single <see cref="IStageHandler{TInput, TOutput}"/>
    ///     responsible for executing the test operation on the internal slave.
    ///     
    ///     The handler purposefully keeps its production‑side dependencies
    ///     minimal so that it can be wired into the DI container from the test
    ///     assembly without touching the main SiteKeeper codebase.
    /// </summary>
    public sealed class TestOpHandler : IMasterActionHandler
    {
        private readonly IStageHandler<TestOpParameters, NodeStageResult?> _multiNodeStage;
        private readonly ILogger<TestOpHandler> _logger;


        public OperationType Handles => OperationType.OrchestrationTest;

        public TestOpHandler(
            IStageHandler<TestOpParameters, NodeStageResult?> multiNodeStage,
            ILogger<TestOpHandler> logger)
        {
            _multiNodeStage = multiNodeStage;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(MasterActionContext context)
        {
            // Parameters arrive as a raw JSON element; let the framework
            // extension method convert it. If it fails (unlikely) we throw a
            // clearly‑defined exception so the test knows what went wrong.
			var parameters = JsonSerializer.Deserialize<TestOpParameters>( JsonSerializer.Serialize( context.Parameters ) ) ??
							throw new InvalidOperationException( "TestOpParameters missing or malformed." );

            if (parameters.Behavior.HasFlag(TestOpBehavior.MasterThrows))
            {
                throw new BoomException("Master handler instructed to throw (MasterThrows flag set).");
            }

            _logger.LogInformation("Starting TestOp with behavior {Behavior}.", parameters.Behavior);

            // The progress reporter is automatically forwarded to the UI via
            // the coordinator so tests can subscribe to it.
            var progress = new Progress<StageProgress>();

            // Execute the (single) multi‑node stage.
            var stageResult = await _multiNodeStage.ExecuteAsync(
                parameters,
                context,
                progress,
                context.CancellationToken);

            // The tests need a simple deterministic payload so they can assert
            // against master_action_result.json.
            context.SetFinalResult(new
            {
                Greeting = "Hello from master!",
                StageResult = stageResult
            });

            _logger.LogInformation("TestOp completed successfully.");
        }
    }

    /// <summary>
    ///     Minimal result DTO written by the slave handler. Placed here to keep
    ///     all test‑operation artefacts in one assembly.
    /// </summary>
    public sealed record NodeStageResult(string NodeName, string Status);
}

namespace SiteKeeper.TestOps
{
    /// <summary>
    ///     Slave‑side task handler that honours <see cref="TestOpBehavior"/> so
    ///     that integration tests can verify all master ↔ slave edge‑cases.
    /// </summary>
    public sealed class TestSlaveTaskHandler : IExecutiveCodeExecutor<TestOpParameters, NodeStageResult>
    {
        private readonly ILogger<TestSlaveTaskHandler> _logger;
        private readonly string _nodeName;

        public TestSlaveTaskHandler(ILogger<TestSlaveTaskHandler> logger, INodeIdentityService nodeIdentity)
        {
            _logger = logger;
            _nodeName = nodeIdentity.NodeName;
        }

        /// <inheritdoc />
        public async Task<NodeStageResult> ExecuteAsync(
            TestOpParameters parameters,
            SlaveTaskContext context,
            CancellationToken cancellationToken)
        {
            if (parameters.Behavior.HasFlag(TestOpBehavior.NodeTimeout))
            {
                var effectiveDelay = parameters.DelayMs <= 0 ? 5_000 : parameters.DelayMs;
                _logger.LogInformation("Simulating node timeout by sleeping for {DelayMs} ms.", effectiveDelay);

                await Task.Delay(effectiveDelay, cancellationToken);
            }

            if (parameters.Behavior.HasFlag(TestOpBehavior.NodeThrows))
            {
                _logger.LogError("Simulating node exception (NodeThrows flag set).");
                throw new BoomException("Slave task handler instructed to throw (NodeThrows flag set).");
            }

            if (parameters.Behavior.HasFlag(TestOpBehavior.SpamLogs) && parameters.LogCount > 0)
            {
                for (var i = 0; i < parameters.LogCount; i++)
                {
                    _logger.LogInformation("Spam log {Index}/{Total} from node {Node}.", i + 1, parameters.LogCount, _nodeName);
                }
            }

            return new NodeStageResult(_nodeName, "Success");
        }
    }
}


namespace SiteKeeper.Tests.Infrastructure
{
    /// <summary>
    ///     Non‑intrusive extensions for <c>SiteKeeperHostFixture</c> that make it
    ///     trivial to start a test‑operation and perform common journal
    ///     assertions. Implemented as a partial class so it is merged with the
    ///     original definition during compilation – no edit of the production
    ///     source file required.
    /// </summary>
    public static class SiteKeeperHostFixtureExtensions
    {
        /// <summary>
        ///     Initiates a new TestOp via the in‑memory API exposed by the DI
        ///     container, waits for completion and returns both the terminal
        ///     <see cref="OperationOutcome"/> and the on‑disk journal folder for
        ///     further inspection.
        /// </summary>
        public static async Task<(OperationOutcome Outcome, string Folder)> StartTestOpAsync(
            this SiteKeeperHostFixture fixture,
            TestOpBehavior behavior,
            int logCount = 0,
            int delayMs = 0)
        {
            var coordinator = fixture.Services.GetRequiredService<IMasterActionCoordinatorService>();

            var request = new OperationInitiateRequest
            {
                OperationType = TestOpHandler.OperationType,
                Parameters = JsonSerializer.SerializeToElement(new TestOpParameters
                {
                    Behavior = behavior,
                    LogCount = logCount,
                    DelayMs = delayMs
                })
            };

            // System principal is usually exposed by the fixture; fall back to
            // a built‑in one if not present.
            var principal = fixture.SystemPrincipal ?? TestPrincipal.System;

            var action = await coordinator.InitiateMasterActionAsync(request, principal);

            // Wait for the action to finish; the fixture provides a helper that
            // polls the status endpoint.
            var finalStatus = await fixture.WaitForCompletionAsync(action.Id, TimeSpan.FromMinutes(5));

            var journalRoot = fixture.JournalRootPath;
            var folder = Directory.EnumerateDirectories(journalRoot, $"*{action.Id}*", SearchOption.AllDirectories).Single();

            return (finalStatus.Outcome, folder);
        }

        /// <summary>
        ///     Helper that parses <c>info.json</c> and invokes a callback so that
        ///     test cases can perform concise assertions on the journal.
        /// </summary>
        public static async Task AssertJournalAsync(this SiteKeeperHostFixture fixture,
            string folder,
            Action<JournalInfo> assert)
        {
            var infoPath = Path.Combine(folder, "info.json");
            if (!File.Exists(infoPath)) throw new FileNotFoundException(infoPath);

            await using var stream = File.OpenRead(infoPath);
            var info = await JsonSerializer.DeserializeAsync<JournalInfo>(stream);
            if (info is null) throw new InvalidOperationException("Failed to deserialize info.json");

            assert(info);
        }
    }

    #region Helper DTOs

    /// <summary>Minimal projection of the action journal's info.json.</summary>
    public sealed record JournalInfo(string? Outcome, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt);

    /// <summary>Stub principal used when fixture doesn`t expose a system account.</summary>
    internal static class TestPrincipal
    {
        public static readonly System.Security.Claims.ClaimsPrincipal System =
            new(new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim("role", "System")
            }, "TestAuth"));
    }

    #endregion
}

namespace SiteKeeper.TestOps
{
    /// <summary>
    ///     Behaviour flags that instruct the <c>TestOpHandler</c> and the
    ///     <c>TestSlaveTaskHandler</c> to simulate specific success or failure
    ///     scenarios during integration‑tests.
    ///     
    ///     The enum is <see cref="FlagsAttribute"/>‑enabled which makes it easy
    ///     to combine scenarios, e.g. <c>NodeThrows | SpamLogs</c>.
    /// </summary>
    [Flags]
    public enum TestOpBehavior
    {
        /// <summary>
        ///     Default – performs the happy‑path execution with no injected
        ///     faults or delays.
        /// </summary>
        None = 0,

        /// <summary>
        ///     Make the master‑side <c>IMasterActionHandler</c> throw a
        ///     <see cref="BoomException"/> before the first stage is executed.
        ///     Used to test error propagation from the master action itself.
        /// </summary>
        MasterThrows = 1 << 0,

        /// <summary>
        ///     Instruct the slave task handler to throw a
        ///     <see cref="BoomException"/> after its execution begins.
        /// </summary>
        NodeThrows = 1 << 1,

        /// <summary>
        ///     Forces the slave to exceed <c>NodeExecutionTimeout</c> by
        ///     awaiting longer than the master expects. This allows testing of
        ///     timeout and cancellation logic.
        /// </summary>
        NodeTimeout = 1 << 2,

        /// <summary>
        ///     Causes the master and/or slave handler (depending on context) to
        ///     emit a large number of log lines so that ordered log‑flush logic
        ///     can be verified.
        /// </summary>
        SpamLogs = 1 << 3,

        /// <summary>
        ///     Makes the stage deliberately long‑running. Primarily used by the
        ///     cancellation test which needs a window in which to issue the
        ///     <c>RequestCancellationAsync</c> API call.
        /// </summary>
        LongRunning = 1 << 4,
    }
}
*/