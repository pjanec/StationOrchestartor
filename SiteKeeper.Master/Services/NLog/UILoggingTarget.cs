using Microsoft.Extensions.DependencyInjection;
using NLog;
using NLog.Targets;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Abstractions.Workflow;
using SiteKeeper.Shared.DTOs.AgentHub;
using SiteKeeper.Shared.DTOs.SignalR;
using SiteKeeper.Shared.Enums;
using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SiteKeeper.Master.Services.NLog2
{
    /// <summary>
    /// A custom NLog target designed to forward log events from the Master service to connected UI clients via SignalR.
    /// It uses a high-performance, asynchronous producer/consumer queue (`System.Threading.Channels`) to ensure that
    /// logging calls do not block application threads, while guaranteeing that logs are processed and sent in the exact
    /// order they were generated. This target specifically filters for logs that have a "MasterActionId" property
    /// in their Mapped Diagnostics Logical Context (MDLC), ensuring that only logs related to an active workflow are sent to the UI.
    /// </summary>
    [Target("UILoggingTarget")]
    public sealed class UILoggingTarget : TargetWithContext
    {
        private readonly IServiceProvider _serviceProvider;
        
        // The channel acts as a highly efficient, in-memory queue. It can hold different types of objects,
        // which allows us to queue both log events and special "sentinel" objects for flushing.
        private readonly Channel<object> _logQueue;

        /// <summary>
        /// Initializes a new instance of the UILoggingTarget.
        /// </summary>
        /// <param name="serviceProvider">The application's IServiceProvider, used to resolve services like IGuiNotifierService.</param>
        public UILoggingTarget(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            // Create an unbounded channel, meaning the queue can grow as needed. A single consumer (reader)
            // will process items, which is perfect for ensuring sequential processing.
            _logQueue = Channel.CreateUnbounded<object>(new UnboundedChannelOptions { SingleReader = true });

            // Start a single, long-running background task to process the queue.
            // This is the "consumer" part of the pattern.
            _ = ProcessQueueAsync();
        }

        /// <summary>
        /// This method is called by the NLog framework for every log message. It is designed to be extremely fast.
        /// Its only job is to synchronously add the log message to the in-memory queue and return immediately.
        /// This is the "producer" part of the pattern and it does not block the application thread.
        /// </summary>
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
        /// Provides a mechanism for the workflow engine to wait until all currently buffered logs have been sent.
        /// This is crucial for synchronizing log streams between stages of a Master Action.
        /// </summary>
        /// <returns>A Task that completes only when the queue has been fully processed up to this point.</returns>
        public Task FlushAsync()
        {
            // Create a TaskCompletionSource, which is a controllable Task.
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            
            // Write the TCS object itself into the queue. It acts as a "sentinel" or "marker".
            _logQueue.Writer.TryWrite(tcs);
            
            // Return the task to the caller, who can then 'await' it.
            return tcs.Task;
        }

        /// <summary>
        /// A single, dedicated background task that continuously reads from the queue.
        /// It processes items one by one, which guarantees that logs are handled in the order they were received.
        /// </summary>
        private async Task ProcessQueueAsync()
        {
            // ReadAllAsync efficiently waits until an item is available in the queue.
            await foreach (var item in _logQueue.Reader.ReadAllAsync())
            {
                if (item is LogEventInfo logEvent)
                {
                    // This is a normal log event. We process it for sending.
                    await HandleLogEventAsync(logEvent);
                }
                else if (item is TaskCompletionSource tcs)
                {
                    // This is our sentinel object from a FlushAsync() call.
                    // Reaching it means all previous log events in the queue have been processed.
                    // We now complete the task, which unblocks the original caller of FlushAsync().
                    tcs.TrySetResult();
                }
            }
        }

        /// <summary>
        /// Handles the logic for a single log event popped from the queue.
        /// It filters for the MasterActionId, resolves dependencies, and sends the SignalR message.
        /// </summary>
        private async Task HandleLogEventAsync(LogEventInfo logEvent)
        {
            try
            {
                // Retrieve the MasterActionId directly from the NLog event properties (set by MDLC).
                if (!logEvent.Properties.TryGetValue("MasterActionId", out var opIdObj) || opIdObj is not string masterActionId || string.IsNullOrEmpty(masterActionId))
                {
                    // If there's no MasterActionId, it's not a log we need to journal or send to the UI.
                    return;
                }

                // We need to resolve services from a new DI scope because this is a long-running singleton task.
                using var scope = _serviceProvider.CreateScope();
                var guiNotifier = scope.ServiceProvider.GetRequiredService<IGuiNotifierService>();
                var journalService = scope.ServiceProvider.GetRequiredService<IJournalService>();
        
                // We create a SlaveTaskLogEntry, which is what your existing
                // IGuiNotifierService.NotifyOperationLogEntryAsync method expects.
                var logEntryDto = new SlaveTaskLogEntry
                {
                    // The UI expects an "OperationId", so we map our MasterActionId to it.
                    OperationId = masterActionId, 

                    // For logs originating from the master, the TaskId is not directly applicable.
                    // We can reuse the MasterActionId or a special marker.
                    TaskId = masterActionId, // For a master log, TaskId can be the same as the OperationId

                    // We use a special name to signify the log originated from the master orchestrator.
                    NodeName = "_master", // Use the virtual node name

                    LogMessage = logEvent.FormattedMessage,
                    LogLevel = MapNLogLevelToSiteKeeperLevel(logEvent.Level),
                    TimestampUtc = logEvent.TimeStamp.ToUniversalTime()
                };

                // *** THIS IS THE FIX - PART 2 ***
                // Create two tasks: one to notify the UI and one to write to the journal.
                // This allows them to run in parallel.
                var notifyTask = guiNotifier.NotifyOperationLogEntryAsync(logEntryDto);
                var journalTask = journalService.AppendToStageLogAsync(masterActionId, logEntryDto);

                // Await both tasks to ensure the log is both sent and persisted before processing the next item.
                await Task.WhenAll(notifyTask, journalTask);
            }
            catch (Exception ex)
            {
                // If forwarding to the UI fails, we log the error to a standard file target.
                // It is critical to not re-throw here, as that would kill the consumer task.
                LogManager.GetCurrentClassLogger().Error(ex, "Failed to process log in UILoggingTarget.");
            }
        }        

        /// <summary>
        /// Maps NLog's LogLevel to the shared SiteKeeper LogLevel enum for the DTO.
        /// </summary>
        private static Shared.Enums.LogLevel MapNLogLevelToSiteKeeperLevel(NLog.LogLevel nlogLevel)
        {
            if (nlogLevel == NLog.LogLevel.Fatal) return Shared.Enums.LogLevel.Critical;
            if (nlogLevel == NLog.LogLevel.Error) return Shared.Enums.LogLevel.Error;
            if (nlogLevel == NLog.LogLevel.Warn) return Shared.Enums.LogLevel.Warning;
            return Shared.Enums.LogLevel.Information; // Default for Info, Debug, Trace
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
