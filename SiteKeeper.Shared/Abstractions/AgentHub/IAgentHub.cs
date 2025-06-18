using SiteKeeper.Shared.DTOs.AgentHub;
using SiteKeeper.Shared.DTOs.MasterSlave;
using System.Threading.Tasks;

namespace SiteKeeper.Shared.Abstractions.AgentHub
{
    /// <summary>
    /// Defines the contract for methods that the Master Hub (server-side) can invoke on a connected Slave Agent (client-side).
    /// Slave Agents implement this interface to receive commands, instructions, and state updates from the Master.
    /// </summary>
    /// <remarks>
    /// This interface is primarily used for SignalR communication, where the Master acts as the Hub and Agents are connected clients.
    /// </remarks>
    public interface IAgentHub
    {
        /// <summary>
        /// Called by the Master to instruct the slave agent to prepare for an upcoming task.
        /// This is part of a two-phase task commit process (Prepare/Execute).
        /// </summary>
        /// <param name="instruction">Details of the preparation instruction.</param>
        Task ReceivePrepareForTaskInstructionAsync(PrepareForTaskInstruction instruction);

        /// <summary>
        /// Called by the Master to assign the actual task to the agent for execution, typically after readiness is confirmed.
        /// This is part of a two-phase task commit process (Prepare/Execute).
        /// </summary>
        /// <param name="instruction">Details of the task instruction.</param>
        Task ReceiveSlaveTaskAsync(SlaveTaskInstruction instruction);

        /// <summary>
        /// Called by the Master to request the agent to cancel an ongoing or scheduled task.
        /// </summary>
        /// <param name="request">Details of the cancellation request, including the Operation ID of the task to be cancelled.</param>
        Task ReceiveCancelTaskRequestAsync(CancelTaskOnAgentRequest request);

        /// <summary>
        /// Called by the Master to send a command to the slave agent to synchronize its system time.
        /// </summary>
        /// <param name="command">Details of the time adjustment command.</param>
        Task RequestTimeSyncAsync(AdjustSystemTimeCommand command);

        /// <summary>
        /// Called by the Master to send a general command to the agent (e.g., ping, request specific status not covered by other methods).
        /// </summary>
        /// <param name="request">Details of the general command.</param>
        Task SendGeneralCommandAsync(NodeGeneralCommandRequest request);

        /// <summary>
        /// Called by the Master to send its current state information to the agent.
        /// This might be sent upon initial agent connection or periodically to ensure the agent has up-to-date context about the Master.
        /// </summary>
        /// <param name="state">The Master's state information relevant for the agent.</param>
        Task UpdateMasterStateAsync(MasterStateForAgent state);

        /// <summary>
        /// Called by the Master to request that the slave agent flush all buffered logs related to a specific operation.
        /// The agent is expected to confirm this action by invoking the `ConfirmLogFlushForTask` method on the `IAgentHubClient` interface, implemented by the Master.
        /// </summary>
        /// <param name="operationId">The unique identifier of the operation whose logs should be flushed.</param>
        Task RequestLogFlushForTask(string operationId);
    }
} 