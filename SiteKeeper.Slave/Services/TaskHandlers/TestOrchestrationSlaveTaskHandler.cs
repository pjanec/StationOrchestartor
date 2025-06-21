// SiteKeeper.Slave.Services/TaskHandlers/TestOrchestrationTaskHandler.cs
using NLog;
using SiteKeeper.Shared.DTOs.API.Operations;
using SiteKeeper.Shared.DTOs.MasterSlave;
using SiteKeeper.Shared.Enums;
using SiteKeeper.Slave.Abstractions;
using SiteKeeper.Slave.Models;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SiteKeeper.Slave.Services.TaskHandlers
{
    /// <summary>
    /// Handles the 'TestOrchestration' task type, simulating various slave-side behaviors
    /// based on parameters from the master.
    /// </summary>
    public class TestOrchestrationTaskHandler : ISlaveTaskHandler
    {
        public SlaveTaskType Handles => SlaveTaskType.TestOrchestration;

        public async Task<bool> ExecuteTaskAsync(
            SlaveTaskInstruction instruction,
            SlaveTaskContext slaveTaskContext,
            Action<int> reportProgressPercentAction,
            ILogger taskSpecificLogger)
        {
            var simParams = JsonSerializer.Deserialize<TestOpRequest>(instruction.ParametersJson ?? "{}")
                            ?? new TestOpRequest();

            taskSpecificLogger.Info($"SIMULATOR: Running orchestration test with slave behavior mode: {simParams.SlaveBehavior}");

            if (!string.IsNullOrEmpty(simParams.CustomMessage))
            {
                taskSpecificLogger.Info($"SIMULATOR-LOG: {simParams.CustomMessage}");
            }

            switch (simParams.SlaveBehavior)
            {
                case SlaveBehaviorMode.Succeed:
                    for (int i = 0; i <= 100; i += 25)
                    {
                        slaveTaskContext.CancellationTokenSource.Token.ThrowIfCancellationRequested();
                        await Task.Delay(TimeSpan.FromMilliseconds(simParams.ExecutionDelaySeconds * 250), slaveTaskContext.CancellationTokenSource.Token);
                        reportProgressPercentAction(i);
                    }
                    slaveTaskContext.FinalResultJson = JsonSerializer.Serialize(new { result = "Success", message = simParams.CustomMessage ?? "Task succeeded." });
                    return true;

                case SlaveBehaviorMode.FailOnExecute:
                    await Task.Delay(500, slaveTaskContext.CancellationTokenSource.Token);
                    reportProgressPercentAction(25);
                    taskSpecificLogger.Error("Simulating execution failure as requested.");
                    slaveTaskContext.FinalResultJson = JsonSerializer.Serialize(new { error = "FailureRequested", message = simParams.CustomMessage ?? "Task failed." });
                    return false;

                case SlaveBehaviorMode.ThrowOnExecute:
                    await Task.Delay(500, slaveTaskContext.CancellationTokenSource.Token);
                    reportProgressPercentAction(25);
                    taskSpecificLogger.Error("Simulating execution exception as requested.");
                    throw new InvalidOperationException(simParams.CustomMessage ?? "Simulated execution failure.");

                case SlaveBehaviorMode.TimeoutOnExecute:
                    taskSpecificLogger.Warn($"Simulating execution timeout. Will delay for {simParams.ExecutionDelaySeconds} seconds, which should exceed master's timeout.");
                    reportProgressPercentAction(10);
                    await Task.Delay(TimeSpan.FromSeconds(simParams.ExecutionDelaySeconds), slaveTaskContext.CancellationTokenSource.Token);
                    taskSpecificLogger.Info("Timeout delay completed without cancellation. Task will now succeed, but test should have already timed out on master.");
                    slaveTaskContext.FinalResultJson = JsonSerializer.Serialize(new { result = "Timeout simulation completed without being cancelled." });
                    return true;

                case SlaveBehaviorMode.CancelDuringExecute:
                    taskSpecificLogger.Info("Simulating a long-running task to allow for cancellation.");
                    reportProgressPercentAction(10);
                    await Task.Delay(Timeout.Infinite, slaveTaskContext.CancellationTokenSource.Token);
                    return false; // This line is effectively unreachable due to the exception from cancellation.

                default:
                    throw new ArgumentOutOfRangeException(nameof(simParams.SlaveBehavior), $"Unsupported slave behavior mode: {simParams.SlaveBehavior}");
            }
        }
    }
}