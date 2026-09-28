namespace SilentHillEmulatorRPC.State;

/// <summary>
/// States for the Discord RPC Daemon lifecycle.
/// </summary>
public enum ServiceState
{
    /// <summary>
    /// Service is monitoring running processes for configured games.
    /// </summary>
    Idle,

    /// <summary>
    /// A configured game process is active and Rich Presence is published.
    /// </summary>
    ActiveGame,

    /// <summary>
    /// The active game has ended or closed; presence is being cleared and connection closed.
    /// </summary>
    Terminating
}
