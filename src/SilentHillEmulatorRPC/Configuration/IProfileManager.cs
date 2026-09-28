namespace SilentHillEmulatorRPC.Configuration;

/// <summary>
/// Service managing game profiles and persisting enable/disable toggles to appsettings.json.
/// </summary>
public interface IProfileManager
{
    #region Events

    /// <summary>
    /// Event triggered when a game profile is enabled or disabled.
    /// Parameters: Identifier, IsEnabled.
    /// </summary>
    event Action<string, bool>? ProfileToggled;

    #endregion

    #region Methods

    /// <summary>
    /// Returns the current list of game profiles.
    /// </summary>
    IReadOnlyList<GameProfile> GetProfiles();

    /// <summary>
    /// Enables or disables detection for a specific game profile and saves the setting.
    /// </summary>
    bool SetProfileEnabled(string Identifier, bool IsEnabled);

    #endregion
}
