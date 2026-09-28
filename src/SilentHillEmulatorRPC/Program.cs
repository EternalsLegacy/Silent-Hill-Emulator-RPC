using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SilentHillEmulatorRPC.Configuration;
using SilentHillEmulatorRPC.Detection;
using SilentHillEmulatorRPC.Discord;
using SilentHillEmulatorRPC.State;
using SilentHillEmulatorRPC.UI;
using SilentHillEmulatorRPC.Worker;

namespace SilentHillEmulatorRPC;

public static class Program
{
    #region Entry Point

    [STAThread]
    public static async Task Main(string[] args)
    {
        // Initialize Windows Forms subsystem for System Tray NotifyIcon
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Build generic host with Dependency Injection, Configuration, and Logging
        var host = CreateHostBuilder(args).Build();

        // Start background worker services
        await host.StartAsync();

        // Resolve dependencies for the System Tray UI
        var profileManager = host.Services.GetRequiredService<IProfileManager>();
        var stateTracker = host.Services.GetRequiredService<IRpcStateTracker>();
        var hostLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var logger = host.Services.GetRequiredService<ILogger<TrayApplicationContext>>();

        // Create and run the System Tray Application Context
        using var trayContext = new TrayApplicationContext(
            profileManager,
            stateTracker,
            () => hostLifetime.StopApplication(),
            logger);

        // Run Windows message loop on STA thread (blocks until Exit in tray menu)
        Application.Run(trayContext);

        // Graceful host shutdown when tray loop finishes
        await host.StopAsync();
    }

    #endregion

    #region Host Configuration

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddEnvironmentVariables(prefix: "SHRPC_");
                config.AddCommandLine(args);
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConfiguration(context.Configuration.GetSection("Logging"));
                logging.AddSimpleConsole(options =>
                {
                    options.TimestampFormat = "[HH:mm:ss] ";
                });
            })
            .ConfigureServices((hostContext, services) =>
            {
                // Options binding
                services.Configure<AppConfig>(hostContext.Configuration.GetSection(AppConfig.SectionName));

                // Core service registrations
                services.AddSingleton<IProcessProvider, SystemProcessProvider>();
                services.AddSingleton<IGameDetector, GameDetector>();
                services.AddSingleton<IDiscordCoordinator, DiscordCoordinator>();
                services.AddSingleton<IProfileManager, ProfileManager>();
                services.AddSingleton<IRpcStateTracker, RpcStateTracker>();

                // Background worker
                services.AddHostedService<RpcWorkerService>();
            });

    #endregion
}
