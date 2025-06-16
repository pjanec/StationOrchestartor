using SiteKeeper.Shared.DTOs.AgentHub;
using SiteKeeper.Shared.DTOs.MasterSlave;
using System.Threading.Tasks;

namespace SiteKeeper.Shared.Abstractions.AgentHub
{
    /// <summary>
    /// Defines the contract for methods that the Master Hub (server) can invoke on a connected Slave Agent (client).
    /// </summary>
    /// <remarks>
    /// Slave Agents implement these methods to receive commands and state updates from the Master.
    /// This interface is used for typed SignalR Hubs.
    /// </remarks>
    public interface IAgentHub
    {
        /// <summary>
        /// Instructs the slave agent to prepare for an upcoming task.
        /// Part of the two-phase task commit (Prepare/Execute).
        /// </summary>
        Task ReceivePrepareForTaskInstructionAsync(PrepareForTaskInstruction instruction);

        /// <summary>
        /// Assigns the actual task to the agent for execution, after readiness is confirmed (if applicable).
        /// Part of the two-phase task commit (Prepare/Execute).
        /// </summary>
        Task ReceiveSlaveTaskAsync(SlaveTaskInstruction instruction);

        /// <summary>
        /// Requests the agent to cancel an ongoing task.
        /// </summary>
        Task ReceiveCancelTaskRequestAsync(CancelTaskOnAgentRequest request);

        /// <summary>
        /// Sends a command to the slave agent to synchronize its system time.
        /// </summary>
        Task RequestTimeSyncAsync(AdjustSystemTimeCommand command);

        /// <summary>
        /// Sends a general command to the agent (e.g., ping, get status not covered by specific methods).
        /// </summary>
        Task SendGeneralCommandAsync(NodeGeneralCommandRequest request);

        /// <summary>
        /// Sends Master state information to the agent.
        /// This might be sent upon connection or periodically to ensure agent has context.
        /// </summary>
        Task UpdateMasterStateAsync(MasterStateForAgent state);

        /// <summary>
        /// Requests that the slave agent flush all buffered logs for a specific operation.
        /// The agent is expected to call back with ConfirmLogFlushForTask when done.
        /// </summary>
        /// <param name="operationId">The unique identifier of the operation whose logs should be flushed.</param>
        Task RequestLogFlushForTask(string operationId);
    }
} 