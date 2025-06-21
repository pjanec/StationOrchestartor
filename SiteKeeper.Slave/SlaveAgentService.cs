using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SiteKeeper.Shared.DTOs.AgentHub;
using SiteKeeper.Shared.DTOs.MasterSlave;
using SiteKeeper.Shared.Enums;
using SiteKeeper.Slave.Abstractions;
using SiteKeeper.Slave.Configuration;
using SiteKeeper.Slave.Models;
using SiteKeeper.Slave.Services;
using SiteKeeper.Slave.Services.NLog2;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SiteKeeper.Slave.Services.TaskHandlers;

namespace SiteKeeper.Slave
{
    /// <summary>
    /// Manages the slave agent's lifecycle, its communication with the Master Agent via SignalR,
    /// and orchestrates local task execution based on instructions from the master.
    /// </summary>
    /// <remarks>
    /// This service is a core component of the SiteKeeper Slave Agent. Its responsibilities include:
    /// <list type="bullet">
    ///   <item><description>Establishing and maintaining a persistent SignalR connection to the Master Agent Hub.</description></item>
    ///   <item><description>Registering the slave agent with the master upon connection.</description></item>
    ///   <item><description>Handling incoming messages and instructions from the master, such as requests to prepare for a task, execute a task, or cancel an ongoing task. These are typically delegated to the <see cref="SlaveCommandsHandler"/>.</description></item>
    ///   <item><description>Sending regular heartbeat signals and periodic resource usage reports to the master.</description></item>
    ///   <item><description>Managing concurrent task execution using a semaphore, based on the <see cref="SlaveConfig.MaxConcurrentTasks"/> setting.</description></item>
    ///   <item><description>Ensuring graceful startup and shutdown, integrating with the .NET Generic Host lifecycle via <see cref="IHostedService"/>.</description></item>
    ///   <item><description>Providing callbacks to the <see cref="SlaveCommandsHandler"/> for sending task progress updates and readiness reports back to the master.</description></item>
    /// </list>
    /// Communication with the master primarily uses DTOs defined in <see cref="SiteKeeper.Shared.DTOs.MasterSlave"/> and <see cref="SiteKeeper.Shared.DTOs.AgentHub"/>.
    /// Configuration is drawn from <see cref="SlaveConfig"/>.
    /// Logging is performed using an <see cref="ILogger{TCategoryName}"/>, with NLog as the underlying provider, often enriched with MDLC properties for operation/task context.
    /// </remarks>
    public class SlaveAgentService : IHostedService, IDisposable
    {
        private readonly ILogger<SlaveAgentService> _logger;
        private readonly SlaveConfig _config;
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly SlaveCommandsHandler _slaveCommandsHandler;

        private HubConnection? _masterConnection;
        private Timer? _heartbeatTimer;
        private Timer? _resourceMonitorTimer;
        private readonly ConcurrentDictionary<string, SlaveTaskContext> _activeSlaveTasks = new ConcurrentDictionary<string, SlaveTaskContext>();
        private readonly SemaphoreSlim _concurrentTaskSemaphore;
        private CancellationTokenSource? _stoppingCts;

        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _memoryCounter;
        private ulong _totalMemoryBytes = 0;

        private const string LogMdlcActionId = "SK-ActionId";
        private const string LogMdlcTaskId = "SK-TaskId";
        private const string LogMdlcNodeName = "SK-NodeName";

        /// <summary>
        /// Initializes a new instance of the <see cref="SlaveAgentService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance injected by DI, for logging service activities.</param>
        /// <param name="configOptions">Configuration options for the slave agent (e.g., from appsettings.json), wrapped in <see cref="IOptions{SlaveConfig}"/>.</param>
        /// <param name="appLifetime">The application lifetime service, used to handle application start and stop events for graceful shutdown.</param>
        /// <param name="executiveCodeExecutor">The executor responsible for running the actual task scripts or commands, injected by DI.</param>
        /// <remarks>
        /// The constructor initializes readonly fields and sets up core components:
        /// - The <see cref="SlaveCommandsHandler"/> is instantiated, receiving callbacks (<see cref="SendSlaveTaskUpdateAsync"/> and <see cref="SendTaskReadinessReportAsync"/>) 
        ///   that allow it to communicate task progress and readiness reports back to the Master Agent via this service.
        /// - A <see cref="SemaphoreSlim"/> is configured based on <see cref="SlaveConfig.MaxConcurrentTasks"/> to limit the number of simultaneously executing tasks.
        /// Critical dependencies like logger, config, appLifetime, and executiveCodeExecutor are validated for nullity.
        /// </remarks>
        public SlaveAgentService(
            ILogger<SlaveAgentService> logger,
            IOptions<SlaveConfig> configOptions,
            IHostApplicationLifetime appLifetime,
            IServiceProvider serviceProvider
            )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = configOptions?.Value ?? throw new ArgumentNullException(nameof(configOptions));
            _appLifetime = appLifetime ?? throw new ArgumentNullException(nameof(appLifetime));

            // --- Service Locator pattern is used here to resolve DI-managed handlers ---
            // This is a pragmatic choice to keep AgentCommandsHandler's construction simple,
            // as it's tightly coupled to the state (callbacks) of this singleton service.
            // The handlers themselves are fully managed by the DI container.
            var slaveTaskHandlers = serviceProvider.GetRequiredService<IEnumerable<ISlaveTaskHandler>>();

            _slaveCommandsHandler = new SlaveCommandsHandler(
                _config.AgentName,
                SendSlaveTaskUpdateAsync,
                SendTaskReadinessReportAsync,
                slaveTaskHandlers      // Pass the collection of handlers
            );

            _concurrentTaskSemaphore = new SemaphoreSlim(_config.MaxConcurrentTasks, _config.MaxConcurrentTasks);
            InitializePerformanceCounters();
            _logger.LogInformation($"Slave Agent Service initialized for Node: '{_config.AgentName}'. Max concurrent tasks: {_config.MaxConcurrentTasks}.");
        }

        private void InitializePerformanceCounters()
        {
            try
            {
                if (OperatingSystem.IsWindows()) // PerformanceCounters are Windows-specific
                {
                    _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    _cpuCounter.NextValue(); // Initial call to prime the counter

                    _memoryCounter = new PerformanceCounter("Memory", "Committed Bytes");
                    _memoryCounter.NextValue(); // Initial call

					// get total memory available in bytes
                    using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            _totalMemoryBytes = (ulong)obj["TotalPhysicalMemory"];
                        }
                    }

					_logger.LogInformation("Performance counters for CPU and Memory initialized.");
                }
                else
                {
                    _logger.LogWarning("Performance counters are not initialized (OS is not Windows). CPU and Memory usage will report -1.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize performance counters. CPU and Memory usage will report -1.");
                _cpuCounter?.Dispose();
                _memoryCounter?.Dispose();
                _cpuCounter = null;
                _memoryCounter = null;
            }
        }

        /// <summary>
        /// Triggered when the application host is ready to start the service.
        /// Performs initial validation and starts the connection process to the Master Agent.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that indicates the start process has been aborted by the host.</param>
        /// <remarks>
        /// This method is part of the <see cref="IHostedService"/> interface.
        /// It performs critical pre-start checks such as ensuring <see cref="SlaveConfig.AgentName"/> and <see cref="SlaveConfig.MasterHost"/> are configured.
        /// If validations pass, it sets up the NLog MDLC context with the agent name and then calls <see cref="ConnectToMasterAsync"/>
        /// to establish the SignalR connection. It also registers handlers for application lifecycle events (<see cref="IHostApplicationLifetime.ApplicationStarted"/>, <see cref="IHostApplicationLifetime.ApplicationStopping"/>).
        /// A <see cref="CancellationTokenSource"/>, linked to the provided <paramref name="cancellationToken"/>, is created to manage the service's own stopping process.
        /// </remarks>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SiteKeeper Slave Agent Service starting...");
            _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _appLifetime.ApplicationStarted.Register(OnApplicationStarted);
            _appLifetime.ApplicationStopping.Register(OnApplicationStopping);

            if (string.IsNullOrWhiteSpace(_config.AgentName))
            {
                _logger.LogCritical("AgentName is not configured. Slave agent cannot start.");
                _appLifetime.StopApplication();
                return Task.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(_config.MasterHost))
            {
                _logger.LogCritical("MasterHost is not configured. Slave agent cannot start.");
                _appLifetime.StopApplication();
                return Task.CompletedTask;
            }

            NLog.MappedDiagnosticsLogicalContext.Set(LogMdlcNodeName, _config.AgentName);

			// this should not be awaited, will block the master host startup
			_ = ConnectToMasterAsync(_stoppingCts.Token);


            // Return a completed task so the host can proceed to start other services.
            return Task.CompletedTask;
        }

        /// <summary>
        /// Triggered when the application host is performing a graceful shutdown.
        /// Handles the orderly shutdown of the slave agent, including disconnecting from the master and stopping timers.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that indicates the shutdown process should no longer be graceful (e.g., timeout exceeded).</param>
        /// <remarks>
        /// This method is part of the <see cref="IHostedService"/> interface.
        /// It signals the service's internal <see cref="CancellationTokenSource"/> (<see cref="_stoppingCts"/>) to begin shutdown.
        /// It stops the heartbeat and resource monitoring timers, attempts to gracefully disconnect the SignalR connection
        /// (with a timeout), cancels any active tasks managed by the <see cref="SlaveCommandsHandler"/> (via <see cref="SlaveTaskContext.CancellationTokenSource"/>),
        /// disposes the <see cref="SemaphoreSlim"/>, and finally shuts down NLog to flush logs.
        /// </remarks>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SiteKeeper Slave Agent Service stopping...");
            _stoppingCts?.Cancel();

            _heartbeatTimer?.Change(Timeout.Infinite, 0);
            _resourceMonitorTimer?.Change(Timeout.Infinite, 0);

            if (_masterConnection != null)
            {
                // Clear the hub connection provider for the NLog target
                SiteKeeperMasterBoundTarget.SetHubConnectionProvider(() => null);

                _logger.LogInformation("Disconnecting from Master Agent...");
                using var disconnectCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                try
                {
                    await _masterConnection.StopAsync(disconnectCts.Token);
                    _logger.LogInformation("Successfully disconnected from Master Agent.");
                }
                catch (OperationCanceledException) when (disconnectCts.IsCancellationRequested)
                {
                    _logger.LogWarning("Timeout while stopping SignalR connection to Master.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during SignalR connection stop.");
                }
                await _masterConnection.DisposeAsync();
                _masterConnection = null;
            }

            _logger.LogInformation("Cancelling any active slave tasks...");
            foreach (var taskContext in _activeSlaveTasks.Values)
            {
                try { taskContext.CancellationTokenSource.Cancel(); } catch(ObjectDisposedException) { /* ignore if already disposed */ }
            }

            _concurrentTaskSemaphore.Dispose();
            _stoppingCts?.Dispose();
            NLog.LogManager.Shutdown();
            _logger.LogInformation("SiteKeeper Slave Agent Service stopped.");
        }

        private void OnApplicationStarted()
        {
            _logger.LogInformation("Application has started. SiteKeeper Slave Agent is running.");
        }

        private void OnApplicationStopping()
        {
            _logger.LogInformation("Application is stopping. SiteKeeper Slave Agent will shut down.");
            _stoppingCts?.Cancel();
        }

        private async Task ConnectToMasterAsync(CancellationToken cancellationToken)
        {
            var masterUrl = new UriBuilder(
                _config.UseHttpsForMasterConnection ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
                _config.MasterHost,
                _config.MasterAgentPort,
                "/agenthub"
            ).ToString();

            _logger.LogInformation($"Attempting to connect to Master Agent Hub at: {masterUrl}");

            _masterConnection = new HubConnectionBuilder()
                .WithUrl(masterUrl, options =>
                {
                    if (_config.UseHttpsForMasterConnection)
                    {
                        options.HttpMessageHandlerFactory = (handler) =>
                        {
                            if (handler is HttpClientHandler clientHandler)
                            {
                                clientHandler.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;

                                if (!string.IsNullOrEmpty(_config.ClientCertPath))
                                {
                                    try
                                    {
                                        var clientCert = new X509Certificate2(_config.ClientCertPath, _config.ClientCertPassword);
                                        clientHandler.ClientCertificates.Add(clientCert);
                                        _logger.LogInformation($"Client certificate '{clientCert.Subject}' loaded for Master connection.");
                                    }
                                    catch (Exception ex)
                                {
                                        _logger.LogError(ex, $"Failed to load client certificate from path: '{_config.ClientCertPath}'. Connection will proceed without it if master allows.");
                                    }
                                }
                                
                                clientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) =>
                                    {
                                    if (cert == null) 
                                    { 
                                        _logger.LogWarning("Master server certificate is null during validation."); 
                                        return false; 
                                    }
                                    if (!string.IsNullOrEmpty(_config.MasterCaCertPath))
                                    {
                                        try
                                        {
                                            X509Chain customChain = new X509Chain();
                                            customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                                            customChain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                                            var caCert = new X509Certificate2(_config.MasterCaCertPath);
                                            customChain.ChainPolicy.ExtraStore.Add(caCert);
                                            if (customChain.Build(new X509Certificate2(cert)))
                                            {
                                                _logger.LogInformation($"Master server certificate validated successfully against custom CA: '{_config.MasterCaCertPath}'.");
                                                return true;
                                    }
                                            _logger.LogWarning($"Master server certificate validation against custom CA '{_config.MasterCaCertPath}' FAILED. Chain status: {string.Join("; ", customChain.ChainStatus.Select(s => s.StatusInformation))}");
                                            return false;
                                }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError(ex, $"Error during custom CA validation for master server certificate using CA '{_config.MasterCaCertPath}'.");
                                            return false;
                            }
                                    }
                                    if (sslPolicyErrors != System.Net.Security.SslPolicyErrors.None)
                                    {                                        
                                        _logger.LogWarning($"Master server certificate validation failed with SSL policy errors: {sslPolicyErrors}.");
                                        return false;
                                    }
                                    _logger.LogInformation("Master server certificate validated successfully by default system validation.");
                                    return true;
                                };
                            }
                            return handler;
                        };
                    }
                })
                .WithAutomaticReconnect(new SlaveHubReconnectPolicy(_logger))
                .AddJsonProtocol()
                .Build();

            // --- Register handlers for master-to-slave messages ---

            _masterConnection.On<PrepareForTaskInstruction>("ReceivePrepareForTaskInstructionAsync", async (instruction) =>
            {
                await HandleSignalRInvokeAsync(instruction.ActionId, instruction.TaskId, async () =>
                {
                    await _slaveCommandsHandler.HandlePrepareForTaskAsync(instruction);
                });
            });

            _masterConnection.On<SlaveTaskInstruction>("ReceiveSlaveTaskAsync", async (instruction) =>
            {
                await HandleSignalRInvokeAsync(instruction.ActionId, instruction.TaskId, async () =>
                {
                    await _slaveCommandsHandler.HandleSlaveTaskAsync(instruction, _activeSlaveTasks, _concurrentTaskSemaphore);
                });
            });

            _masterConnection.On<CancelTaskOnAgentRequest>("ReceiveCancelTaskRequestAsync", async (request) =>
            {
                await HandleSignalRInvokeAsync(request.ActionId, request.TaskId, async () =>
                {
                    await _slaveCommandsHandler.HandleTaskCancelRequestAsync(request.ActionId, request.TaskId, _activeSlaveTasks);
                });
            });

            _masterConnection.On<AdjustSystemTimeCommand>("ReceiveAdjustSystemTime", async (command) =>
            {
                _logger.LogInformation("Received AdjustSystemTime command from master. Authoritative time: {MasterTimeUtc}", command.AuthoritativeUtcTimestamp);
                // In a real implementation, this would call a system utility to set the time.
                // For this sketch, we just log it.
                // NOTE: This requires administrative privileges.
                // Example: SystemTime.Set(command.AuthoritativeUtcTimestamp);
            });


            _masterConnection.On<string>("RequestLogFlushForTask", async (operationId) =>
            {
                await HandleSignalRInvokeAsync(operationId, null, async () =>
                {
                    _logger.LogInformation("Received log flush request from master for OperationId: {OperationId}", operationId);
 
                    var masterLoggingTarget = NLog.LogManager.Configuration?.FindTargetByName<SiteKeeperMasterBoundTarget>("masterBoundTarget");
                    
                     if (masterLoggingTarget != null)
                    {
                        await masterLoggingTarget.FlushAsync();
                        _logger.LogInformation("Log flush completed for OperationId: {OperationId}", operationId);
                    }
                    else
                    {
                        _logger.LogWarning("Cannot flush logs; 'masterBoundTarget' NLog target not found.");
                    }

                    if (_masterConnection != null)
                    {
                        await _masterConnection.InvokeAsync("ConfirmLogFlushForTask", operationId, _config.AgentName, cancellationToken);
                        _logger.LogInformation("Confirmed log flush to master for OperationId: {OperationId}", operationId);
                    }
                });
            });

            _masterConnection.On<NodeGeneralCommandRequest>("SendGeneralCommandAsync", async (request) =>
            {
                _logger.LogInformation("Received General Command: {Command}", request.CommandType);
                // Actual command handling logic would go here.
                await Task.CompletedTask;
            });

            _masterConnection.On<MasterStateForAgent>("UpdateMasterStateAsync", (state) =>
            {
                var opId = state.AssignedOrRelevantOperations?.FirstOrDefault()?.OperationId ?? "None";
                _logger.LogInformation("Received master state update. First relevant operation: {OperationId}", opId);
            });

            _masterConnection.Reconnecting += (error) =>
            {
                _logger.LogWarning(error, "SignalR connection to master is reconnecting...");
                StopTimers();
                // Clear the hub connection provider for the NLog target while disconnected
                SiteKeeperMasterBoundTarget.SetHubConnectionProvider(() => null);
                return Task.CompletedTask;
            };

            _masterConnection.Reconnected += async (connectionId) =>
            {
                _logger.LogInformation("SignalR connection re-established with new ConnectionId: {ConnectionId}. Re-registering with master...", connectionId);
                await RegisterWithMasterAsync(cancellationToken);
                // Re-establish the provider for the NLog target
                SiteKeeperMasterBoundTarget.SetHubConnectionProvider(() => _masterConnection);
                StartTimers();
            };

            _masterConnection.Closed += (error) =>
            {
                _logger.LogError(error, "SignalR connection to master was closed. Will not reconnect automatically. Service will try to restart connection logic if not shutting down.");
                StopTimers();
                // Clear the hub connection provider for the NLog target
                SiteKeeperMasterBoundTarget.SetHubConnectionProvider(() => null);
                if (!_stoppingCts.IsCancellationRequested)
                {
                    // Attempt to reconnect after a delay, unless the application is stopping.
                    Task.Delay(TimeSpan.FromSeconds(15), cancellationToken).ContinueWith(async t =>
                    {
                        if (!t.IsCanceled)
                        {
                            await ConnectToMasterAsync(cancellationToken);
                        }
                    }, cancellationToken);
                }
                return Task.CompletedTask;
            };

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Connecting to master...");
                    await _masterConnection.StartAsync(cancellationToken);
                    _logger.LogInformation("Successfully connected to master with ConnectionId: {ConnectionId}", _masterConnection.ConnectionId);

                    // Provide the NLog target with a way to get the active connection
                    SiteKeeperMasterBoundTarget.SetHubConnectionProvider(() => _masterConnection);

                    await RegisterWithMasterAsync(cancellationToken);
                    StartTimers();
                    return; // Exit the loop on successful connection
                }
                catch (AuthenticationException ex)
                {
                    _logger.LogCritical(ex, "Authentication failed while connecting to master. This may be due to an invalid or missing client certificate. The slave will not retry and will shut down.");
                    _appLifetime.StopApplication();
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to connect to master. Retrying in {RetrySeconds} seconds...", _config.MasterConnectionRetryIntervalSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(_config.MasterConnectionRetryIntervalSeconds), cancellationToken);
                }
            }
        }

        private void StartTimers()
        {
            _logger.LogInformation("Starting periodic timers (heartbeat, resource monitor).");
            if (_config.HeartbeatIntervalSeconds > 0)
            {
                _heartbeatTimer?.Dispose();
                _heartbeatTimer = new Timer(async _ => await SendHeartbeatAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(_config.HeartbeatIntervalSeconds));
                _logger.LogInformation($"Heartbeat timer started with interval: {_config.HeartbeatIntervalSeconds} seconds.");
            }
            else
            {
                 _logger.LogWarning("Heartbeat interval is not configured or is zero. Heartbeats will not be sent.");
            }

            if (_config.ResourceMonitorIntervalSeconds > 0)
            {
                _resourceMonitorTimer?.Dispose();
                _resourceMonitorTimer = new Timer(async _ => await SendResourceUsageAsync(), null, TimeSpan.FromSeconds(_config.ResourceMonitorIntervalSeconds), TimeSpan.FromSeconds(_config.ResourceMonitorIntervalSeconds));
                _logger.LogInformation($"Resource monitor timer started with interval: {_config.ResourceMonitorIntervalSeconds} seconds.");
            }
             else
            {
                 _logger.LogWarning("Resource monitor interval is not configured or is zero. Resource usage will not be sent periodically.");
            }
        }

        private void StopTimers()
        {
            _logger.LogInformation("Stopping periodic timers (heartbeat, resource monitor).");
            _heartbeatTimer?.Change(Timeout.Infinite, 0);
            _resourceMonitorTimer?.Change(Timeout.Infinite, 0);
        }

        private async Task SendHeartbeatAsync()
        {
            if (_masterConnection is null || _masterConnection.State != HubConnectionState.Connected)
                return;

            try
            {
                var heartbeat = new SlaveHeartbeat
                {
                    NodeName = _config.AgentName,
                    Timestamp = DateTimeOffset.UtcNow,
                    ActiveTasks = _activeSlaveTasks.Count,
                    AvailableTaskSlots = _concurrentTaskSemaphore.CurrentCount,

                    // Populate the new fields by calling the resource monitoring helpers.
                    CpuUsagePercent = GetCurrentCpuUsage(),
                    RamUsagePercent = GetCurrentRamUsagePercentage()
                };

                // Send the heartbeat to the master. The CancellationToken ensures that if the service is
                // stopping, we don't attempt to send a final heartbeat on a disconnected line.
                await _masterConnection.InvokeAsync("SendHeartbeatAsync", heartbeat, _stoppingCts?.Token ?? default);
            }
            catch (Exception ex)
            {
                if (_stoppingCts?.IsCancellationRequested ?? false)
                {
                    // If stopping, it's expected that the connection might close, so log as info.
                    _logger.LogInformation("Could not send heartbeat, service is stopping.");
                }
                else
                {
                    _logger.LogError(ex, "Failed to send heartbeat to Master Agent.");
                }
            }
        }
        
        private async Task SendResourceUsageAsync()
        {
            if (_masterConnection is null || _masterConnection.State != HubConnectionState.Connected || (_stoppingCts?.IsCancellationRequested ?? false))
                return;

            var resourceUsage = new SlaveResourceUsage
            {
                NodeName = _config.AgentName,
                Timestamp = DateTimeOffset.UtcNow,
                CpuUsagePercentage = GetCurrentCpuUsage(),
                MemoryUsageBytes = GetCurrentMemoryUsage(),
                AvailableDiskSpaceMb = GetAvailableDiskSpaceMb(_config.MonitoredDriveForDiskSpace)
            };

            try
            {
                await _masterConnection.InvokeAsync("SendResourceUsageAsync", resourceUsage, _stoppingCts?.Token ?? default);
            }
            catch (Exception ex)
            {
                if (!(_stoppingCts?.IsCancellationRequested ?? false))
                {
                    _logger.LogError(ex, "Failed to send resource usage to Master Agent.");
                }
            }
        }

        private async Task SendSlaveTaskUpdateAsync(SlaveTaskProgressUpdate taskUpdateDto)
        {
            if (_masterConnection?.State == HubConnectionState.Connected)
            {
                try
                {
                    NLog.MappedDiagnosticsLogicalContext.Set(LogMdlcActionId, taskUpdateDto.ActionId);
                    NLog.MappedDiagnosticsLogicalContext.Set(LogMdlcTaskId, taskUpdateDto.TaskId);
                    await _masterConnection.InvokeAsync("ReportOngoingTaskProgressAsync", taskUpdateDto, _stoppingCts?.Token ?? CancellationToken.None);
                    _logger.LogDebug($"Sent task progress update for OpId: {taskUpdateDto.ActionId}, TaskId: {taskUpdateDto.TaskId}, Status: {taskUpdateDto.Status}.");
                }
                catch (OperationCanceledException) when (_stoppingCts?.IsCancellationRequested ?? false)
                {
                     _logger.LogInformation($"Sending task update for TaskId: {taskUpdateDto.TaskId} was canceled during service shutdown.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to send task progress update for OpId: {taskUpdateDto.ActionId}, TaskId: {taskUpdateDto.TaskId}.");
                }
                finally
                {
                    NLog.MappedDiagnosticsLogicalContext.Remove(LogMdlcActionId);
                    NLog.MappedDiagnosticsLogicalContext.Remove(LogMdlcTaskId);
                }
            }
            else
            {
                _logger.LogWarning($"Cannot send task progress update for OpId: {taskUpdateDto.ActionId}, TaskId: {taskUpdateDto.TaskId}. No active connection to Master.");
            }
        }

        public async Task SendTaskReadinessReportAsync(SlaveTaskReadinessReport readinessReportDto)
        {
            if (_masterConnection?.State == HubConnectionState.Connected)
            {
                try
                {
                    NLog.MappedDiagnosticsLogicalContext.Set(LogMdlcActionId, readinessReportDto.ActionId);
                    NLog.MappedDiagnosticsLogicalContext.Set(LogMdlcTaskId, readinessReportDto.TaskId);
                    await _masterConnection.InvokeAsync("ReportSlaveTaskReadinessAsync", readinessReportDto, _stoppingCts?.Token ?? CancellationToken.None);
                    _logger.LogInformation($"Successfully sent readiness report for OpId: {readinessReportDto.ActionId}, TaskId: {readinessReportDto.TaskId}, Node: '{readinessReportDto.NodeName}', IsReady: {readinessReportDto.IsReady}.");
                }
                catch (OperationCanceledException) when (_stoppingCts?.IsCancellationRequested ?? false)
                {
                     _logger.LogInformation($"Sending readiness report for TaskId: {readinessReportDto.TaskId} was canceled during service shutdown.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to send readiness report for OpId: {readinessReportDto.ActionId}, TaskId: {readinessReportDto.TaskId}. Error: {ex.Message}");
                }
                 finally
                {
                    NLog.MappedDiagnosticsLogicalContext.Remove(LogMdlcActionId);
                    NLog.MappedDiagnosticsLogicalContext.Remove(LogMdlcTaskId);
                }
            }
            else
            {
                _logger.LogWarning($"Cannot send readiness report for OpId: {readinessReportDto.ActionId}, TaskId: {readinessReportDto.TaskId}. No active connection to Master.");
            }
        }

        private async Task HandleSignalRInvokeAsync(string? operationId, string? taskId, Func<Task> handlerAction)
        {
            bool opIdSetInMdlc = false;
            bool taskIdSetInMdlc = false;

            try
            {
                if (!string.IsNullOrEmpty(operationId))
                {
                    NLog.MappedDiagnosticsLogicalContext.Set(LogMdlcActionId, operationId);
                    opIdSetInMdlc = true;
                }
                if (!string.IsNullOrEmpty(taskId))
                {
                    NLog.MappedDiagnosticsLogicalContext.Set(LogMdlcTaskId, taskId);
                    taskIdSetInMdlc = true;
                }

                _logger.LogDebug($"Handling master instruction. OpId: {operationId ?? "N/A"}, TaskId: {taskId ?? "N/A"}.");
                await handlerAction();
                _logger.LogDebug($"Successfully handled master instruction. OpId: {operationId ?? "N/A"}, TaskId: {taskId ?? "N/A"}.");
            }
            catch (OperationCanceledException opEx) when (_stoppingCts?.IsCancellationRequested ?? false)
            {
                 _logger.LogInformation(opEx, $"Handler for master instruction (OpId: {operationId ?? "N/A"}, TaskId: {taskId ?? "N/A"}) was canceled due to service stopping.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling master instruction. OpId: {operationId ?? "N/A"}, TaskId: {taskId ?? "N/A"}.");
            }
            finally
            {
                if (opIdSetInMdlc) NLog.MappedDiagnosticsLogicalContext.Remove(LogMdlcActionId);
                if (taskIdSetInMdlc) NLog.MappedDiagnosticsLogicalContext.Remove(LogMdlcTaskId);
            }
        }
        
        private double GetCurrentCpuUsage() 
        {
            if (_cpuCounter == null)
            {
                // _logger.LogTrace("CPU counter not available, reporting placeholder value."); // Already logged during init
                return -1.0;
            }
            try
            {
                return _cpuCounter.NextValue();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get CPU usage from performance counter. Reporting -1.");
                return -1.0;
            }
        }

        private long GetCurrentMemoryUsage()
        {
            if (_memoryCounter == null)
            {
                return -1L;
            }
            try
            {
                return (long)_memoryCounter.NextValue();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Memory usage (Committed Bytes) from performance counter. Reporting -1L.");
                return -1L;
            }
        }

        /// <summary>
        /// Calculates the current RAM usage as a percentage of total physical memory.
        /// </summary>
        /// <returns>RAM usage percentage (0-100), or -1 if unable to calculate.</returns>
        private double GetCurrentRamUsagePercentage()
        {
            if (_memoryCounter == null || _totalMemoryBytes == 0)
            {
                return -1;
            }

            var usedMemoryBytes = _memoryCounter.NextValue();
            if (usedMemoryBytes <= 0)
            {
                return 0;
            }

            // Calculate the percentage of used memory against the total physical memory.
            return ((double)usedMemoryBytes / _totalMemoryBytes) * 100.0;
        }

        private long GetAvailableDiskSpaceMb(string driveName) 
        {
            if (string.IsNullOrWhiteSpace(driveName))
            {
                _logger.LogWarning("Drive name for disk space check is null or empty. Reporting -1L.");
                return -1L;
            }

            try
            {
                // Ensure driveName is just the root (e.g. "C:\\") for DriveInfo
                // Or a letter like "C" which DriveInfo can handle.
                string rootPath = driveName;
                if (driveName.Length == 1 && char.IsLetter(driveName[0]))
                {
                    rootPath = driveName + ":\\"; // DriveInfo constructor prefers "C:" or "C:\\"
                }
                else if (!driveName.Contains(Path.VolumeSeparatorChar)) // If it's not like "C:" or "C:\something"
                {
                     _logger.LogWarning($"Drive name '{driveName}' does not appear to be a valid drive specifier for DriveInfo. Attempting anyway.");
                }


                DriveInfo driveInfo = new DriveInfo(rootPath);
                return driveInfo.AvailableFreeSpace / (1024 * 1024); // Convert bytes to MB
            }
            catch (ArgumentException ex) // Handles invalid drive letters/formats
            {
                 _logger.LogError(ex, $"Invalid drive name '{driveName}' for disk space check. Reporting -1L.");
                return -1L;
            }
            catch (Exception ex) // Handles other potential issues like drive not ready, etc.
            {
                _logger.LogError(ex, $"Failed to get free disk space for drive '{driveName}'. Reporting -1L.");
                return -1L;
            }
        }

        private async Task RegisterWithMasterAsync(CancellationToken cancellationToken)
        {
            if (_masterConnection?.State != HubConnectionState.Connected)
            {
                _logger.LogWarning("Cannot register with master: No active connection or connection not in Connected state.");
                return;
            }

            var registrationRequest = new SlaveRegistrationRequest
            {
                AgentName = _config.AgentName,
                AgentVersion = GetType().Assembly.GetName().Version?.ToString() ?? "0.0.0.0", 
                OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                FrameworkDescription = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                MaxConcurrentTasks = _config.MaxConcurrentTasks,
                Hostname = Dns.GetHostName()                
            };
            _logger.LogInformation($"Attempting to register slave agent '{registrationRequest.AgentName}' (Version: {registrationRequest.AgentVersion}) with master...");

            try
            {
                await _masterConnection.InvokeAsync("RegisterSlaveAsync", registrationRequest, cancellationToken);
                _logger.LogInformation($"Slave agent '{_config.AgentName}' registered successfully with master.");
                StartTimers(); 
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                 _logger.LogInformation("Slave registration was canceled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to register slave agent '{_config.AgentName}' with master. Will retry on next reconnect or if forced.");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _logger.LogInformation("Disposing SlaveAgentService resources...");
                _heartbeatTimer?.Dispose();
                _resourceMonitorTimer?.Dispose();
                _cpuCounter?.Dispose();
                _memoryCounter?.Dispose();
                _masterConnection?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _concurrentTaskSemaphore?.Dispose();
                _stoppingCts?.Dispose();
                 _logger.LogInformation("SlaveAgentService resources disposed.");
            }
        }
    }

    public class SlaveHubReconnectPolicy : IRetryPolicy
    {
        private readonly ILogger _logger;

        public SlaveHubReconnectPolicy(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            long previousRetryCount = retryContext.PreviousRetryCount;
            TimeSpan delay;

            if (previousRetryCount == 0) delay = TimeSpan.FromSeconds(1);
            else if (previousRetryCount == 1) delay = TimeSpan.FromSeconds(2);
            else if (previousRetryCount == 2) delay = TimeSpan.FromSeconds(5);
            else if (previousRetryCount < 6) delay = TimeSpan.FromSeconds(10);
            else if (previousRetryCount < 12) delay = TimeSpan.FromSeconds(30);
            else delay = TimeSpan.FromMinutes(1);
            
            _logger.LogWarning($"SignalR connection retry attempt #{previousRetryCount + 1}. Waiting {delay.TotalSeconds}s before next attempt. Reason: {retryContext.RetryReason?.Message}");
            return delay;
        }
    }
} 