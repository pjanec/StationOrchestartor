using NLog;
using SiteKeeper.Shared.DTOs.API.Operations;
using SiteKeeper.Shared.DTOs.MasterSlave;
using SiteKeeper.Shared.Enums;
using SiteKeeper.Slave.Abstractions;
using SiteKeeper.Slave.Models;
using SiteKeeper.Slave.Services.NLog2;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SiteKeeper.Slave.Services
{
    /// <summary>
    /// Handles the detailed logic for specific operations instructed by the Master Agent,
    /// such as task preparation, execution, and cancellation.
    /// It orchestrates calls to the <see cref="IExecutiveCodeExecutor"/> for actual task work
    /// and uses callbacks to report progress and readiness back to the <see cref="SlaveAgentService"/>.
    /// </summary>
    /// <remarks>
    /// This class is a key component in the slave agent's task management workflow.
    /// It ensures that task lifecycle events are processed correctly and reported appropriately.
    /// Logging within this class uses NLog and is enriched with MDLC context (OperationId, TaskId, NodeName)
    /// which is expected to be set by the calling service (<see cref="SlaveAgentService"/>).
    /// </remarks>
    public class OperationHandler
    {
        private readonly string _agentName;
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        private readonly Func<SlaveTaskProgressUpdate, Task> _sendTaskUpdateAsyncCallback;
        private readonly Func<SlaveTaskReadinessReport, Task> _sendTaskReadinessReportCallback;
        private readonly IExecutiveCodeExecutor _executiveCodeExecutor;

        /// <summary>
        /// Initializes a new instance of the <see cref="OperationHandler"/> class.
        /// </summary>
        /// <param name="agentName">The name of this slave agent, used for populating DTOs.</param>
        /// <param name="sendTaskUpdateAsyncCallback">Callback function to send general task progress and status updates to the master.</param>
        /// <param name="sendTaskReadinessReportCallback">Callback function to send specific task readiness reports to the master.</param>
        /// <param name="executiveCodeExecutor">The executor responsible for running the actual task logic (e.g., scripts, commands).</param>
        public OperationHandler(
            string agentName,
            Func<SlaveTaskProgressUpdate, Task> sendTaskUpdateAsyncCallback,
            Func<SlaveTaskReadinessReport, Task> sendTaskReadinessReportCallback,
            IExecutiveCodeExecutor executiveCodeExecutor)
        {
            _agentName = agentName ?? throw new ArgumentNullException(nameof(agentName));
            _sendTaskUpdateAsyncCallback = sendTaskUpdateAsyncCallback ?? throw new ArgumentNullException(nameof(sendTaskUpdateAsyncCallback));
            _sendTaskReadinessReportCallback = sendTaskReadinessReportCallback ?? throw new ArgumentNullException(nameof(sendTaskReadinessReportCallback));
            _executiveCodeExecutor = executiveCodeExecutor ?? throw new ArgumentNullException(nameof(executiveCodeExecutor));
        }

        /// <summary>
        /// Handles a "Prepare For Task" instruction from the master agent.
        /// </summary>
        /// <param name="prepareInstruction">The instruction DTO containing details about the task to prepare for.</param>
        /// <remarks>
        /// This method performs local readiness checks based on the <paramref name="prepareInstruction"/>.
        /// These checks are typically quick and might involve verifying disk space, current load, or other prerequisites.
        /// It then sends a <see cref="SlaveTaskReadinessReport"/> back to the master via the <see cref="_sendTaskReadinessReportCallback"/>.
        /// MDLC context (OperationId, TaskId, NodeName) is expected to be set by the caller.
        /// </remarks>
        public async Task HandlePrepareForTaskAsync(PrepareForTaskInstruction prepareInstruction)
        {
            _logger.Info($"Handling PrepareForTask: OpId '{prepareInstruction.OperationId}', TaskId '{prepareInstruction.TaskId}', ExpectedType '{prepareInstruction.ExpectedTaskType}'.");

            bool isCurrentlyReady = true;
            string reasonIfNotReady = string.Empty;

            if (prepareInstruction.ExpectedTaskType == SlaveTaskType.TestOrchestration)
            {
                try
                {
                    // Parameters for a test op are passed in the 'PreparationParametersJson' field.
                    // We deserialize them into the now-enhanced TestOpRequest DTO.
                    var testOpRequest = JsonSerializer.Deserialize<TestOpRequest>(prepareInstruction.PreparationParametersJson ?? "{}");
                    if (testOpRequest != null)
                    {
                        _logger.Info($"SIMULATOR: Handling PrepareForTask for TestOrchestration with slave behavior mode {testOpRequest.SlaveBehavior}");
                        
                        // Handle simulated failure on prepare
                        if (testOpRequest.SlaveBehavior == SlaveBehaviorMode.FailOnPrepare)
                        {
                            isCurrentlyReady = false;
                            reasonIfNotReady = testOpRequest.CustomMessage ?? "Simulated failure during readiness check.";
                            _logger.Warn($"Readiness check failed for TaskId {prepareInstruction.TaskId} as requested by simulation.");
                        }
                        // Handle simulated timeout on prepare
                        else if (testOpRequest.SlaveBehavior == SlaveBehaviorMode.TimeoutOnPrepare)
                        {
                            _logger.Warn($"SIMULATOR: Simulating readiness timeout for TaskId {prepareInstruction.TaskId}. This task will not send a readiness report.");
                            return; // Exit without sending a report, forcing a timeout on the master.
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.Error(ex, "Failed to deserialize TestOpRequest from PreparationParametersJson.");
                    isCurrentlyReady = false;
                    reasonIfNotReady = "Could not parse test parameters for readiness check.";
                }
            }

            // === Slave's Internal Readiness Check Logic ===
            // Example: Check disk space for specific task types.
            if (prepareInstruction.ExpectedTaskType == SlaveTaskType.ExecuteBackupStep)
            {
                try
                {
                    long freeSpaceThresholdBytes = 10L * 1024 * 1024 * 1024; // 10GB
                    long actualFreeSpace = GetFreeDiskSpace(string.IsNullOrEmpty(prepareInstruction.TargetResource) ? "C:" : prepareInstruction.TargetResource); // Use TargetResource if specified, else C:

                    if (actualFreeSpace < freeSpaceThresholdBytes)
                    {
                        isCurrentlyReady = false;
                        reasonIfNotReady = $"Insufficient disk space on '{prepareInstruction.TargetResource ?? "default drive"}' for backup. Required: {freeSpaceThresholdBytes / (1024*1024)}MB, Available: {actualFreeSpace / (1024*1024)}MB.";
                        _logger.Warn($"Readiness check failed for TaskId {prepareInstruction.TaskId}: {reasonIfNotReady}");
                    }
                }
                catch(Exception ex)
                {
                    isCurrentlyReady = false;
                    reasonIfNotReady = $"Error checking disk space for '{prepareInstruction.TargetResource ?? "default drive"}': {ex.Message}";
                     _logger.Error(ex, $"Readiness check failed for TaskId {prepareInstruction.TaskId} due to disk space check error: {reasonIfNotReady}");
                }
            }
            // Add other readiness checks as needed (e.g., concurrent task limits if not handled by semaphore acquisition timing later)
            // ===============================================

            var readinessReportDto = new SlaveTaskReadinessReport
            {
                OperationId = prepareInstruction.OperationId,
                TaskId = prepareInstruction.TaskId,
                NodeName = _agentName,
                IsReady = isCurrentlyReady,
                ReasonIfNotReady = reasonIfNotReady,
                TimestampUtc = DateTime.UtcNow
            };
            
            try
            {
                // Use the dedicated callback for readiness reports
                await _sendTaskReadinessReportCallback(readinessReportDto);
                _logger.Info($"Sent readiness report for TaskId '{prepareInstruction.TaskId}': IsReady={isCurrentlyReady}.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to send readiness report for TaskID '{prepareInstruction.TaskId}'.");
                // Optionally, send a general failure update if the specific callback fails catastrophically
                // await _sendTaskUpdateAsyncCallback(new SlaveTaskProgressUpdate { ... Status=Failed ... });
            }
        }

        /// <summary>
        /// Handles an "Execute Task" instruction from the master agent.
        /// </summary>
        /// <param name="instruction">The instruction DTO containing task details and parameters.</param>
        /// <param name="activeSlaveTasks">A concurrent dictionary holding context for currently active tasks.</param>
        /// <param name="concurrentTaskSemaphore">A semaphore to limit concurrent task executions.</param>
        /// <remarks>
        /// This method attempts to acquire a slot from the <paramref name="concurrentTaskSemaphore"/>.
        /// If successful, it creates a <see cref="SlaveTaskContext"/>, adds it to <paramref name="activeSlaveTasks"/>,
        /// and then launches the actual task execution via <see cref="IExecutiveCodeExecutor.ExecuteTaskAsync"/>
        /// in a background thread (using <see cref="Task.Run(Func{Task})"/>) to avoid blocking the caller.
        /// Progress and final status are reported back to the master using the <see cref="_sendTaskUpdateAsyncCallback"/>.
        /// MDLC context (OperationId, TaskId, NodeName) is expected to be set by the caller.
        /// </remarks>
        public async Task HandleSlaveTaskAsync(
            SlaveTaskInstruction instruction,
            ConcurrentDictionary<string, SlaveTaskContext> activeSlaveTasks,
            SemaphoreSlim concurrentTaskSemaphore)
        {
            _logger.Info($"Handling SlaveTask: TaskId '{instruction.TaskId}', Type '{instruction.TaskType}', OpId '{instruction.OperationId}'.");

            // Try to acquire semaphore with a short timeout to prevent blocking if slave is very busy
            if (!await concurrentTaskSemaphore.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None)) // Use CancellationToken.None for semaphore acquisition
            {
                _logger.Warn($"Concurrency limit reached (available slots before this try: {concurrentTaskSemaphore.CurrentCount}). TaskId: {instruction.TaskId} rejected.");
                await _sendTaskUpdateAsyncCallback(new SlaveTaskProgressUpdate { 
                    OperationId = instruction.OperationId, TaskId = instruction.TaskId, NodeName = _agentName,
                    Status = NodeTaskStatus.Failed.ToString(), 
                    Message = "Agent busy, concurrency limit reached.", 
                    TimestampUtc = DateTime.UtcNow 
                });
                return;
            }
            _logger.Debug($"Semaphore acquired for TaskId: {instruction.TaskId}. Available slots after acquisition: {concurrentTaskSemaphore.CurrentCount}");

            var taskContext = new SlaveTaskContext(instruction);
            if (!activeSlaveTasks.TryAdd(instruction.TaskId, taskContext))
            {
                _logger.Warn($"Task {instruction.TaskId} (OpId {instruction.OperationId}) is already active or failed to add to dictionary. Ignoring new request.");
                concurrentTaskSemaphore.Release(); // Release semaphore if task cannot be added
                return;
            }

            _logger.Info($"Task {instruction.TaskId} accepted for execution. Total active tasks now: {activeSlaveTasks.Count}");
            taskContext.CurrentLocalStatus = SlaveLocalTaskExecutionStatus.Starting;
            await _sendTaskUpdateAsyncCallback(new SlaveTaskProgressUpdate {
                OperationId = instruction.OperationId, TaskId = instruction.TaskId, NodeName = _agentName,
                Status = NodeTaskStatus.Starting.ToString(), Message = "Task starting execution.", TimestampUtc = DateTime.UtcNow
            });

            // Execute the task using Task.Run to avoid blocking the SignalR handler thread.
            // The MDLC context (OpId, TaskId, NodeName) set by SlaveAgentService should flow into this Task.Run.
            taskContext.ExecutionTask = Task.Run(async () =>
            {
                // Set the MDLC context here, so it applies to the lifetime of this background task.
                NLog.MappedDiagnosticsLogicalContext.Set("SK-OperationId", instruction.OperationId);
                NLog.MappedDiagnosticsLogicalContext.Set("SK-TaskId", instruction.TaskId);

                // Get a logger instance here; it should pick up MDLC from the current async context if set by caller
                string loggerName = $"{SiteKeeperMasterBoundTarget.ExecutiveLogPrefix}.{instruction.TaskType}";
                NLog.ILogger taskSpecificNLogLogger = LogManager.GetLogger(loggerName);

                bool success = false;
                string finalMessage = "Task execution completed.";

                try
                {
                    taskContext.CurrentLocalStatus = SlaveLocalTaskExecutionStatus.InProgress; // Set before ExecuteTaskAsync
                    // Report InProgress before long execution starts
                     await _sendTaskUpdateAsyncCallback(new SlaveTaskProgressUpdate {
                        OperationId = instruction.OperationId, TaskId = instruction.TaskId, NodeName = _agentName,
                        Status = NodeTaskStatus.InProgress.ToString(), Message = "Task execution in progress.",
                        ProgressPercent = 0, TimestampUtc = DateTime.UtcNow
                    });


                    success = await _executiveCodeExecutor.ExecuteTaskAsync(
                        instruction,
                        taskContext, 
                        (progress) => { 
                            taskContext.CurrentProgressPercent = progress;
                            taskContext.LastStatusUpdateUtc = DateTime.UtcNow;
                            // Throttle intermediate InProgress updates
                            if (taskContext.ShouldSendProgressUpdate(progress)) 
                            {
                                _sendTaskUpdateAsyncCallback(new SlaveTaskProgressUpdate {
                                    OperationId = instruction.OperationId,
                                    TaskId = instruction.TaskId,
                                    NodeName = _agentName,
                                    Status = NodeTaskStatus.InProgress.ToString(), Message = $"Task progress {progress}%",
                                    ProgressPercent = progress, TimestampUtc = DateTime.UtcNow
                                }).ContinueWith(t => { if (t.IsFaulted) _logger.Error(t.Exception, "Error sending progress update."); }, TaskContinuationOptions.OnlyOnFaulted);
                                taskContext.LastReportedProgressPercent = progress; // Update last reported progress
                            }
                            taskSpecificNLogLogger.Debug($"Internal task progress: {progress}%" );
                        },
                        taskSpecificNLogLogger
                    );

                    taskContext.CurrentLocalStatus = success ? SlaveLocalTaskExecutionStatus.Succeeded : SlaveLocalTaskExecutionStatus.Failed;
                    finalMessage = success ? "Task completed successfully by executive code." : (taskContext.FinalResultJson != null && taskContext.FinalResultJson.Contains("error", StringComparison.OrdinalIgnoreCase) ? "Task reported failure by executive code." : "Task execution failed.");
                    if (!success && string.IsNullOrEmpty(taskContext.FinalResultJson)) // Ensure some error info if executive code didn't provide
                    {
                        taskContext.FinalResultJson = JsonSerializer.Serialize(new { error = finalMessage });
                    }
                }
                catch (OperationCanceledException) when (taskContext.CancellationTokenSource.IsCancellationRequested)
                {
                    taskContext.CurrentLocalStatus = SlaveLocalTaskExecutionStatus.Cancelled;
                    finalMessage = "Task cancelled by request during execution.";
                    taskSpecificNLogLogger.Warn(finalMessage);
                    taskContext.FinalResultJson = taskContext.FinalResultJson ?? JsonSerializer.Serialize(new { statusMessage = finalMessage, reason = "Cancelled by master" });
                }
                catch (Exception ex)
                {
                    taskContext.CurrentLocalStatus = SlaveLocalTaskExecutionStatus.Failed;
                    finalMessage = $"Task failed with unhandled exception: {ex.Message}";
                    taskSpecificNLogLogger.Error(ex, finalMessage);
                    taskContext.FinalResultJson = taskContext.FinalResultJson ?? JsonSerializer.Serialize(new { errorType = ex.GetType().FullName, errorMessage = ex.Message, stackTrace = ex.ToString() });
                }
                finally
                {
                    taskContext.LastStatusUpdateUtc = DateTime.UtcNow;
                    await _sendTaskUpdateAsyncCallback(new SlaveTaskProgressUpdate {
                        OperationId = instruction.OperationId,
                        TaskId = instruction.TaskId,
                        NodeName = _agentName,
                        Status = MapSlaveLocalStatusToNodeTaskStatus(taskContext.CurrentLocalStatus).ToString(),
                        Message = finalMessage,
                        ProgressPercent = (taskContext.CurrentLocalStatus >= SlaveLocalTaskExecutionStatus.Succeeded) ? 100 : taskContext.CurrentProgressPercent,
                        ResultJson = taskContext.FinalResultJson,
                        TimestampUtc = taskContext.LastStatusUpdateUtc
                    });

                    activeSlaveTasks.TryRemove(instruction.TaskId, out _);
                    concurrentTaskSemaphore.Release();
                    taskSpecificNLogLogger.Info($"Task {instruction.TaskId} (Op: {instruction.OperationId}) finished with status {taskContext.CurrentLocalStatus}. Semaphore released. Active tasks: {activeSlaveTasks.Count}");

                    // Clear the MDLC context at the very end of the task's lifecycle.
                    NLog.MappedDiagnosticsLogicalContext.Remove("SK-OperationId");
                    NLog.MappedDiagnosticsLogicalContext.Remove("SK-TaskId");
                }
            }, taskContext.CancellationTokenSource.Token);
        }

        /// <summary>
        /// Handles a "Cancel Task" request from the master agent for a specific task.
        /// </summary>
        /// <param name="operationId">The operation ID associated with the task to cancel.</param>
        /// <param name="taskId">The unique ID of the task to cancel.</param>
        /// <param name="activeSlaveTasks">A concurrent dictionary holding context for currently active tasks.</param>
        /// <remarks>
        /// If the specified task is found in <paramref name="activeSlaveTasks"/> and matches the <paramref name="operationId"/>,
        /// and is in a cancellable state, its <see cref="CancellationTokenSource"/> is cancelled.
        /// A "Cancelling" status update is sent back to the master.
        /// MDLC context (OperationId, TaskId, NodeName) is expected to be set by the caller.
        /// </remarks>
        public async Task HandleTaskCancelRequestAsync(string operationId, string taskId, ConcurrentDictionary<string, SlaveTaskContext> activeSlaveTasks)
        {
            _logger.Info($"Handling cancellation request for OpId: {operationId}, TaskId: {taskId}");
            if (activeSlaveTasks.TryGetValue(taskId, out var taskContext))
            {
                // Ensure the operation ID also matches to prevent cancelling the wrong instance if a TaskId was reused with a new OpId.
                if (taskContext.Instruction.OperationId == operationId)
                {
                    if (taskContext.CurrentLocalStatus < SlaveLocalTaskExecutionStatus.Succeeded && // Not already completed
                        taskContext.CurrentLocalStatus != SlaveLocalTaskExecutionStatus.Cancelled && // Not already cancelled
                        taskContext.CurrentLocalStatus != SlaveLocalTaskExecutionStatus.Cancelling) // Not already cancelling
                    {
                        _logger.Info($"Signaling cancellation for TaskId: {taskId}. Current status: {taskContext.CurrentLocalStatus}");
                        taskContext.CurrentLocalStatus = SlaveLocalTaskExecutionStatus.Cancelling; // Set local status
                        taskContext.CancellationTokenSource.Cancel(); // Signal the CancellationToken

                        // Report "Cancelling" status to master
                        await _sendTaskUpdateAsyncCallback(new SlaveTaskProgressUpdate {
                            OperationId = operationId, TaskId = taskId, NodeName = _agentName,
                            Status = NodeTaskStatus.Cancelling.ToString(), // Master's view of "Cancelling"
                            Message = "Cancellation initiated by master; attempting to stop task.",
                            TimestampUtc = DateTime.UtcNow
                        });
                    } else { _logger.Info($"Task {taskId} is already in a terminal state ({taskContext.CurrentLocalStatus}) or already cancelling. Ignoring redundant cancellation request."); }
                } else { _logger.Warn($"Mismatched OperationId for TaskId {taskId} on cancel request. Request OpId: {operationId}, Task OpId: {taskContext.Instruction.OperationId}. Ignoring."); }
            } else { _logger.Warn($"Task {taskId} not found in active tasks. Cannot cancel."); }
        }

        /// <summary>
        /// Handles a command from the master agent to adjust the system time on the slave.
        /// </summary>
        /// <param name="command">The DTO containing the master's authoritative UTC timestamp.</param>
        /// <param name="maxTimeAdjustmentMinutesWithoutForce">The maximum time difference (in minutes) allowed for automatic adjustment without a force flag.</param>
        /// <remarks>
        /// This method calculates the difference between the slave's current UTC time and the master's authoritative time.
        /// If the difference is within the <paramref name="maxTimeAdjustmentMinutesWithoutForce"/> threshold,
        /// it attempts to set the system time using platform-specific native methods.
        /// A status update is sent back to the master indicating success or failure.
        /// MDLC context (OperationId, TaskId, NodeName) is expected to be set by the caller.
        /// Note: The actual time setting logic is platform-dependent (Windows in this case).
        /// </remarks>
        public async Task HandleAdjustSystemTimeAsync(AdjustSystemTimeCommand command, int maxTimeAdjustmentMinutesWithoutForce)
        {
            _logger.Info($"Handling AdjustSystemTime. Master's Authoritative UTC: {command.AuthoritativeUtcTimestamp}. My UTC before: {DateTime.UtcNow}");

            TimeSpan differenceFromCurrent = DateTime.UtcNow - command.AuthoritativeUtcTimestamp;
            string operationSystemTimeSync = "SYSTEM_TIME_SYNC"; // Consistent OperationId for these system actions
            string taskIdTimeSyncAck = $"timesyncack-{DateTime.UtcNow.Ticks}";


            if (Math.Abs(differenceFromCurrent.TotalMinutes) > maxTimeAdjustmentMinutesWithoutForce && !command.ForceAdjustment)
            {
                string message = $"Time adjustment from master ({command.AuthoritativeUtcTimestamp}) is too large (difference: {differenceFromCurrent.TotalMinutes:F2} minutes) and ForceAdjustment is false. Skipping adjustment.";
                _logger.Error(message);
                await _sendTaskUpdateAsyncCallback(new SlaveTaskProgressUpdate { 
                    OperationId = operationSystemTimeSync, TaskId = taskIdTimeSyncAck, NodeName = _agentName,
                    Status = NodeTaskStatus.Failed.ToString(), Message = message,
                    TimestampUtc = DateTime.UtcNow
                });
                return;
            }

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var st = new NativeMethods.SYSTEMTIME
                    {
                        wYear = (ushort)command.AuthoritativeUtcTimestamp.Year,
                        wMonth = (ushort)command.AuthoritativeUtcTimestamp.Month,
                        wDay = (ushort)command.AuthoritativeUtcTimestamp.Day,
                        wHour = (ushort)command.AuthoritativeUtcTimestamp.Hour,
                        wMinute = (ushort)command.AuthoritativeUtcTimestamp.Minute,
                        wSecond = (ushort)command.AuthoritativeUtcTimestamp.Second,
                        wMilliseconds = (ushort)command.AuthoritativeUtcTimestamp.Millisecond
                    };
                    if (NativeMethods.SetSystemTime(ref st))
                    {
                        string successMsg = $"System time successfully adjusted to {command.AuthoritativeUtcTimestamp} (was {command.AuthoritativeUtcTimestamp.Add(differenceFromCurrent)}). Difference: {differenceFromCurrent.TotalSeconds:F2}s.";
                        _logger.Info(successMsg);
                        await _sendTaskUpdateAsyncCallback(new SlaveTaskProgressUpdate {
                             OperationId = operationSystemTimeSync, TaskId = taskIdTimeSyncAck, NodeName = _agentName,
                             Status = NodeTaskStatus.Succeeded.ToString(), Message = successMsg, TimestampUtc = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        string errorMsg = $"Failed to set system time. Error code: {Marshal.GetLastWin32Error()}";
                        _logger.Error(errorMsg);
                         await _sendTaskUpdateAsyncCallback(new SlaveTaskProgressUpdate {
                            OperationId = operationSystemTimeSync, TaskId = taskIdTimeSyncAck, NodeName = _agentName,
                            Status = NodeTaskStatus.Failed.ToString(), Message = errorMsg, TimestampUtc = DateTime.UtcNow
                        });
                    }
                }
                else
                {
                    string unsupportedMsg = "System time adjustment is not supported on this OS platform.";
                     _logger.Warn(unsupportedMsg);
                    await _sendTaskUpdateAsyncCallback(new SlaveTaskProgressUpdate {
                        OperationId = operationSystemTimeSync, TaskId = taskIdTimeSyncAck, NodeName = _agentName,
                        Status = NodeTaskStatus.Failed.ToString(), Message = unsupportedMsg, TimestampUtc = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                string exceptionMsg = $"Exception during system time adjustment: {ex.Message}";
                _logger.Error(ex, exceptionMsg);
                await _sendTaskUpdateAsyncCallback(new SlaveTaskProgressUpdate {
                    OperationId = operationSystemTimeSync, TaskId = taskIdTimeSyncAck, NodeName = _agentName,
                    Status = NodeTaskStatus.Failed.ToString(), Message = exceptionMsg, TimestampUtc = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Maps the slave's internal <see cref="SlaveLocalTaskExecutionStatus"/> to the master-facing <see cref="NodeTaskStatus"/>.
        /// </summary>
        /// <param name="localStatus">The local status of the task on the slave.</param>
        /// <returns>The corresponding <see cref="NodeTaskStatus"/> for reporting to the master.</returns>
        private NodeTaskStatus MapSlaveLocalStatusToNodeTaskStatus(SlaveLocalTaskExecutionStatus localStatus)
        {
            switch (localStatus)
            {
                case SlaveLocalTaskExecutionStatus.Starting: return NodeTaskStatus.Starting;
                case SlaveLocalTaskExecutionStatus.InProgress: return NodeTaskStatus.InProgress;
                case SlaveLocalTaskExecutionStatus.Succeeded: return NodeTaskStatus.Succeeded;
                case SlaveLocalTaskExecutionStatus.Failed: return NodeTaskStatus.Failed;
                case SlaveLocalTaskExecutionStatus.Cancelled: return NodeTaskStatus.Cancelled;
                case SlaveLocalTaskExecutionStatus.Cancelling: return NodeTaskStatus.Cancelling; // Added mapping
                default:
                    _logger.Warn($"Unknown SlaveLocalTaskExecutionStatus: {localStatus}. Defaulting to Failed.");
                    return NodeTaskStatus.Failed;
        }
        }

        // Basic disk space check (platform-dependent ways are better for specific drives)
        // Consider refining this to use DriveInfo for specific drive if 'driveName' is a letter like "C" or path like "C:"
        /// <summary>
        /// Gets the available free disk space for the specified drive.
        /// </summary>
        /// <param name="drivePathOrLetter">A path or drive letter (e.g., "C:", "C", "/mnt/data").
        /// If a path is provided, its root will be used. If only a letter (e.g., "C") is provided, it's assumed to be a drive letter.</param>
        /// <returns>The available free space in bytes. Returns 0 if an error occurs or the drive cannot be determined.</returns>
        /// <remarks>
        /// This method attempts to determine the root of the given path to instantiate <see cref="DriveInfo"/>.
        /// If <paramref name="drivePathOrLetter"/> is a single letter (e.g., "C"), it appends ":\" to form a valid root.
        /// If the root cannot be determined or <see cref="DriveInfo"/> fails, it logs an error and returns 0.
        /// </remarks>
        private long GetFreeDiskSpace(string drivePathOrLetter)
        {
            try
            {
                // Ensure drivePathOrLetter is just the root (e.g. "C:\") for DriveInfo
                string rootPath = Path.GetPathRoot(drivePathOrLetter);
                if (string.IsNullOrEmpty(rootPath)) 
                {
                     // If it's just "C", append ":\"
                    if (drivePathOrLetter.Length == 1 && char.IsLetter(drivePathOrLetter[0]))
                    {
                        rootPath = drivePathOrLetter + ":\\";
                    }
                    else // if not a letter, or longer but no root, can't determine drive
                    {
                         _logger.Warn($"Cannot determine root path for drive '{drivePathOrLetter}' to check free space. Defaulting to very large number.");
                        return long.MaxValue; // Or throw
                    }
                }

                DriveInfo driveInfo = new DriveInfo(rootPath);
                return driveInfo.AvailableFreeSpace;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to get free disk space for drive '{drivePathOrLetter}'. Returning 0.");
                return 0; // Or throw to indicate failure to check
            }
        }
    }

    // Keep NativeMethods internal to this file if only used here for time setting.
    internal static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct SYSTEMTIME
        {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetSystemTime(ref SYSTEMTIME lpSystemTime);
    }
} 