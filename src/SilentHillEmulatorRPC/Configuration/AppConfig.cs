namespace SilentHillEmulatorRPC.Configuration;

/// <summary>
/// Root application settings for the Silent Hill Discord RPC Daemon.
/// </summary>
public class AppConfig
{
    #region Constants

    public const string SectionName = "DiscordRpc";

    #endregion

    #region Properties

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
    /// When false (default), Discord Rich Presence will only show Game Title, Elapsed Time,
    /// Large Icon, and Small Icon without any extra details or state strings.
    /// </summary>
    public bool ShowDetailsAndState { get; set; } = false;

    /// <summary>
    /// List of configured game and emulator profiles.
    /// </summary>
    public List<GameProfile> Games { get; set; } = [];

    #endregion
}
