// Defined in SiteKeeper.Slave.Abstractions (or SiteKeeper.Shared if master needs to know about it)
using SiteKeeper.Shared.DTOs.MasterSlave;
using SiteKeeper.Slave.Models; // For SlaveTaskContext
using System;
using System.Threading;
using System.Threading.Tasks;
// using NLog; // NLog.ILogger is passed as parameter

namespace SiteKeeper.Slave.Abstractions
{
    /// <summary>
    /// Defines the contract for executing the underlying "executive code" for a slave task.
    /// This is the boundary that will be mocked in integration tests or implemented
    /// by the actual task execution logic (which, for now, is simulation).
    /// </summary>
    public interface IExecutiveCodeExecutor
    {
        /// <summary>
        /// Executes (or simulates) the actual work for a given slave task instruction.
        /// </summary>
        /// <param name="instruction">The task instruction received from the master, containing
        /// OperationId, TaskId, TaskType, and ParametersJson.</param>
        /// <param name="slaveTaskContext">The slave's context for this task, providing the CancellationToken
        /// and a place to store the final result JSON (via its FinalResultJson property).</param>
        /// <param name="reportProgressPercentAction">A callback action to report granular progress (0-100)
        /// back to the OperationHandler, which can then be relayed to the master.</param>
        /// <param name="taskSpecificLogger">An NLog.ILogger instance that is already configured with
        /// MDLC context (OperationId, TaskId, NodeName) for this specific task execution.</param>
        /// <returns>A boolean indicating overall success (true) or failure (false) of the executive code.
        /// The detailed result or error information should be set in <paramref name="slaveTaskContext"/>.FinalResultJson by this method.
        /// </returns>
        Task<bool> ExecuteTaskAsync(
            SlaveTaskInstruction instruction,
            SlaveTaskContext slaveTaskContext,
            Action<int> reportProgressPercentAction,
            NLog.ILogger taskSpecificLogger
        );
    }
} 