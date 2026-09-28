using DiscordRPC;
using DiscordRPC.Logging;
using Microsoft.Extensions.Logging;

namespace SilentHillEmulatorRPC.Discord;

/// <summary>
/// Thread-safe coordinator for the Discord RPC client lifecycle.
/// Supports dynamic application ID switching and reconnection resilience.
/// </summary>
public class DiscordCoordinator : IDiscordCoordinator
{
    #region Fields

    private readonly ILogger<DiscordCoordinator> Logger;
    private readonly object SyncLock = new();
    private DiscordRpcClient? Client;
    private bool Disposed;

    #endregion

    #region Properties

    public bool IsInitialized => Client?.IsInitialized ?? false;

    public string? CurrentApplicationId => Client?.ApplicationID;

    #endregion

    #region Constructor

    public DiscordCoordinator(ILogger<DiscordCoordinator> Logger)
    {
        this.Logger = Logger;
    }

    #endregion

    #region Public Methods

    public bool Connect(string ApplicationId)
    {
        if (string.IsNullOrWhiteSpace(ApplicationId))
        {
            Logger.LogWarning("Cannot connect to Discord: Application ID is empty or not configured.");
            return false;
        }

        lock (SyncLock)
        {
            if (Client != null &&
                string.Equals(Client.ApplicationID, ApplicationId, StringComparison.OrdinalIgnoreCase) &&
                Client.IsInitialized)
            {
                return true;
            }

            CleanupClient();

            try
            {
                Logger.LogInformation("Connecting to Discord IPC with Application ID: {AppId}", ApplicationId);

                Client = new DiscordRpcClient(ApplicationId)
                {
                    Logger = new ConsoleLogger(DiscordRPC.Logging.LogLevel.Warning)
                };

                Client.OnReady += (Sender, Msg) =>
                {
                    Logger.LogInformation("Discord RPC connected successfully for user {User} (v{Version}).",
                        Msg.User.Username, Msg.Version);
                };

                Client.OnPresenceUpdate += (Sender, Msg) =>
                {
                    Logger.LogDebug("Discord presence updated: {Details} | {State}",
                        Msg.Presence?.Details, Msg.Presence?.State);
                };

                Client.OnError += (Sender, Msg) =>
                {
                    Logger.LogError("Discord RPC Error [{Code}]: {Message}", Msg.Code, Msg.Message);
                };

                Client.OnConnectionFailed += (Sender, Msg) =>
                {
                    Logger.LogWarning("Discord RPC connection failed (pipe {Pipe}). Is Discord running?", Msg.FailedPipe);
                };

                Client.OnClose += (Sender, Msg) =>
                {
                    Logger.LogInformation("Discord RPC connection closed: {Reason}", Msg.Reason);
                };

                bool Initialized = Client.Initialize();
                if (!Initialized)
                {
                    Logger.LogWarning("DiscordRpcClient.Initialize returned false. Discord may not be running.");
                }

                return Initialized;
            }
            catch (Exception Ex)
            {
                Logger.LogError(Ex, "Unexpected error while initializing Discord RPC client for {AppId}.", ApplicationId);
                CleanupClient();
                return false;
            }
        }
    }

    public void SetPresence(RichPresence Presence)
    {
        lock (SyncLock)
        {
            if (Client == null || !Client.IsInitialized)
            {
                Logger.LogDebug("Cannot set presence: Discord client is not initialized.");
                return;
            }

            try
            {
                Client.SetPresence(Presence);
            }
            catch (Exception Ex)
            {
                Logger.LogError(Ex, "Failed to send Rich Presence update to Discord.");
            }
        }
    }

    public void ClearPresence()
    {
        lock (SyncLock)
        {
            if (Client == null || !Client.IsInitialized)
            {
                return;
            }

            try
            {
                Logger.LogInformation("Clearing Discord Rich Presence.");
                Client.ClearPresence();
            }
            catch (Exception Ex)
            {
                Logger.LogDebug(Ex, "Error while clearing Discord Rich Presence.");
            }
        }
    }

    public void Disconnect()
    {
        lock (SyncLock)
        {
            CleanupClient();
        }
    }

    #endregion

    #region Private Methods

    private void CleanupClient()
    {
        if (Client == null)
            return;

        try
        {
            if (Client.IsInitialized)
            {
                Client.ClearPresence();
                Client.Deinitialize();
            }
            Client.Dispose();
        }
        catch (Exception Ex)
        {
            Logger.LogDebug(Ex, "Error while disposing Discord RPC client.");
        }
        finally
        {
            Client = null;
        }
    }

    #endregion

    #region IDisposable Support

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;

        Disconnect();
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    #endregion
}
