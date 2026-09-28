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

    private readonly ILogger<DiscordCoordinator> _logger;
    private readonly object _syncLock = new();
    private DiscordRpcClient? _client;
    private bool _disposed;

    #endregion

    #region Properties

    public bool IsInitialized => _client?.IsInitialized ?? false;

    public string? CurrentApplicationId => _client?.ApplicationID;

    #endregion

    #region Constructor

    public DiscordCoordinator(ILogger<DiscordCoordinator> logger)
    {
        _logger = logger;
    }

    #endregion

    #region Public Methods

    public bool Connect(string applicationId)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            _logger.LogWarning("Cannot connect to Discord: Application ID is empty or not configured.");
            return false;
        }

        lock (_syncLock)
        {
            // If already connected with the same Application ID and still active, reuse it
            if (_client != null &&
                string.Equals(_client.ApplicationID, applicationId, StringComparison.OrdinalIgnoreCase) &&
                _client.IsInitialized)
            {
                return true;
            }

            // If switching application IDs or restarting client, clean up old client first
            CleanupClient();

            try
            {
                _logger.LogInformation("Connecting to Discord IPC with Application ID: {AppId}", applicationId);

                _client = new DiscordRpcClient(applicationId)
                {
                    Logger = new ConsoleLogger(DiscordRPC.Logging.LogLevel.Warning)
                };

                _client.OnReady += (sender, msg) =>
                {
                    _logger.LogInformation("Discord RPC connected successfully for user {User} (v{Version}).",
                        msg.User.Username, msg.Version);
                };

                _client.OnPresenceUpdate += (sender, msg) =>
                {
                    _logger.LogDebug("Discord presence updated: {Details} | {State}",
                        msg.Presence?.Details, msg.Presence?.State);
                };

                _client.OnError += (sender, msg) =>
                {
                    _logger.LogError("Discord RPC Error [{Code}]: {Message}", msg.Code, msg.Message);
                };

                _client.OnConnectionFailed += (sender, msg) =>
                {
                    _logger.LogWarning("Discord RPC connection failed (pipe {Pipe}). Is Discord running?", msg.FailedPipe);
                };

                _client.OnClose += (sender, msg) =>
                {
                    _logger.LogInformation("Discord RPC connection closed: {Reason}", msg.Reason);
                };

                var initialized = _client.Initialize();
                if (!initialized)
                {
                    _logger.LogWarning("DiscordRpcClient.Initialize returned false. Discord may not be running.");
                }

                return initialized;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while initializing Discord RPC client for {AppId}.", applicationId);
                CleanupClient();
                return false;
            }
        }
    }

    public void SetPresence(RichPresence presence)
    {
        lock (_syncLock)
        {
            if (_client == null || !_client.IsInitialized)
            {
                _logger.LogDebug("Cannot set presence: Discord client is not initialized.");
                return;
            }

            try
            {
                _client.SetPresence(presence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send Rich Presence update to Discord.");
            }
        }
    }

    public void ClearPresence()
    {
        lock (_syncLock)
        {
            if (_client == null || !_client.IsInitialized)
            {
                return;
            }

            try
            {
                _logger.LogInformation("Clearing Discord Rich Presence.");
                _client.ClearPresence();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error while clearing Discord Rich Presence.");
            }
        }
    }

    public void Disconnect()
    {
        lock (_syncLock)
        {
            CleanupClient();
        }
    }

    #endregion

    #region Private Methods

    private void CleanupClient()
    {
        if (_client == null)
            return;

        try
        {
            if (_client.IsInitialized)
            {
                _client.ClearPresence();
                _client.Deinitialize();
            }
            _client.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error while disposing Discord RPC client.");
        }
        finally
        {
            _client = null;
        }
    }

    #endregion

    #region IDisposable Support

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

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
