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
    public static async Task Main(string[] Args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        IHost HostInstance = CreateHostBuilder(Args).Build();

        await HostInstance.StartAsync();

        IProfileManager ProfileManager = HostInstance.Services.GetRequiredService<IProfileManager>();
        IRpcStateTracker StateTracker = HostInstance.Services.GetRequiredService<IRpcStateTracker>();
        IHostApplicationLifetime HostLifetime = HostInstance.Services.GetRequiredService<IHostApplicationLifetime>();
        ILogger<TrayApplicationContext> Logger = HostInstance.Services.GetRequiredService<ILogger<TrayApplicationContext>>();

        using TrayApplicationContext TrayContext = new TrayApplicationContext(
            ProfileManager,
            StateTracker,
            () => HostLifetime.StopApplication(),
            Logger);

        Application.Run(TrayContext);

        await HostInstance.StopAsync();
    }

    #endregion

    #region Host Configuration

    public static IHostBuilder CreateHostBuilder(string[] Args) =>
        Host.CreateDefaultBuilder(Args)
            .ConfigureAppConfiguration((HostingContext, Config) =>
            {
                Config.SetBasePath(AppContext.BaseDirectory);
                Config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                Config.AddEnvironmentVariables(prefix: "SHRPC_");
                Config.AddCommandLine(Args);
            })
            .ConfigureLogging((Context, Logging) =>
            {
                Logging.ClearProviders();
                Logging.AddConfiguration(Context.Configuration.GetSection("Logging"));
                Logging.AddSimpleConsole(Options =>
                {
                    Options.TimestampFormat = "[HH:mm:ss] ";
                });
            })
            .ConfigureServices((HostContext, Services) =>
            {
                Services.Configure<AppConfig>(HostContext.Configuration.GetSection(AppConfig.SectionName));

                Services.AddSingleton<IProcessProvider, SystemProcessProvider>();
                Services.AddSingleton<IGameDetector, GameDetector>();
                Services.AddSingleton<IDiscordCoordinator, DiscordCoordinator>();
                Services.AddSingleton<IProfileManager, ProfileManager>();
                Services.AddSingleton<IRpcStateTracker, RpcStateTracker>();

                Services.AddHostedService<RpcWorkerService>();
            });

    #endregion
}
