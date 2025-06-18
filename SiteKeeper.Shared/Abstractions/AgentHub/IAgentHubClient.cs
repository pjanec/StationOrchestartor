using SiteKeeper.Shared.DTOs.AgentHub;
using SiteKeeper.Shared.DTOs.MasterSlave;
using System.Threading.Tasks;

namespace SiteKeeper.Shared.Abstractions.AgentHub
{
    /// <summary>
    /// Defines the contract for methods that a Slave Agent (client-side) can invoke on the Master Hub (server-side).
    /// The Master Hub implements this interface to receive data, status updates, and reports from connected Slave Agents.
    /// </summary>
    /// <remarks>
    /// This interface is primarily used for SignalR communication, where Agents act as clients invoking methods on the Master Hub.
    /// </remarks>
    public interface IAgentHubClient
    {
        /// <summary>
        /// Called by a Slave Agent to register itself with the Master upon connection or reconnection.
        /// </summary>
        /// <param name="request">Data transfer object containing registration details of the Slave Agent.</param>
        Task RegisterSlaveAsync(SlaveRegistrationRequest request);

        /// <summary>
        /// Called by a Slave Agent to send a periodic heartbeat signal to the Master, indicating it is still active.
        /// </summary>
        /// <param name="heartbeat">Data transfer object containing heartbeat information, such as agent status and timestamp.</param>
        Task SendHeartbeatAsync(SlaveHeartbeat heartbeat);

        /// <summary>
        /// Called by a Slave Agent to report ongoing progress of an assigned task to the Master.
        /// </summary>
        /// <param name="taskUpdate">Data transfer object containing details about the task progress.</param>
        Task ReportOngoingTaskProgressAsync(SlaveTaskProgressUpdate taskUpdate);

        /// <summary>
        /// Called by a Slave Agent to report its readiness (or failure to prepare) for a specific task,
        /// often in response to a `ReceivePrepareForTaskInstructionAsync` call from the Master.
        /// </summary>
        /// <param name="readinessReport">Data transfer object containing the agent's readiness status for a task.</param>
        Task ReportSlaveTaskReadinessAsync(SlaveTaskReadinessReport readinessReport);

        /// <summary>
        /// Called by a Slave Agent to report its current resource usage (e.g., CPU, memory) to the Master.
        /// </summary>
        /// <param name="resourceUsage">Data transfer object containing details of the agent's resource consumption.</param>
        Task ReportResourceUsageAsync(SlaveResourceUsage resourceUsage);

        /// <summary>
        /// Called by a Slave Agent to confirm that a log flush for a specific operation has been completed.
        /// This is typically invoked in response to a `RequestLogFlushForTask` call from the Master (on `IAgentHub`).
        /// </summary>
        /// <param name="operationId">The unique identifier of the operation for which logs were flushed.</param>
        /// <param name="nodeName">The name of the slave node that completed the log flush.</param>
        Task ConfirmLogFlushForTask(string operationId, string nodeName);

        /// <summary>
        /// Called by a Slave Agent to report a log entry related to a specific task execution to the Master.
        /// </summary>
        /// <param name="logEntry">Data transfer object containing the contextualized log message, including operation ID and log details.</param>
        Task ReportSlaveTaskLogAsync(SlaveTaskLogEntry logEntry);
        
        
        // Methods that were previously in AgentHub.cs but might be specific or need review:
        // Task SendDiagnosticsReportAsync(AgentNodeDiagnosticsReport diagnosticsReport);
        // Task SendGeneralCommandResponseAsync(AgentGeneralCommandResponse commandResponse);
    }
} 