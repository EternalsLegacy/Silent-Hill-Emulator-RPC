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

    private readonly IOptionsMonitor<AppConfig> _configMonitor;
    private readonly IProfileManager _profileManager;
    private readonly IGameDetector _gameDetector;
    private readonly IDiscordCoordinator _discordCoordinator;
    private readonly IProcessProvider _processProvider;
    private readonly IRpcStateTracker _stateTracker;
    private readonly ILogger<RpcWorkerService> _logger;
    private readonly RpcStateMachine _stateMachine = new();

    private DateTime _lastReconnectAttempt = DateTime.MinValue;

    #endregion

    #region Constructor

    public RpcWorkerService(
        IOptionsMonitor<AppConfig> configMonitor,
        IProfileManager profileManager,
        IGameDetector gameDetector,
        IDiscordCoordinator discordCoordinator,
        IProcessProvider processProvider,
        IRpcStateTracker stateTracker,
        ILogger<RpcWorkerService> logger)
    {
        _configMonitor = configMonitor;
        _profileManager = profileManager;
        _gameDetector = gameDetector;
        _discordCoordinator = discordCoordinator;
        _processProvider = processProvider;
        _stateTracker = stateTracker;
        _logger = logger;

        _stateMachine.StateChanged += OnStateMachineChanged;
        _profileManager.ProfileToggled += OnProfileToggled;
    }

    #endregion

    #region BackgroundService Overrides

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("====================================================");
        _logger.LogInformation("  Silent Hill Universal Discord RPC Daemon started  ");
        _logger.LogInformation("====================================================");

        LogLoadedProfiles(_configMonitor.CurrentValue);

        while (!stoppingToken.IsCancellationRequested)
        {
            var config = _configMonitor.CurrentValue;
            var pollInterval = TimeSpan.FromSeconds(Math.Max(1, config.PollingIntervalSeconds));

            try
            {
                switch (_stateMachine.CurrentState)
                {
                    case ServiceState.Idle:
                        HandleIdleState(config);
                        break;

                    case ServiceState.ActiveGame:
                        HandleActiveGameState(config);
                        break;

                    case ServiceState.Terminating:
                        HandleTerminatingState();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred during polling cycle.");
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // Clean termination upon stopping
        HandleTerminatingState();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Shutting down Discord RPC Daemon...");
        HandleTerminatingState();
        await base.StopAsync(cancellationToken);
    }

    #endregion

    #region State Handling Methods

    private void HandleIdleState(AppConfig config)
    {
        var processes = _processProvider.GetRunningProcesses();
        var match = _gameDetector.DetectGame(config.Games, processes);

        if (match == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(match.Profile.DiscordApplicationId) ||
            match.Profile.DiscordApplicationId.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Game detected [{Identifier}] '{DisplayName}' (PID {Pid}), but DiscordApplicationId is not set in configuration! Please enter your Client ID.",
                match.Profile.Identifier, match.Profile.DisplayName, match.ProcessId);
            return;
        }

        _logger.LogInformation(">>> Game detected: {DisplayName} (PID: {Pid}, Process: '{ProcessName}')",
            match.Profile.DisplayName, match.ProcessId, match.ProcessName);

        // Connect to Discord IPC
        _discordCoordinator.Connect(match.Profile.DiscordApplicationId);

        // Transition state
        _stateMachine.TransitionToActive(match);

        // Publish presence
        PublishPresence(match);
    }

    private void HandleActiveGameState(AppConfig config)
    {
        var current = _stateMachine.CurrentMatch;
        if (current == null)
        {
            _stateMachine.TransitionToTerminating();
            return;
        }

        // Check if the current profile was disabled in the tray menu
        var profile = config.Games.FirstOrDefault(g =>
            string.Equals(g.Identifier, current.Profile.Identifier, StringComparison.OrdinalIgnoreCase));

        if (profile != null && !profile.Enabled)
        {
            _logger.LogInformation("Active game {DisplayName} was disabled via UI.", current.Profile.DisplayName);
            _stateMachine.TransitionToTerminating();
            return;
        }

        // Verify if process and window title condition still hold
        var stillActive = _gameDetector.IsMatchStillActive(
            current.Profile,
            current.ProcessId,
            _processProvider,
            out var updatedResult);

        if (!stillActive)
        {
            _logger.LogInformation("<<< Game closed or title changed: {DisplayName} (PID: {Pid})",
                current.Profile.DisplayName, current.ProcessId);

            _stateMachine.TransitionToTerminating();
            return;
        }

        // Attempt reconnection if Discord wasn't running or closed
        if (!_discordCoordinator.IsInitialized && config.AutoReconnect)
        {
            var reconnectInterval = TimeSpan.FromSeconds(Math.Max(2, config.ReconnectDelaySeconds));
            if (DateTime.UtcNow - _lastReconnectAttempt >= reconnectInterval)
            {
                _lastReconnectAttempt = DateTime.UtcNow;
                _logger.LogDebug("Attempting to reconnect to Discord IPC for active game {DisplayName}...", current.Profile.DisplayName);
                if (_discordCoordinator.Connect(current.Profile.DiscordApplicationId))
                {
                    PublishPresence(updatedResult ?? current);
                }
            }
        }
    }

    private void HandleTerminatingState()
    {
        _logger.LogInformation("Cleaning up Discord Presence and resetting session.");

        try
        {
            _discordCoordinator.ClearPresence();
            _discordCoordinator.Disconnect();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Exception during Discord client disconnection.");
        }

        _stateMachine.TransitionToIdle();
    }

    #endregion

    #region Presence Publishing

    private void PublishPresence(GameMatchResult match)
    {
        var startTime = _stateMachine.SessionStartTimeUtc ?? DateTime.UtcNow;
        var config = _configMonitor.CurrentValue;

        // When ShowDetailsAndState is false (default), omit Details and State entirely
        // so Discord displays strictly: Game Title, Elapsed Time, Large Icon, and Small Icon (like Destiny 2).
        string? details = null;
        string? state = null;

        if (config.ShowDetailsAndState)
        {
            details = string.IsNullOrWhiteSpace(match.Details) ? null : match.Details;
            state = string.IsNullOrWhiteSpace(match.State) ? null : match.State;
        }

        var presence = new RichPresence
        {
            Details = details,
            State = state,
            Timestamps = new Timestamps(startTime),
            Assets = new Assets
            {
                LargeImageKey = match.LargeImageKey,
                LargeImageText = string.IsNullOrWhiteSpace(match.LargeImageText) ? null : match.LargeImageText,
                SmallImageKey = string.IsNullOrWhiteSpace(match.SmallImageKey) ? null : match.SmallImageKey,
                SmallImageText = string.IsNullOrWhiteSpace(match.SmallImageText) ? null : match.SmallImageText
            }
        };

        _discordCoordinator.SetPresence(presence);
    }

    #endregion

    #region Event Callbacks

    private void OnStateMachineChanged(ServiceState oldState, ServiceState newState)
    {
        _logger.LogInformation("State transition: [{OldState}] -> [{NewState}]", oldState, newState);
        _stateTracker.UpdateState(newState, _stateMachine.CurrentMatch, _stateMachine.SessionStartTimeUtc);
    }

    private void OnProfileToggled(string identifier, bool isEnabled)
    {
        if (!isEnabled &&
            _stateMachine.CurrentState == ServiceState.ActiveGame &&
            _stateMachine.CurrentMatch != null &&
            string.Equals(_stateMachine.CurrentMatch.Profile.Identifier, identifier, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Currently running game [{Identifier}] was disabled by user in tray menu. Terminating presence.", identifier);
            _stateMachine.TransitionToTerminating();
        }
    }

    #endregion

    #region Logging Helpers

    private void LogLoadedProfiles(AppConfig config)
    {
        var activeProfiles = config.Games.Where(p => p.Enabled).ToList();
        _logger.LogInformation("Loaded {Count} active game profile(s):", activeProfiles.Count);

        foreach (var profile in activeProfiles)
        {
            var processList = profile.ProcessNames.Count > 0
                ? string.Join(", ", profile.ProcessNames)
                : profile.ProcessName ?? "none";

            var titleFilter = !string.IsNullOrWhiteSpace(profile.TitlePattern)
                ? $" [Title: '{profile.TitlePattern}']"
                : "";

            _logger.LogInformation("  - [{Id}] {Name}: Processes: ({Processes}){Title}",
                profile.Identifier, profile.DisplayName, processList, titleFilter);
        }
    }

    #endregion
}
