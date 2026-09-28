using DiscordRPC;

namespace SilentHillEmulatorRPC.Discord;

/// <summary>
/// Coordinates the lifecycle of the Discord IPC connection.
/// Supports switching between different Discord Application IDs for multi-game setups.
/// </summary>
public interface IDiscordCoordinator : IAsyncDisposable, IDisposable
{
    #region Properties

    /// <summary>
    /// Gets whether a Discord RPC client is initialized.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Gets the Discord Application ID currently in use.
    /// </summary>
    string? CurrentApplicationId { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Connects to Discord with the specified Application ID.
    /// If already connected with a different Application ID, cleanly switches to the new one.
    /// </summary>
    bool Connect(string applicationId);

    /// <summary>
    /// Sets or updates the active Rich Presence in Discord.
    /// </summary>
    void SetPresence(RichPresence presence);

    /// <summary>
    /// Clears any currently active Rich Presence in Discord.
    /// </summary>
    void ClearPresence();

    /// <summary>
    /// Disconnects the current Discord RPC client and releases resources.
    /// </summary>
    void Disconnect();

    #endregion
}
