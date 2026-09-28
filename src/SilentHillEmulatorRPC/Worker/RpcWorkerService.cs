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
/// Background daemon executing the polling loop and driving the RPC state machine.
/// </summary>
public class RpcWorkerService : BackgroundService
{
    private readonly IOptionsMonitor<AppConfig> _configMonitor;
    private readonly IGameDetector _gameDetector;
    private readonly IDiscordCoordinator _discordCoordinator;
    private readonly IProcessProvider _processProvider;
    private readonly ILogger<RpcWorkerService> _logger;
    private readonly RpcStateMachine _stateMachine = new();

    private DateTime _lastReconnectAttempt = DateTime.MinValue;

    public RpcWorkerService(
        IOptionsMonitor<AppConfig> configMonitor,
        IGameDetector gameDetector,
        IDiscordCoordinator discordCoordinator,
        IProcessProvider processProvider,
        ILogger<RpcWorkerService> logger)
    {
        _configMonitor = configMonitor;
        _gameDetector = gameDetector;
        _discordCoordinator = discordCoordinator;
        _processProvider = processProvider;
        _logger = logger;

        _stateMachine.StateChanged += OnStateChanged;
    }

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

        // Connect Discord IPC
        _discordCoordinator.Connect(match.Profile.DiscordApplicationId);

        // Transition state
        _stateMachine.TransitionToActive(match);

        // Publish initial presence
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

        // Check if metadata (window title or details) changed
        if (updatedResult != null && (updatedResult.Details != current.Details || updatedResult.State != current.State))
        {
            _logger.LogDebug("Presence metadata updated: {Details} | {State}", updatedResult.Details, updatedResult.State);
            _stateMachine.UpdateActiveMatch(updatedResult);
            PublishPresence(updatedResult);
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

    private void PublishPresence(GameMatchResult match)
    {
        var startTime = _stateMachine.SessionStartTimeUtc ?? DateTime.UtcNow;

        var presence = new RichPresence
        {
            Details = match.Details,
            State = match.State,
            Timestamps = new Timestamps(startTime),
            Assets = new Assets
            {
                LargeImageKey = match.LargeImageKey,
                LargeImageText = match.LargeImageText,
                SmallImageKey = match.SmallImageKey,
                SmallImageText = match.SmallImageText
            }
        };

        _discordCoordinator.SetPresence(presence);
    }

    private void OnStateChanged(ServiceState oldState, ServiceState newState)
    {
        _logger.LogInformation("State transition: [{OldState}] -> [{NewState}]", oldState, newState);
    }

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
}
