using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SilentHillEmulatorRPC.Configuration;

/// <summary>
/// Manages game profiles and persists enable/disable state to appsettings.json.
/// </summary>
public class ProfileManager : IProfileManager
{
    #region Fields

    private readonly IOptionsMonitor<AppConfig> _configMonitor;
    private readonly ILogger<ProfileManager> _logger;
    private readonly object _saveLock = new();

    #endregion

    #region Events

    public event Action<string, bool>? ProfileToggled;

    #endregion

    #region Constructor

    public ProfileManager(
        IOptionsMonitor<AppConfig> configMonitor,
        ILogger<ProfileManager> logger)
    {
        _configMonitor = configMonitor;
        _logger = logger;
    }

    #endregion

    #region Public Methods

    public IReadOnlyList<GameProfile> GetProfiles()
    {
        return _configMonitor.CurrentValue.Games;
    }

    public bool SetProfileEnabled(string identifier, bool isEnabled)
    {
        lock (_saveLock)
        {
            var profile = _configMonitor.CurrentValue.Games
                .FirstOrDefault(g => string.Equals(g.Identifier, identifier, StringComparison.OrdinalIgnoreCase));

            if (profile == null)
            {
                _logger.LogWarning("Profile with identifier '{Identifier}' not found.", identifier);
                return false;
            }

            profile.Enabled = isEnabled;
            PersistProfileState(identifier, isEnabled);

            _logger.LogInformation("Profile '{Identifier}' ({Name}) detection set to {State}.",
                identifier, profile.DisplayName, isEnabled ? "ENABLED" : "DISABLED");

            ProfileToggled?.Invoke(identifier, isEnabled);
            return true;
        }
    }

    #endregion

    #region Private Methods

    private void PersistProfileState(string identifier, bool isEnabled)
    {
        try
        {
            var configPath = FindAppSettingsPath();
            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
            {
                _logger.LogWarning("appsettings.json not found for persisting state.");
                return;
            }

            var jsonContent = File.ReadAllText(configPath);
            var jsonNode = JsonNode.Parse(jsonContent);

            if (jsonNode?["DiscordRpc"]?["Games"] is JsonArray gamesArray)
            {
                var matched = false;
                foreach (var item in gamesArray)
                {
                    if (item is JsonObject obj &&
                        obj.TryGetPropertyValue("Identifier", out var idNode) &&
                        string.Equals(idNode?.ToString(), identifier, StringComparison.OrdinalIgnoreCase))
                    {
                        obj["Enabled"] = isEnabled;
                        matched = true;
                        break;
                    }
                }

                if (matched)
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };

                    File.WriteAllText(configPath, jsonNode.ToJsonString(options));
                    _logger.LogDebug("Persisted enabled state for '{Identifier}' to {Path}", identifier, configPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist profile state change to appsettings.json.");
        }
    }

    private static string? FindAppSettingsPath()
    {
        // Check next to running executable
        var exeDir = AppContext.BaseDirectory;
        var directPath = Path.Combine(exeDir, "appsettings.json");
        if (File.Exists(directPath))
        {
            return directPath;
        }

        // Check current working directory
        var cwdPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        if (File.Exists(cwdPath))
        {
            return cwdPath;
        }

        // Check project source directory if running in development
        var srcPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "SilentHillEmulatorRPC", "appsettings.json");
        if (File.Exists(srcPath))
        {
            return srcPath;
        }

        return directPath;
    }

    #endregion
}
