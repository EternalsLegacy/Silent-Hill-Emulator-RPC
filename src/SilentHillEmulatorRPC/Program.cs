using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SilentHillEmulatorRPC.Configuration;
using SilentHillEmulatorRPC.Detection;
using SilentHillEmulatorRPC.Discord;
using SilentHillEmulatorRPC.Worker;

namespace SilentHillEmulatorRPC;

public static class Program
{
    public static async Task Main(string[] args)
    {
        PrintBanner();

        var builder = Host.CreateDefaultBuilder(args)
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
                // Bind configuration with IOptionsMonitor support
                services.Configure<AppConfig>(hostContext.Configuration.GetSection(AppConfig.SectionName));

                // Core service registrations
                services.AddSingleton<IProcessProvider, SystemProcessProvider>();
                services.AddSingleton<IGameDetector, GameDetector>();
                services.AddSingleton<IDiscordCoordinator, DiscordCoordinator>();

                // Background worker
                services.AddHostedService<RpcWorkerService>();
            });

        var host = builder.Build();
        await host.RunAsync();
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(@"  ____  _ _            _     _   _ _ _ _   ____  ____   ____ ");
        Console.WriteLine(@" / ___|(_) | ___ _ __ | |_  | | | (_) | | |  _ \|  _ \ / ___|");
        Console.WriteLine(@" \___ \| | |/ _ \ '_ \| __| | |_| | | | | | |_) | |_) | |    ");
        Console.WriteLine(@"  ___) | | |  __/ | | | |_  |  _  | | | | |  _ <|  __/| |___ ");
        Console.WriteLine(@" |____/|_|_|\___|_| |_|\__| |_| |_|_|_|_| |_| \_\_|    \____|");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(" Universal Discord Rich Presence Daemon for Emulators & PC Ports");
        Console.WriteLine(" Version 1.0.0 | Press Ctrl+C to exit");
        Console.WriteLine("-----------------------------------------------------------------");
        Console.ResetColor();
    }
}
