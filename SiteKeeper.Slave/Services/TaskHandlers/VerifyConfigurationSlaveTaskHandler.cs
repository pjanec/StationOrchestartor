// SiteKeeper.Slave.Services/TaskHandlers/VerifyConfigurationTaskHandler.cs
using NLog;
using SiteKeeper.Shared.DTOs.MasterSlave;
using SiteKeeper.Shared.Enums;
using SiteKeeper.Slave.Abstractions;
using SiteKeeper.Slave.Models;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace SiteKeeper.Slave.Services.TaskHandlers
{
    /// <summary>
    /// Handles the 'VerifyConfiguration' task, simulating a verification process.
    /// </summary>
    public class VerifyConfigurationTaskHandler : ISlaveTaskHandler
    {
        public SlaveTaskType Handles => SlaveTaskType.VerifyConfiguration;

        public async Task<bool> ExecuteTaskAsync(
            SlaveTaskInstruction instruction,
            SlaveTaskContext slaveTaskContext,
            Action<int> reportProgressPercentAction,
            ILogger taskSpecificLogger)
        {
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
            slaveTaskContext.FinalResultJson = JsonSerializer.Serialize(verificationResult);
            return true;
        }
    }
}
