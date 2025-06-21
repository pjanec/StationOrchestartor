// SiteKeeper.Slave.Abstractions/ISlaveTaskHandler.cs
using SiteKeeper.Shared.DTOs.MasterSlave;
using SiteKeeper.Shared.Enums;
using SiteKeeper.Slave.Models;
using System;
using System.Threading.Tasks;

namespace SiteKeeper.Slave.Abstractions
{
    /// <summary>
    /// Defines the contract for a handler that executes the logic for a specific <see cref="SlaveTaskType"/>.
    /// This replaces the single IExecutiveCodeExecutor, allowing for a more modular and extensible design.
    /// </summary>
    public interface ISlaveTaskHandler
    {
        /// <summary>
        /// Gets the specific <see cref="SlaveTaskType"/> that this handler is responsible for.
        /// </summary>
        SlaveTaskType Handles { get; }

        /// <summary>
        /// Executes the actual work for a given slave task instruction.
        /// </summary>
        /// <param name="instruction">The task instruction received from the master.</param>
        /// <param name="slaveTaskContext">The slave's context for this task, providing the CancellationToken and a place to store the final result.</param>
        /// <param name="reportProgressPercentAction">A callback action to report granular progress (0-100) back to the handler infrastructure.</param>
        /// <param name="taskSpecificLogger">An NLog.ILogger instance pre-configured with MDLC context for this specific task execution.</param>
        /// <returns>A boolean indicating overall success (true) or failure (false) of the task.</returns>
        Task<bool> ExecuteTaskAsync(
            SlaveTaskInstruction instruction,
            SlaveTaskContext slaveTaskContext,
            Action<int> reportProgressPercentAction,
            NLog.ILogger taskSpecificLogger
        );
    }
}