using SiteKeeper.Shared.DTOs.API.Operations;
using SiteKeeper.Shared.DTOs.MasterSlave;
using SiteKeeper.Shared.Enums;
using SiteKeeper.Slave.Abstractions;
using SiteKeeper.Slave.Models;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using NLog;

namespace SiteKeeper.Slave.Services
{
    /// <summary>
    /// Provides a simulated implementation of IExecutiveCodeExecutor for development and testing.
    /// This class contains the logic to generate fake data and simulate work for different task types.
    /// It now reads parameters from the enhanced TestOpRequest DTO to simulate various behaviors.
    /// </summary>
    public class SimulatedExecutiveCodeExecutor : IExecutiveCodeExecutor
    {
        /// <inheritdoc />
        public async Task<bool> ExecuteTaskAsync(
            SlaveTaskInstruction instruction,
            SlaveTaskContext slaveTaskContext,
            Action<int> reportProgressPercentAction,
            ILogger taskSpecificLogger)
        {
            taskSpecificLogger.Info($"SIMULATOR: Starting TaskType '{instruction.TaskType}', TaskId '{instruction.TaskId}'. Params: {instruction.ParametersJson}");
            string? resultJsonOutput = null;
            bool successState = false;

            try
            {
                // Simulate work based on instruction.TaskType
                switch (instruction.TaskType)
                {
                    case SlaveTaskType.TestOrchestration:
                        var simParams = JsonSerializer.Deserialize<TestOpRequest>(instruction.ParametersJson ?? "{}") 
                                        ?? new TestOpRequest(); // Ensure simParams is not null

                        taskSpecificLogger.Info($"SIMULATOR: Running orchestration test with slave behavior mode: {simParams.SlaveBehavior}");

                        // If a custom message is provided, log it. This is used for log verification tests.
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
                                resultJsonOutput = JsonSerializer.Serialize(new { result = "Success", message = simParams.CustomMessage ?? "Task succeeded." });
                                successState = true;
                                break;

                            case SlaveBehaviorMode.FailOnExecute:
                                await Task.Delay(500, slaveTaskContext.CancellationTokenSource.Token);
                                reportProgressPercentAction(25);
                                taskSpecificLogger.Error("Simulating execution failure as requested.");
                                throw new InvalidOperationException(simParams.CustomMessage ?? "Simulated execution failure.");

                            case SlaveBehaviorMode.TimeoutOnExecute:
                                taskSpecificLogger.Warn($"Simulating execution timeout. Will delay for {simParams.ExecutionDelaySeconds} seconds, which should exceed master's timeout.");
                                reportProgressPercentAction(10);
                                await Task.Delay(TimeSpan.FromSeconds(simParams.ExecutionDelaySeconds), slaveTaskContext.CancellationTokenSource.Token);
                                taskSpecificLogger.Info("Timeout delay completed without cancellation. Task will now succeed, but the test should have already failed on timeout from the master's perspective.");
                                resultJsonOutput = JsonSerializer.Serialize(new { result = "Timeout simulation completed without being cancelled." });
                                successState = true;
                                break;

                            case SlaveBehaviorMode.CancelDuringExecute:
                                taskSpecificLogger.Info("Simulating a long-running task to allow for cancellation.");
                                reportProgressPercentAction(10);
                                // Wait indefinitely until the CancellationToken is triggered. This will throw an OperationCanceledException, which is caught by the OperationHandler.
                                await Task.Delay(Timeout.Infinite, slaveTaskContext.CancellationTokenSource.Token);
                                break; // This line will only be reached if cancellation is thrown and caught outside

                            // Note: FailOnPrepare and TimeoutOnPrepare are handled in OperationHandler and will not reach here.
                            default:
                                throw new ArgumentOutOfRangeException(nameof(simParams.SlaveBehavior), $"Unsupported slave behavior mode: {simParams.SlaveBehavior}");
                        }
                        break;

                    case SlaveTaskType.VerifyConfiguration:
                        taskSpecificLogger.Info("Simulating environment configuration verification...");
                        for (int i = 0; i <= 100; i += 20)
                        {
                            slaveTaskContext.CancellationTokenSource.Token.ThrowIfCancellationRequested();
                            taskSpecificLogger.Info($"Verification simulation progress: {i}%");
                            reportProgressPercentAction(i);
                            await Task.Delay(TimeSpan.FromMilliseconds(250), slaveTaskContext.CancellationTokenSource.Token);
                        }
                        var verificationResult = new
                        {
                            filesChecked = 1250,
                            deviationsFound = 0,
                            summary = "All configurations and services match the manifest."
                        };
                        resultJsonOutput = JsonSerializer.Serialize(verificationResult);
                        successState = true;
                        break;

                    default:
                        taskSpecificLogger.Warn($"No specific simulation for TaskType '{instruction.TaskType}'. Running default success simulation.");
                        for (int i = 0; i <= 100; i += 25)
                        {
                            slaveTaskContext.CancellationTokenSource.Token.ThrowIfCancellationRequested();
                            await Task.Delay(TimeSpan.FromMilliseconds(50), slaveTaskContext.CancellationTokenSource.Token);
                            reportProgressPercentAction(i);
                        }
                        resultJsonOutput = JsonSerializer.Serialize(new { message = $"Default simulation for {instruction.TaskType} completed successfully." });
                        successState = true;
                        break;
                }
                taskSpecificLogger.Info($"SIMULATOR: Task '{instruction.TaskId}' finished simulation with success: {successState}.");
            }
            catch (OperationCanceledException)
            {
                taskSpecificLogger.Warn("Simulation was cancelled by request.");
                resultJsonOutput = JsonSerializer.Serialize(new { statusMessage = "Task cancelled during simulation." });
                successState = false; 
                throw; // Re-throw for OperationHandler to handle and report "Cancelled" status
            }
            catch (Exception ex)
            {
                taskSpecificLogger.Error(ex, "Error during simulation of executive code.");
                resultJsonOutput = JsonSerializer.Serialize(new { error = ex.GetType().Name, message = ex.Message, stackTrace = ex.ToString() });
                successState = false;
            }
            finally
            {
                // Store the final result JSON in the context for the OperationHandler to use.
                slaveTaskContext.FinalResultJson = resultJsonOutput;
            }
            return successState;
        }
    }
}
