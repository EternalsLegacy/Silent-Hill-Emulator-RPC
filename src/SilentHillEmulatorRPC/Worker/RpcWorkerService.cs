using DiscordRPC;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SilentHillEmulatorRPC.Configuration;
using SilentHillEmulatorRPC.Detection;
using SilentHillEmulatorRPC.Discord;
using SilentHillEmulatorRPC.State;

namespace SilentHillEmulatorRPC.Worker;

/// <summary>
/// Background daemon executing the process polling loop and driving the RPC state machine.
/// </summary>
public class RpcWorkerService : BackgroundService
{
    #region Fields

    private readonly IOptionsMonitor<AppConfig> ConfigMonitor;
    private readonly IProfileManager ProfileManager;
    private readonly IGameDetector GameDetector;
    private readonly IDiscordCoordinator DiscordCoordinator;
    private readonly IProcessProvider ProcessProvider;
    private readonly IRpcStateTracker StateTracker;
    private readonly ILogger<RpcWorkerService> Logger;
    private readonly RpcStateMachine StateMachine = new();

    private DateTime LastReconnectAttempt = DateTime.MinValue;

    #endregion

    #region Constructor

    public RpcWorkerService(
        IOptionsMonitor<AppConfig> ConfigMonitor,
        IProfileManager ProfileManager,
        IGameDetector GameDetector,
        IDiscordCoordinator DiscordCoordinator,
        IProcessProvider ProcessProvider,
        IRpcStateTracker StateTracker,
        ILogger<RpcWorkerService> Logger)
    {
        this.ConfigMonitor = ConfigMonitor;
        this.ProfileManager = ProfileManager;
        this.GameDetector = GameDetector;
        this.DiscordCoordinator = DiscordCoordinator;
        this.ProcessProvider = ProcessProvider;
        this.StateTracker = StateTracker;
        this.Logger = Logger;

        StateMachine.StateChanged += OnStateMachineChanged;
        this.ProfileManager.ProfileToggled += OnProfileToggled;
    }

    #endregion

    #region BackgroundService Overrides

    protected override async Task ExecuteAsync(CancellationToken StoppingToken)
    {
        Logger.LogInformation("====================================================");
        Logger.LogInformation("  Silent Hill Universal Discord RPC Daemon started  ");
        Logger.LogInformation("====================================================");

        LogLoadedProfiles(ConfigMonitor.CurrentValue);

        while (!StoppingToken.IsCancellationRequested)
        {
            AppConfig Config = ConfigMonitor.CurrentValue;
            TimeSpan PollInterval = TimeSpan.FromSeconds(Math.Max(1, Config.PollingIntervalSeconds));

            try
            {
                switch (StateMachine.CurrentState)
                {
                    case ServiceState.Idle:
                        HandleIdleState(Config);
                        break;

                    case ServiceState.ActiveGame:
                        HandleActiveGameState(Config);
                        break;

                    case ServiceState.Terminating:
                        HandleTerminatingState();
                        break;
                }
            }
            catch (Exception Ex)
            {
                Logger.LogError(Ex, "Unexpected error occurred during polling cycle.");
            }

            try
            {
                await Task.Delay(PollInterval, StoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        HandleTerminatingState();
    }

    public override async Task StopAsync(CancellationToken CancellationToken)
    {
        Logger.LogInformation("Shutting down Discord RPC Daemon...");
        HandleTerminatingState();
        await base.StopAsync(CancellationToken);
    }

    #endregion

    #region State Handling Methods

    private void HandleIdleState(AppConfig Config)
    {
        IReadOnlyList<ProcessSnapshot> Processes = ProcessProvider.GetRunningProcesses();
        GameMatchResult? Match = GameDetector.DetectGame(Config.Games, Processes);

        if (Match == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Match.Profile.DiscordApplicationId) ||
            Match.Profile.DiscordApplicationId.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning(
                "Game detected [{Identifier}] '{DisplayName}' (PID {Pid}), but DiscordApplicationId is not set in configuration! Please enter your Client ID.",
                Match.Profile.Identifier, Match.Profile.DisplayName, Match.ProcessId);
            return;
        }

        Logger.LogInformation(">>> Game detected: {DisplayName} (PID: {Pid}, Process: '{ProcessName}')",
            Match.Profile.DisplayName, Match.ProcessId, Match.ProcessName);

        DiscordCoordinator.Connect(Match.Profile.DiscordApplicationId);
        StateMachine.TransitionToActive(Match);
        PublishPresence(Match);
    }

    private void HandleActiveGameState(AppConfig Config)
    {
        GameMatchResult? Current = StateMachine.CurrentMatch;
        if (Current == null)
        {
            StateMachine.TransitionToTerminating();
            return;
        }

        GameProfile? Profile = Config.Games.FirstOrDefault(G =>
            string.Equals(G.Identifier, Current.Profile.Identifier, StringComparison.OrdinalIgnoreCase));

        if (Profile != null && !Profile.Enabled)
        {
            Logger.LogInformation("Active game {DisplayName} was disabled via UI.", Current.Profile.DisplayName);
            StateMachine.TransitionToTerminating();
            return;
        }

        bool StillActive = GameDetector.IsMatchStillActive(
            Current.Profile,
            Current.ProcessId,
            ProcessProvider,
            out GameMatchResult? UpdatedResult);

        if (!StillActive)
        {
            Logger.LogInformation("<<< Game closed or title changed: {DisplayName} (PID: {Pid})",
                Current.Profile.DisplayName, Current.ProcessId);

            StateMachine.TransitionToTerminating();
            return;
        }

        if (!DiscordCoordinator.IsInitialized && Config.AutoReconnect)
        {
            TimeSpan ReconnectInterval = TimeSpan.FromSeconds(Math.Max(2, Config.ReconnectDelaySeconds));
            if (DateTime.UtcNow - LastReconnectAttempt >= ReconnectInterval)
            {
                LastReconnectAttempt = DateTime.UtcNow;
                Logger.LogDebug("Attempting to reconnect to Discord IPC for active game {DisplayName}...", Current.Profile.DisplayName);
                if (DiscordCoordinator.Connect(Current.Profile.DiscordApplicationId))
                {
                    PublishPresence(UpdatedResult ?? Current);
                }
            }
        }
    }

    private void HandleTerminatingState()
    {
        Logger.LogInformation("Cleaning up Discord Presence and resetting session.");

        try
        {
            DiscordCoordinator.ClearPresence();
            DiscordCoordinator.Disconnect();
        }
        catch (Exception Ex)
        {
            Logger.LogDebug(Ex, "Exception during Discord client disconnection.");
        }

        StateMachine.TransitionToIdle();
    }

    #endregion

    #region Presence Publishing

    private void PublishPresence(GameMatchResult Match)
    {
        DateTime StartTime = StateMachine.SessionStartTimeUtc ?? DateTime.UtcNow;
        AppConfig Config = ConfigMonitor.CurrentValue;

        string? Details = null;
        string? State = null;

        if (Config.ShowDetailsAndState)
        {
            Details = string.IsNullOrWhiteSpace(Match.Details) ? null : Match.Details;
            State = string.IsNullOrWhiteSpace(Match.State) ? null : Match.State;
        }

        RichPresence Presence = new RichPresence
        {
            Details = Details,
            State = State,
            Timestamps = new Timestamps(StartTime),
            Assets = new Assets
            {
                LargeImageKey = Match.LargeImageKey,
                LargeImageText = string.IsNullOrWhiteSpace(Match.LargeImageText) ? null : Match.LargeImageText,
                SmallImageKey = string.IsNullOrWhiteSpace(Match.SmallImageKey) ? null : Match.SmallImageKey,
                SmallImageText = string.IsNullOrWhiteSpace(Match.SmallImageText) ? null : Match.SmallImageText
            }
        };

        DiscordCoordinator.SetPresence(Presence);
    }

    #endregion

    #region Event Callbacks

    private void OnStateMachineChanged(ServiceState OldState, ServiceState NewState)
    {
        Logger.LogInformation("State transition: [{OldState}] -> [{NewState}]", OldState, NewState);
        StateTracker.UpdateState(NewState, StateMachine.CurrentMatch, StateMachine.SessionStartTimeUtc);
    }

    private void OnProfileToggled(string Identifier, bool IsEnabled)
    {
        if (!IsEnabled &&
            StateMachine.CurrentState == ServiceState.ActiveGame &&
            StateMachine.CurrentMatch != null &&
            string.Equals(StateMachine.CurrentMatch.Profile.Identifier, Identifier, StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogInformation("Currently running game [{Identifier}] was disabled by user in tray menu. Terminating presence.", Identifier);
            StateMachine.TransitionToTerminating();
        }
    }

    #endregion

    #region Logging Helpers

    private void LogLoadedProfiles(AppConfig Config)
    {
        List<GameProfile> ActiveProfiles = Config.Games.Where(P => P.Enabled).ToList();
        Logger.LogInformation("Loaded {Count} active game profile(s):", ActiveProfiles.Count);

        foreach (GameProfile Profile in ActiveProfiles)
        {
            string ProcessList = Profile.ProcessNames.Count > 0
                ? string.Join(", ", Profile.ProcessNames)
                : Profile.ProcessName ?? "none";

            string TitleFilter = !string.IsNullOrWhiteSpace(Profile.TitlePattern)
                ? $" [Title: '{Profile.TitlePattern}']"
                : "";

            Logger.LogInformation("  - [{Id}] {Name}: Processes: ({Processes}){Title}",
                Profile.Identifier, Profile.DisplayName, ProcessList, TitleFilter);
        }
    }

    #endregion
}
