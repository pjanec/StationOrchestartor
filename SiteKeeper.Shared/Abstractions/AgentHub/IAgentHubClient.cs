using SiteKeeper.Shared.DTOs.AgentHub;
using SiteKeeper.Shared.DTOs.MasterSlave;
using System.Threading.Tasks;

namespace SiteKeeper.Shared.Abstractions.AgentHub
{
    /// <summary>
    /// Defines the contract for methods that a Slave Agent (client) can invoke on the Master Hub (server).
    /// </summary>
    /// <remarks>
    /// The Master Hub implements these methods. Slave Agents use these to send data and reports to the Master.
    /// This interface is used for typed SignalR Hubs (client-side invocation).
    /// </remarks>
    public interface IAgentHubClient
    {
        /// <summary>
        /// Registers a slave agent with the master.
        /// </summary>
        Task RegisterSlaveAsync(SlaveRegistrationRequest request);

        /// <summary>
        /// Sends a heartbeat signal from the slave to the master.
        /// </summary>
        Task SendHeartbeatAsync(SlaveHeartbeat heartbeat);

        /// <summary>
        /// Reports ongoing progress of a task execution from the slave to the master.
        /// </summary>
        Task ReportOngoingTaskProgressAsync(SlaveTaskProgressUpdate taskUpdate);

        /// <summary>
        /// Reports the readiness of a slave for a specific task to the master.
        /// </summary>
        Task ReportSlaveTaskReadinessAsync(SlaveTaskReadinessReport readinessReport);

        /// <summary>
        /// Reports current resource usage from the slave to the master.
        /// </summary>
        Task ReportResourceUsageAsync(SlaveResourceUsage resourceUsage);

        /// <summary>
        /// Confirms that a log flush for a specific operation has been completed on the slave.
        /// This is called by the agent in response to a RequestLogFlushForTask from the master.
        /// </summary>
        /// <param name="operationId">The unique identifier of the operation that was flushed.</param>
        /// <param name="nodeName">The name of the node that completed the flush.</param>
        Task ConfirmLogFlushForTask(string operationId, string nodeName);

        // Methods that were previously in AgentHub.cs but might be specific or need review:
        // Task SendDiagnosticsReportAsync(AgentNodeDiagnosticsReport diagnosticsReport);
        // Task SendGeneralCommandResponseAsync(AgentGeneralCommandResponse commandResponse);
    }
} 