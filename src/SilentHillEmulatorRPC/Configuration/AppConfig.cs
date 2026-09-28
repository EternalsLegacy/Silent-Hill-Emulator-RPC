namespace SilentHillEmulatorRPC.Configuration;

/// <summary>
/// Root application settings for the Discord RPC Daemon.
/// </summary>
public class AppConfig
{
    public const string SectionName = "DiscordRpc";

    /// <summary>
    /// Interval in seconds between process polling cycles (default: 3 seconds).
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 3;

    /// <summary>
    /// Delay in seconds before attempting to reconnect to Discord IPC if connection was lost.
    /// </summary>
    public int ReconnectDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Whether to automatically attempt reconnecting to Discord if it is not open or closes.
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// List of configured game and emulator profiles.
    /// </summary>
    public List<GameProfile> Games { get; set; } = [];
}
