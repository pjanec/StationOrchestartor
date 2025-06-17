using Microsoft.AspNetCore.SignalR.Client;
using NLog;
using NLog.Common;
using NLog.Targets;
using SiteKeeper.Shared.DTOs.AgentHub;
using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using SiteKeeperLogLevel = SiteKeeper.Shared.Enums.LogLevel;

namespace SiteKeeper.Slave.Services.NLog2
{
    /// <summary>
    /// Custom NLog target that sends contextualized log messages to the SiteKeeper Master's AgentHub.
    /// This version is enhanced with an in-memory queue to ensure ordered, non-blocking log processing
    /// and to support remote log flushing.
    /// </summary>
    /// <remarks>
    /// This target is a key component of the distributed logging strategy. It captures log events,
    /// enqueues them, and processes them in a dedicated async loop. This prevents logging calls from
    /// blocking the application threads while waiting for network operations.
    /// 
    /// It works by checking for specific MappedDiagnosticsLogicalContext (MDLC) properties
    /// (SK-OperationId, SK-TaskId, SK-NodeName). If present, it sends the log as a 
    /// <see cref="SlaveTaskLogEntry"/> to the master.
    /// 
    /// The SignalR HubConnection is provided by the SlaveAgentService via the static
    /// <see cref="SetHubConnectionProvider"/> method.
    /// 
    /// The <see cref="FlushAsync"/> method allows an external caller (like the Multi-Node Coordinator)
    /// to wait until all currently queued logs have been sent, ensuring log synchronization.
    /// </remarks>
    [Target("SiteKeeperMasterBound")]
    public sealed class SiteKeeperMasterBoundTarget : TargetWithContext
    {
        private static Func<HubConnection?>? _hubConnectionProvider;
        private readonly Channel<object> _logQueue;

        public const string ExecutiveLogPrefix = "Executive";

        /// <summary>
        /// Initializes a new instance of the <see cref="SiteKeeperMasterBoundTarget"/> class.
        /// It creates the unbounded channel for queuing log events and starts the background processing task.
        /// </summary>
        public SiteKeeperMasterBoundTarget()
        {
            // Use an unbounded channel with a single reader for optimized performance.
            _logQueue = Channel.CreateUnbounded<object>(new UnboundedChannelOptions { SingleReader = true });
            // Start the queue processor in a fire-and-forget task.
            _ = ProcessQueueAsync();
        }

        /// <summary>
        /// Sets the provider function that this target will use to get the active SignalR HubConnection.
        /// This is called by the SlaveAgentService when a connection to the master is established.
        /// </summary>
        /// <param name="provider">A function that returns the current HubConnection, or null if not connected.</param>
        public static void SetHubConnectionProvider(Func<HubConnection?> provider) => _hubConnectionProvider = provider;

        /// <summary>
        /// Writes the log event to the internal queue. This method is called by the NLog framework
        /// and should return quickly.
        /// </summary>
        /// <param name="logEvent">The log event information.</param>
        protected override void Write(LogEventInfo logEvent)
        {
            var props = GetAllProperties( logEvent ); // get mdlc properties for this log event

			// save all to logEvent.Properties
			foreach( var prop in props )
			{
				logEvent.Properties[prop.Key] = prop.Value;
			}

            _logQueue.Writer.TryWrite(logEvent);
        }

        /// <summary>
        /// Enqueues a special marker and returns a task that completes when the marker is processed.
        /// This effectively allows waiting for the log queue to be empty up to this point in time.
        /// </summary>
        /// <returns>A task that completes when all logs queued before this call have been sent.</returns>
        public Task FlushAsync()
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _logQueue.Writer.TryWrite(tcs);
            return tcs.Task;
        }

        /// <summary>
        /// The core processing loop for the log queue. It continuously reads from the channel
        /// and processes items, which can be either log events or flush markers.
        /// </summary>
        private async Task ProcessQueueAsync()
        {
            // ReadAllAsync provides an efficient way to process items as they become available.
            await foreach (var item in _logQueue.Reader.ReadAllAsync())
            {
                if (item is LogEventInfo logEvent)
                {
                    // If the item is a log event, try to send it.
                    await TrySendLogAsync(logEvent);
                }
                else if (item is TaskCompletionSource tcs)
                {
                    // If the item is a flush marker, complete the associated task.
                    tcs.TrySetResult();
                }
            }
        }

        /// <summary>
        /// Handles the logic for sending a single log event to the master.
        /// </summary>
        private async Task TrySendLogAsync(LogEventInfo logEvent)
        {
            var hubConnection = _hubConnectionProvider?.Invoke();
            if (hubConnection?.State != HubConnectionState.Connected)
            {
                return; // Can't send if not connected.
            }

            // A log event is only relevant for remote logging if it has the OperationId.
            if (!logEvent.Properties.TryGetValue("SK-OperationId", out var opIdObj) || opIdObj is not string opId || string.IsNullOrEmpty(opId))
            {
                return;
            }

            // Also retrieve the other contextual properties.
            logEvent.Properties.TryGetValue("SK-TaskId", out var taskIdObj);
            logEvent.Properties.TryGetValue("SK-NodeName", out var nodeNameObj);

            try
            {
                var entry = new SlaveTaskLogEntry
                {
                    OperationId = opId,
                    TaskId = taskIdObj as string ?? string.Empty,
                    NodeName = nodeNameObj as string ?? string.Empty,
                    LogLevel = MapNLogLevelToSiteKeeperLevel(logEvent.Level),
                    LogMessage = RenderLogEvent(Layout, logEvent),
                    TimestampUtc = logEvent.TimeStamp.ToUniversalTime()
                };

                // The rest of this method can remain the same...
                var logger = LogManager.GetCurrentClassLogger();
                logger.Log(NLog.LogLevel.Debug, "Attempting to send SlaveTaskLogEntry to master. OpId: {0}, TaskId: {1}, Message: '{2}'", entry.OperationId, entry.TaskId, entry.LogMessage);

                await hubConnection.InvokeAsync("ReportSlaveTaskLogAsync", entry);
        
                logger.Log(NLog.LogLevel.Debug, "Successfully invoked ReportSlaveTaskLogAsync on master for TaskId: {0}", entry.TaskId);

            }
            catch (Exception ex)
            {
                LogManager.GetCurrentClassLogger().Error(ex, "Failed to send slave task log to master.");
            }
        }

        /// <summary>
        /// Maps NLog's LogLevel to the shared SiteKeeper LogLevel enum.
        /// </summary>
        private static SiteKeeperLogLevel MapNLogLevelToSiteKeeperLevel(NLog.LogLevel nlogLevel)
        {
            if (nlogLevel == NLog.LogLevel.Trace) return SiteKeeperLogLevel.Information;
            if (nlogLevel == NLog.LogLevel.Debug) return SiteKeeperLogLevel.Information;
            if (nlogLevel == NLog.LogLevel.Info) return SiteKeeperLogLevel.Information;
            if (nlogLevel == NLog.LogLevel.Warn) return SiteKeeperLogLevel.Warning;
            if (nlogLevel == NLog.LogLevel.Error) return SiteKeeperLogLevel.Error;
            if (nlogLevel == NLog.LogLevel.Fatal) return SiteKeeperLogLevel.Critical;
            return SiteKeeperLogLevel.Information;
        }

        /// <summary>
        /// This method is called by NLog during application shutdown when LogManager.Shutdown() is invoked.
        /// It's the critical step for gracefully terminating the background processing task.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if from a finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Mark the channel's writer as complete. This signals to the consumer
                // that no more items will ever be added to the queue.
                _logQueue.Writer.Complete();
            }
            base.Dispose(disposing);
        }
    }
} 