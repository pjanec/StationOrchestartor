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
using SiteKeeper.Master.Workflow;

namespace SiteKeeper.ConsoleHost
{
    public class Program
    {
        private static NLog.Logger? _logger;

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

                // Automatically register all ISlaveTaskHandler implementations.
                // This is the primary mechanism for adding new slave-side task logic.
                builder.Services.Scan(scan => scan
                    .FromAssemblyOf<ISlaveTaskHandler>() // Scans the SiteKeeper.Slave assembly
                    .AddClasses(classes => classes.AssignableTo<ISlaveTaskHandler>())
                    .AsImplementedInterfaces()
                    .WithSingletonLifetime()); // Handlers are stateless
                
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
                builder.Services.AddSingleton<MasterNLogSetup>().AddHostedService(p => p.GetRequiredService<MasterNLogSetup>());

                // Register the specialized stage handler for multi-node operations.
                // 1. Register the concrete class as a singleton. This is required by AgentHub, which injects
                //    the concrete type to access its public methods not defined on the interface.
                builder.Services.AddSingleton<NodeActionDispatcher>();
                
                // 2. Register the same interface to resolve to the same singleton instance.
                //    This ensures that any service asking for the interface (like our action handlers)
                //    and any service asking for the concrete class get the *exact same instance*,
                //    preserving its state (e.g., the _activeOperations dictionary).
                builder.Services.AddSingleton<INodeActionDispatcher>(sp =>
                    sp.GetRequiredService<NodeActionDispatcher>());
                
                builder.Services.AddSingleton<IJournal, Journal>();
                builder.Services.AddSingleton<IGuiNotifier, GuiNotifier>();
                builder.Services.AddSingleton<IAgentConnectionManager, AgentConnectionManager>();
                builder.Services.AddSingleton<INodeHealthMonitor, NodeHealthMonitor>();
                builder.Services.AddSingleton<IActionIdTranslator, ActionIdMappingService>();
                builder.Services.AddHostedService<MasterLifecycleNotifier>();

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
                builder.Services.AddSingleton<IMasterActionCoordinator>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<MasterActionCoordinator>>();
                    var nlogSetupService = sp.GetRequiredService<MasterNLogSetup>();
                    var journalService = sp.GetRequiredService<IJournal>();
                    var actionIdTranslator = sp.GetRequiredService<IActionIdTranslator>();
                    return new MasterActionCoordinator(logger, sp, journalService, nlogSetupService, actionIdTranslator);
                });

                // Automatically register all IMasterActionHandler implementations with a Scoped lifetime.
                // This is the primary mechanism for adding new top-level workflows (Master Actions).
                builder.Services.Scan(scan => scan
                    .FromAssemblyOf<IMasterActionHandler>()
                    .AddClasses(classes => classes.AssignableTo<IMasterActionHandler>())
                    .AsImplementedInterfaces()
                    .WithScopedLifetime());

                builder.Services.AddScoped<IWorkflowLogger, SiteKeeper.Master.Workflow.WorkflowLogger>();
            }


            if (!runMaster && !runSlave)
            {
                _logger.Warn("Neither Master nor Slave mode is enabled. The application will run but do nothing.");
            }

            // Enable running as a Windows service (Unchanged from original Program.cs).
            builder.Host.UseWindowsService();

            return builder;
        }

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

    // The ProcessExtensions helper class remains unchanged.
    public static class ProcessExtensions
    {
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

        private static Process? FindPidFromIndexedProcessName(string indexedProcessName)
        {
            try
            {
                using var parentId = new PerformanceCounter("Process", "Creating Process ID", indexedProcessName);
                return Process.GetProcessById((int)parentId.NextValue());
            }
            catch { return null; }
        }

        public static Process? ParentProcess(this Process process)
        {
            return FindPidFromIndexedProcessName(FindIndexedProcessName(process.Id) ?? string.Empty);
        }
    }


    // This class contains the pipeline logic moved from the Main method.
    public static class WebApplicationExtensions
    {
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