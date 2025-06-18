using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Web;
using SiteKeeper.Master;
using SiteKeeper.Slave;
using SiteKeeper.Slave.Configuration;
using SiteKeeper.Slave.Abstractions;
using SiteKeeper.Slave.Services;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SiteKeeper.Slave.Services.NLog2;
using SiteKeeper.Master.Services; // For Master's services
using SiteKeeper.Master.Hubs;
using SiteKeeper.Master.Abstractions.Services;
using SiteKeeper.Master.Services.Placeholders;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using SiteKeeper.Master.Web.Apis; // For API endpoints mapping
using Microsoft.OpenApi.Models;
using System.Collections.Generic;
using System;
using SiteKeeper.Shared.DTOs.API.Authentication;
using Microsoft.Extensions.Options;
using System.Net.NetworkInformation;
using NLog.Fluent;
using OperatingSystem = System.OperatingSystem;
using Scrutor;
using SiteKeeper.Master.Abstractions.Workflow;
using SiteKeeper.Master.Workflow.StageHandlers;

namespace SiteKeeper.ConsoleHost
{
    /// <summary>
    /// The main entry point for the SiteKeeper application.
    /// This console host can run the Master components, Slave components, or both,
    /// depending on the "SiteKeeperMode" configuration.
    /// It handles application setup, configuration, logging, and service execution.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Static NLog logger instance for use within the Program class, primarily for early startup logging.
        /// </summary>
        private static NLog.Logger? _logger;

        /// <summary>
        /// The main entry point of the SiteKeeper application.
        /// </summary>
        /// <param name="args">Command-line arguments passed to the application.</param>
        /// <remarks>
        /// This method orchestrates the application startup by:
        /// 1. Determining the content root path, adjusting for Windows Service context if necessary.
        /// 2. Creating the <see cref="WebApplicationBuilder"/> using <see cref="CreateAppBuilder"/>, which configures services, logging, and application settings.
        /// 3. Building the <see cref="WebApplication"/> instance from the builder.
        /// 4. Configuring the HTTP request pipeline for Master components (if enabled) using <see cref="WebApplicationExtensions.ConfigureSiteKeeperPipeline"/>.
        /// 5. Running the application, handling differences between console execution and Windows Service execution.
        /// It includes global exception handling to log critical errors and ensures NLog shutdown.
        /// </remarks>
        public static async Task Main(string[] args)
        {
            var pathToContentRoot = Directory.GetCurrentDirectory();
            if (IsRunningAsWindowsService() || args.Contains("--service"))
            {
                pathToContentRoot = AppContext.BaseDirectory;
            }

            // The builder now directly creates a WebApplication, which is both an IHost and a web server.
            var builder = CreateAppBuilder(args, pathToContentRoot);

            // Build the single, unified host.
            var app = builder.Build();

            app.ConfigureSiteKeeperPipeline();

            try
            {
                // The existing logic for running as a service or console app remains unchanged
                // and works perfectly with WebApplication.
                if (IsRunningAsWindowsService() || args.Contains("--service"))
                {
                    await app.RunAsync();
                }
                else
                {
                    Console.WriteLine("Running as console application.");
                    Console.WriteLine($"Content Root: {pathToContentRoot}");
                    _logger?.Info("Console Host starting up...");
                    await app.RunAsync(); // Use RunAsync for console as well for consistent shutdown behavior.
                }
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Stopped program because of exception");
                Console.WriteLine("Stopped program because of exception: " + ex.ToString());
                throw;
            }
            finally
            {
                NLog.LogManager.Shutdown();
            }
        }

        /// <summary>
        /// Creates and configures the single, unified WebApplication host.
        /// This method replaces the old CreateHostBuilder and consolidates all service
        /// and host configuration from Program.cs, MasterStartupService.cs, and MasterAppHost.cs.
        /// </summary>
        /// <param name="args">Command-line arguments passed to the application. Used for configuration sources.</param>
        /// <param name="contentRoot">The application's content root path. Used for resolving configuration files and other assets.</param>
        /// <returns>A configured <see cref="WebApplicationBuilder"/> instance ready to build the <see cref="WebApplication"/>.</returns>
        /// <remarks>
        /// The method performs comprehensive setup:
        /// <list type="number">
        ///   <item><description>Initializes <see cref="WebApplication.CreateBuilder"/> with options.</description></item>
        ///   <item><description>Configures application settings by loading from JSON files (appsettings.json, environment-specific), environment variables, and command-line arguments.</description></item>
        ///   <item><description>Sets up NLog as the logging provider.</description></item>
        ///   <item><description>Determines the operational mode ("All", "MasterOnly", "SlaveOnly") from configuration ("SiteKeeperMode").</description></item>
        ///   <item><description>Conditionally configures Slave components if Slave mode is enabled:
        ///     Registers <see cref="SlaveConfig"/>, <see cref="IExecutiveCodeExecutor"/> (with <see cref="SimulatedExecutiveCodeExecutor"/>),
        ///     <see cref="SlaveAgentService"/> (as IHostedService), and <see cref="NLogTargetAssuranceService"/> (as IHostedService).
        ///   </description></item>
        ///   <item><description>Conditionally configures Master web host components if Master mode is enabled:
        ///     Binds and registers <see cref="MasterConfig"/>.
        ///     Configures Kestrel for dual ports (GUI and Agent) as specified in <see cref="MasterConfig"/>, including HTTPS setup
        ///     with server certificate loading and optional client certificate validation for the Agent port.
        ///     Registers Master services for Dependency Injection: core services (Journal, GUI Notifier, Agent Connection Manager, Node Health Monitor),
        ///     placeholder services for various interfaces, <see cref="MasterNLogSetupService"/>, <see cref="MultiNodeOperationStageHandler"/>,
        ///     <see cref="IMasterActionCoordinatorService"/> (with <see cref="MasterActionCoordinatorService"/>), and scans for <see cref="IMasterActionHandler"/> implementations.
        ///     Configures SignalR with JSON enum conversion.
        ///     Configures Swagger/OpenAPI for API documentation.
        ///     Configures JWT Bearer authentication and authorization.
        ///   </description></item>
        ///   <item><description>Enables running as a Windows Service using <see cref="Host.UseWindowsService()"/>.</description></item>
        /// </list>
        /// </remarks>
        public static WebApplicationBuilder CreateAppBuilder(string[] args, string contentRoot)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args ?? Array.Empty<string>(),
                ContentRootPath = contentRoot
            });

            // 1. CONFIGURE APP CONFIGURATION (Unchanged from original Program.cs)
            builder.Configuration.SetBasePath(contentRoot);
            builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
            builder.Configuration.AddEnvironmentVariables();
            if (args != null)
            {
                builder.Configuration.AddCommandLine(args);
            }

            // 2. CONFIGURE LOGGING (Unchanged from original Program.cs)
            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(LogLevel.Trace);
            builder.Host.UseNLog();

            _logger = NLogBuilder.ConfigureNLog("nlog.config").GetCurrentClassLogger();

            // 3. DETERMINE OPERATIONAL MODE
            string? siteKeeperMode = builder.Configuration.GetValue<string>("SiteKeeperMode");
            _logger.Info($"SiteKeeperMode from configuration: {siteKeeperMode}");

            bool runMaster = siteKeeperMode is "All" or "MasterOnly";
            bool runSlave = siteKeeperMode is "All" or "SlaveOnly";

            // 4. CONDITIONALLY CONFIGURE SERVICES for Slave and Master

            // --- Configure Slave Components (if enabled) ---
            // This logic is moved from the old CreateHostBuilder.
            if (runSlave)
            {
                _logger.Info("Slave Agent components will be configured and started as a Hosted Service.");
                builder.Services.Configure<SlaveConfig>(builder.Configuration.GetSection("SlaveConfig"));
                builder.Services.AddSingleton<IExecutiveCodeExecutor, SimulatedExecutiveCodeExecutor>();
                builder.Services.AddHostedService<SlaveAgentService>();
                builder.Services.AddHostedService<NLogTargetAssuranceService>();
            }

            // --- Configure Master Web Host Components (if enabled) ---
            // This block merges all configuration from the deleted MasterAppHost.cs.
            if (runMaster)
            {
                _logger.Info("Master Agent components will be configured.");
                var masterConfigSection = builder.Configuration.GetSection("MasterConfig");
                if (!masterConfigSection.Exists())
                {
                    var exMessage = "'MasterConfig' section not found in configuration. Master cannot start.";
                    _logger.Fatal(exMessage);
                    throw new InvalidOperationException(exMessage);
                }

                // Bind MasterConfig and add it as a singleton for easy access.
                builder.Services.Configure<MasterConfig>(masterConfigSection);
                var masterConfig = masterConfigSection.Get<MasterConfig>()!;
                builder.Services.AddSingleton(masterConfig);

                // --- Configure Kestrel for Dual Ports (from MasterAppHost.cs) ---
                _logger.Info($"Configuring Kestrel. GUI Port: {masterConfig.GuiPort}, Agent Port: {masterConfig.AgentPort}, Use HTTPS: {masterConfig.UseHttps}");
                builder.WebHost.ConfigureKestrel(serverOptions =>
                {
                    // GUI Port Configuration
                    serverOptions.Listen(IPAddress.Any, masterConfig.GuiPort, listenOptions =>
                    {
                        _logger.Info($"Configuring GUI listener on port {masterConfig.GuiPort}");
                        if (masterConfig.UseHttps && !string.IsNullOrEmpty(masterConfig.MasterCertPath))
                        {
                            try
                            {
                                _logger.Debug($"Attempting to load server certificate for GUI port from: {Path.GetFullPath(masterConfig.MasterCertPath)}");
                                listenOptions.UseHttps(masterConfig.MasterCertPath, masterConfig.MasterCertPassword);
                                _logger.Info($"GUI port {masterConfig.GuiPort} configured for HTTPS.");
                            }
                            catch (Exception ex)
                            {
                                _logger.Error(ex, $"Failed to configure HTTPS for GUI port {masterConfig.GuiPort}. Check certificate path ('{masterConfig.MasterCertPath}') and password. Ensure the certificate file exists and is accessible.");
                            }
                        }
                        else
                        {
                            _logger.Info($"GUI port {masterConfig.GuiPort} configured for HTTP.");
                        }
                    });

                    // Agent Port Configuration
                    serverOptions.Listen(IPAddress.Any, masterConfig.AgentPort, listenOptions =>
                    {
                        _logger.Info($"Configuring Agent listener on port {masterConfig.AgentPort}");
                        if (masterConfig.UseHttps && !string.IsNullOrEmpty(masterConfig.MasterCertPath))
                        {
                            try
                            {
                                _logger.Debug($"Attempting to load server certificate for Agent port from: {Path.GetFullPath(masterConfig.MasterCertPath)}");
                                var httpsConnectionAdapterOptions = new HttpsConnectionAdapterOptions
                                {
                                    ServerCertificate = new X509Certificate2(masterConfig.MasterCertPath, masterConfig.MasterCertPassword)
                                };

                                if (!string.IsNullOrEmpty(masterConfig.MasterCaCertPath))
                                {
                                    _logger.Info("Client certificate authentication WILL BE REQUIRED for Agent port based on MasterCaCertPath configuration.");
                                    httpsConnectionAdapterOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                                    httpsConnectionAdapterOptions.ClientCertificateValidation = (clientCert, chain, sslPolicyErrors) =>
                                    {
                                        if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None) return true; // Valid OS-level chain

                                        _logger.Warn($"Client certificate '{clientCert.Subject}' for Agent port has SSL policy errors: {sslPolicyErrors}. Attempting CA validation using '{masterConfig.MasterCaCertPath}'.");
                                        try
                                        {
                                            var caCert = new X509Certificate2(masterConfig.MasterCaCertPath);
                                            X509Chain customChain = new X509Chain();
                                            customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                                            customChain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                                            customChain.ChainPolicy.ExtraStore.Add(caCert);

                                            if (customChain.Build(clientCert))
                                            {
                                                var isValidChain = customChain.ChainElements.Cast<X509ChainElement>()
                                                    .Any(element => element.Certificate.Thumbprint == caCert.Thumbprint);
                                                if (isValidChain)
                                                {
                                                    _logger.Info($"Client certificate '{clientCert.Subject}' validated successfully against CA '{caCert.Subject}'.");
                                                    return true;
                                                }
                                            }
                                            _logger.Error($"Client certificate '{clientCert.Subject}' FAILED validation against CA '{caCert.Subject}'. Chain status: {string.Join("; ", customChain.ChainStatus.Select(s => $"{s.Status}: {s.StatusInformation}"))}");
                                            return false;
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.Error(ex, $"Error during client certificate CA validation for '{clientCert.Subject}'.");
                                            return false;
                                        }
                                    };
                                }
                                else
                                {
                                    _logger.Info("Client certificate authentication NOT configured for Agent port (MasterCaCertPath not set). Mode: NoCertificate.");
                                    httpsConnectionAdapterOptions.ClientCertificateMode = ClientCertificateMode.NoCertificate;
                                }
                                listenOptions.UseHttps(httpsConnectionAdapterOptions);
                                _logger.Info($"Agent port {masterConfig.AgentPort} configured for HTTPS.");
                            }
                            catch (Exception ex)
                            {
                                _logger.Error(ex, $"Failed to configure HTTPS for Agent port {masterConfig.AgentPort}. Check certificate paths ('{masterConfig.MasterCertPath}', '{masterConfig.MasterCaCertPath}') and passwords. Ensure files exist and are accessible.");
                            }
                        }
                        else
                        {
                            _logger.Info($"Agent port {masterConfig.AgentPort} configured for HTTP.");
                        }
                    });
                });

                // --- Configure Master Services for DI (from MasterAppHost.cs) ---
                // All master services are now added to the single, unified DI container.
                _logger.Debug("Configuring Master services for DI...");

                // Register the NLog setup service, which provides the UI logging target.
                builder.Services.AddSingleton<MasterNLogSetupService>().AddHostedService(p => p.GetRequiredService<MasterNLogSetupService>());

                // Register the specialized stage handler for multi-node operations.
                // 1. Register the concrete class as a singleton. This is required by AgentHub, which injects
                //    the concrete type to access its public methods not defined on the interface.
                builder.Services.AddSingleton<MultiNodeOperationStageHandler>();
                
                // 2. Register the IStageHandler interface to resolve to the same singleton instance.
                //    This ensures that any service asking for the interface (like our action handlers)
                //    and any service asking for the concrete class get the *exact same instance*,
                //    preserving its state (e.g., the _activeOperations dictionary).
                builder.Services.AddSingleton<IStageHandler<MultiNodeOperationInput, MultiNodeOperationResult>>(sp =>
                    sp.GetRequiredService<MultiNodeOperationStageHandler>());
                
                builder.Services.AddSingleton<IJournalService, JournalService>();
                builder.Services.AddSingleton<IGuiNotifierService, GuiNotifierService>();
                builder.Services.AddSingleton<IAgentConnectionManagerService, AgentConnectionManagerService>();
                builder.Services.AddSingleton<INodeHealthMonitorService, NodeHealthMonitorService>();
                builder.Services.AddHostedService<MasterLifecycleNotifierService>();

                // == START: Placeholder Service Registrations ==
                // Registering placeholder implementations for development and testing.
                // In a production environment, these would be replaced with concrete implementations.
                builder.Services.AddSingleton<IAuthenticationService, PlaceholderAuthenticationService>();
                builder.Services.AddSingleton<IAuditLogService, PlaceholderAuditLogService>();
                builder.Services.AddSingleton<IAppControlService, PlaceholderAppControlService>();
                builder.Services.AddSingleton<IDiagnosticsService, PlaceholderDiagnosticsService>();
                builder.Services.AddSingleton<IEnvironmentService, PlaceholderEnvironmentService>();
                builder.Services.AddSingleton<INodeService, PlaceholderNodeService>();
                builder.Services.AddSingleton<IOfflineUpdateService, PlaceholderOfflineUpdateService>();
                builder.Services.AddSingleton<IPackageService, PlaceholderPackageService>();
                builder.Services.AddSingleton<IPlanControlService, PlaceholderPlanControlService>();
                builder.Services.AddSingleton<IReleaseService, PlaceholderReleaseService>();
                builder.Services.AddSingleton<IUserService, PlaceholderUserService>();

				// FIXME: uncomment when these service implementations are available.
				//builder.Services.AddSingleton<IBackupService, PlaceholderBackupService>();
				//builder.Services.AddSingleton<ISystemSoftwareService, PlaceholderSystemSoftwareService>();

				// == END: Placeholder Service Registrations ==

				builder.Services.AddHttpContextAccessor();

                // Configure JSON options for consistent enum serialization.
                builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
                builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

                // Configure SignalR with JSON options.
                builder.Services.AddSignalR().AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

                // Configure Swagger for API documentation.
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SiteKeeper Master API", Version = "v1" });
                    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                    {
                        Description = "JWT Authorization header using the Bearer scheme.",
                        Name = "Authorization",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.ApiKey,
                        Scheme = "Bearer"
                    });
                    c.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                },
                                Scheme = "oauth2",
                                Name = "Bearer",
                                In = ParameterLocation.Header,

                            },
                            new List<string>()
                        }
                    });
                });

                // Configure JWT Authentication.
                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,
                            ValidIssuer = masterConfig.JwtIssuer,
                            ValidAudience = masterConfig.JwtAudience,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(masterConfig.JwtSecretKey))
                        };
                    });
                builder.Services.AddAuthorization();

                // Register the new MasterActionCoordinatorService, injecting the real log flush provider.
                builder.Services.AddSingleton<IMasterActionCoordinatorService>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<MasterActionCoordinatorService>>();
                    var nlogSetupService = sp.GetRequiredService<MasterNLogSetupService>();
                    var journalService = sp.GetRequiredService<IJournalService>();
					return new MasterActionCoordinatorService(logger, sp, journalService, nlogSetupService);
                });

                // Automatically register all IMasterActionHandler implementations with a Scoped lifetime.
                // This is the primary mechanism for adding new top-level workflows (Master Actions).
                builder.Services.Scan(scan => scan
                    .FromAssemblyOf<IMasterActionHandler>()
                    .AddClasses(classes => classes.AssignableTo<IMasterActionHandler>())
                    .AsImplementedInterfaces()
                    .WithScopedLifetime());
            }

            if (!runMaster && !runSlave)
            {
                _logger.Warn("Neither Master nor Slave mode is enabled. The application will run but do nothing.");
            }

            // Enable running as a Windows service (Unchanged from original Program.cs).
            builder.Host.UseWindowsService();

            return builder;
        }

        /// <summary>
        /// Determines if the application is currently running in the context of a Windows Service.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the application is likely running as a Windows Service (parent process is 'services.exe');
        /// otherwise, <c>false</c>. Returns <c>false</c> on non-Windows operating systems.
        /// </returns>
        /// <remarks>
        /// This method provides a heuristic by checking the parent process name.
        /// While the .NET Host's `UseWindowsService` and the `--service` argument are the primary mechanisms
        /// for service integration, this check can be useful for determining content root or other specific adjustments.
        /// It uses helper methods from <see cref="ProcessExtensions"/> to inspect the parent process.
        /// </remarks>
        private static bool IsRunningAsWindowsService()
        {
            // This heuristic remains a valid way to check for service context if needed,
            // though the .NET Host handles the `--service` argument robustly.
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    var parentProcess = Process.GetCurrentProcess().ParentProcess();
                    return parentProcess != null && parentProcess.ProcessName.Equals("services", StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    _logger?.Warn(ex, "Could not determine parent process for Windows Service check.");
                    return false;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Provides extension methods for inspecting <see cref="Process"/> objects,
    /// primarily used to determine the parent process, which helps in identifying
    /// if the application is running as a Windows service.
    /// </summary>
    public static class ProcessExtensions
    {
        /// <summary>
        /// Finds the indexed name of a process (e.g., "processName#1") used by performance counters.
        /// </summary>
        /// <param name="processId">The ID of the process.</param>
        /// <returns>The indexed process name if found; otherwise, <c>null</c>.</returns>
        /// <remarks>
        /// Performance counters use indexed names when multiple instances of the same process name exist.
        /// This method iterates through these instances to find the one matching the given <paramref name="processId"/>.
        /// </remarks>
         private static string? FindIndexedProcessName(int processId)
         {
            try
            {
                var processName = Process.GetProcessById(processId).ProcessName;
                var processesByName = Process.GetProcessesByName(processName);
                string? processIndexdName = null;

                for (var index = 0; index < processesByName.Length; index++)
                {
                    processIndexdName = index == 0 ? processName : processName + "#" + index;

                    // Wrap the disposable object in a 'using' statement
                    using (var processIdUsingCounter = new PerformanceCounter("Process", "ID Process", processIndexdName))
                    {
                        if ((int)processIdUsingCounter.NextValue() == processId)
                        {
                            return processIndexdName;
                        }
                    }
                }
                return processIndexdName;
            }
            catch { return null; }
        }

        /// <summary>
        /// Finds the Process ID (PID) of the process that created the process identified by an indexed name.
        /// </summary>
        /// <param name="indexedProcessName">The indexed process name (e.g., "processName#1") obtained from performance counters.</param>
        /// <returns>The parent <see cref="Process"/> object if found; otherwise, <c>null</c>.</returns>
        /// <remarks>
        /// This method uses the "Creating Process ID" performance counter to get the PID of the parent process.
        /// </remarks>
        private static Process? FindPidFromIndexedProcessName(string indexedProcessName)
        {
            try
            {
                using var parentId = new PerformanceCounter("Process", "Creating Process ID", indexedProcessName);
                return Process.GetProcessById((int)parentId.NextValue());
            }
            catch { return null; }
        }

        /// <summary>
        /// Gets the parent process of the current <see cref="Process"/>.
        /// </summary>
        /// <param name="process">The current process (extension method target).</param>
        /// <returns>The parent <see cref="Process"/> if found; otherwise, <c>null</c>.</returns>
        public static Process? ParentProcess(this Process process)
        {
            return FindPidFromIndexedProcessName(FindIndexedProcessName(process.Id) ?? string.Empty);
        }
    }

    /// <summary>
    /// Provides extension methods for <see cref="WebApplication"/> to configure the
    /// HTTP request pipeline specific to SiteKeeper Master components.
    /// </summary>
    public static class WebApplicationExtensions
    {
        /// <summary>
        /// Configures the HTTP request processing pipeline for the SiteKeeper application,
        /// particularly for the Master components if they are enabled.
        /// </summary>
        /// <param name="app">The <see cref="WebApplication"/> instance to configure.</param>
        /// <returns>The configured <see cref="WebApplication"/> instance for chaining.</returns>
        /// <remarks>
        /// This method checks if Master components are enabled ("SiteKeeperMode" is "All" or "MasterOnly").
        /// If so, it configures the pipeline with:
        /// <list type="bullet">
        ///   <item><description>Swagger and SwaggerUI (in development environment).</description></item>
        ///   <item><description>Exception handler (in non-development environments).</description></item>
        ///   <item><description>Static file serving.</description></item>
        ///   <item><description>Routing.</description></item>
        ///   <item><description>Authentication and Authorization middleware.</description></item>
        ///   <item><description>Mapping for SignalR hubs: <see cref="GuiHub"/> (on GUI port) and <see cref="AgentHub"/> (on Agent port),
        ///   both constrained by their respective host/port configurations derived from <see cref="MasterConfig"/>.</description></item>
        ///   <item><description>Mapping for API endpoints using <see cref="ApiEndpointsExtensions.MapSiteKeeperApiEndpoints"/>, constrained to the GUI host/port.</description></item>
        ///   <item><description>A fallback to "index.html" for single-page application (SPA) support.</description></item>
        /// </list>
        /// If Master components are not enabled, it logs this and skips the pipeline configuration.
        /// </remarks>
        public static WebApplication ConfigureSiteKeeperPipeline(this WebApplication app)
        {
            // It's good practice to get a logger instance here from the app's services.
            var logger = app.Services.GetRequiredService<ILogger<Program>>();

            var siteKeeperMode = app.Configuration.GetValue<string>("SiteKeeperMode");
            bool runMaster = siteKeeperMode is "All" or "MasterOnly";

            if (runMaster)
            {
                var masterConfig = app.Services.GetRequiredService<IOptions<MasterConfig>>().Value;
                
                string guiHost = $"*:{masterConfig.GuiPort}";
                string agentHost = $"*:{masterConfig.AgentPort}";

                logger.LogInformation($"Master components enabled. Configuring request pipeline...");
                logger.LogInformation($" - GUI endpoints constrained to host '{guiHost}'.");
                logger.LogInformation($" - Agent endpoints constrained to host '{agentHost}'.");

                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SiteKeeper Master API v1"));
                }
                else
                {
                    app.UseExceptionHandler("/Error");
                }

                app.UseStaticFiles();
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                
                app.MapHub<GuiHub>("/guihub").RequireHost(guiHost);
                app.MapHub<AgentHub>("/agenthub").RequireHost(agentHost);
                app.MapSiteKeeperApiEndpoints(guiHost);
                app.MapFallbackToFile("index.html");
            }
            else
            {
                 logger.LogInformation("Master components are disabled. Skipping web server endpoint mapping.");
            }

            return app;
        }
    }
}