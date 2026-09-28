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

    private readonly IOptionsMonitor<AppConfig> ConfigMonitor;
    private readonly ILogger<ProfileManager> Logger;
    private readonly object SaveLock = new();

    #endregion

    #region Events

    public event Action<string, bool>? ProfileToggled;

    #endregion

    #region Constructor

    public ProfileManager(
        IOptionsMonitor<AppConfig> ConfigMonitor,
        ILogger<ProfileManager> Logger)
    {
        this.ConfigMonitor = ConfigMonitor;
        this.Logger = Logger;
    }

    #endregion

    #region Public Methods

    public IReadOnlyList<GameProfile> GetProfiles()
    {
        return ConfigMonitor.CurrentValue.Games;
    }

    public bool SetProfileEnabled(string Identifier, bool IsEnabled)
    {
        lock (SaveLock)
        {
            GameProfile? Profile = ConfigMonitor.CurrentValue.Games
                .FirstOrDefault(G => string.Equals(G.Identifier, Identifier, StringComparison.OrdinalIgnoreCase));

            if (Profile == null)
            {
                Logger.LogWarning("Profile with identifier '{Identifier}' not found.", Identifier);
                return false;
            }

            Profile.Enabled = IsEnabled;
            PersistProfileState(Identifier, IsEnabled);

            Logger.LogInformation("Profile '{Identifier}' ({Name}) detection set to {State}.",
                Identifier, Profile.DisplayName, IsEnabled ? "ENABLED" : "DISABLED");

            ProfileToggled?.Invoke(Identifier, IsEnabled);
            return true;
        }
    }

    #endregion

    #region Private Methods

    private void PersistProfileState(string Identifier, bool IsEnabled)
    {
        try
        {
            string? ConfigPath = FindAppSettingsPath();
            if (string.IsNullOrWhiteSpace(ConfigPath) || !File.Exists(ConfigPath))
            {
                Logger.LogWarning("appsettings.json not found for persisting state.");
                return;
            }

            string JsonContent = File.ReadAllText(ConfigPath);
            JsonNode? RootNode = JsonNode.Parse(JsonContent);

            if (RootNode?["DiscordRpc"]?["Games"] is JsonArray GamesArray)
            {
                bool Matched = false;
                foreach (JsonNode? Item in GamesArray)
                {
                    if (Item is JsonObject Obj &&
                        Obj.TryGetPropertyValue("Identifier", out JsonNode? IdNode) &&
                        string.Equals(IdNode?.ToString(), Identifier, StringComparison.OrdinalIgnoreCase))
                    {
                        Obj["Enabled"] = IsEnabled;
                        Matched = true;
                        break;
                    }
                }

                if (Matched)
                {
                    JsonSerializerOptions Options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };

                    File.WriteAllText(ConfigPath, RootNode.ToJsonString(Options));
                    Logger.LogDebug("Persisted enabled state for '{Identifier}' to {Path}", Identifier, ConfigPath);
                }
            }
        }
        catch (Exception Ex)
        {
            Logger.LogError(Ex, "Failed to persist profile state change to appsettings.json.");
        }
    }

    private static string? FindAppSettingsPath()
    {
        string ExeDir = AppContext.BaseDirectory;
        string DirectPath = Path.Combine(ExeDir, "appsettings.json");
        if (File.Exists(DirectPath))
        {
            return DirectPath;
        }

        string CwdPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        if (File.Exists(CwdPath))
        {
            return CwdPath;
        }

        string SrcPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "SilentHillEmulatorRPC", "appsettings.json");
        if (File.Exists(SrcPath))
        {
            return SrcPath;
        }

        return DirectPath;
    }

    #endregion
}
