using System.Text.Json.Serialization;

namespace SilentHillEmulatorRPC.Configuration;

/// <summary>
/// Configuration profile for a specific game or emulator target.
/// </summary>
public class GameProfile
{
    #region Identification & Display

    /// <summary>
    /// Unique identifier for this game profile (e.g., "SH1_DUCK", "SH3_RELOADED").
    /// </summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>
    /// Friendly display name for UI and logs.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Whether detection is active for this game. Can be toggled via the system tray menu.
    /// </summary>
    public bool Enabled { get; set; } = true;

    #endregion

    #region Discord Application

    /// <summary>
    /// The Discord Application / Client ID from the Discord Developer Portal.
    /// </summary>
    public string DiscordApplicationId { get; set; } = string.Empty;

    #endregion

    #region Process & Window Matching

    /// <summary>
    /// List of executable names (without .exe, case-insensitive) to watch for.
    /// </summary>
    public List<string> ProcessNames { get; set; } = [];

    /// <summary>
    /// Legacy or single process name fallback.
    /// </summary>
    public string? ProcessName
    {
        get => ProcessNames.Count > 0 ? ProcessNames[0] : null;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && !ProcessNames.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                ProcessNames.Add(value);
            }
        }
    }

    /// <summary>
    /// Optional regex or substring filter applied to the process window title.
    /// </summary>
    public string? TitlePattern { get; set; }

    #endregion

    #region Rich Presence Assets & Texts

    /// <summary>
    /// Asset key in Discord Developer Portal for the main cover image.
    /// </summary>
    public string LargeImageKey { get; set; } = string.Empty;

    /// <summary>
    /// Tooltip hover text for the large cover image.
    /// </summary>
    public string? LargeImageText { get; set; }

    /// <summary>
    /// Asset key in Discord Developer Portal for the small badge/logo.
    /// </summary>
    public string? SmallImageKey { get; set; }

    /// <summary>
    /// Tooltip hover text for the small badge.
    /// </summary>
    public string? SmallImageText { get; set; }

    /// <summary>
    /// Optional Line 1 text for Discord Rich Presence (omitted when empty or ShowDetailsAndState is false).
    /// </summary>
    public string? DefaultDetailsText { get; set; }

    /// <summary>
    /// Optional Line 2 text for Discord Rich Presence (omitted when empty or ShowDetailsAndState is false).
    /// </summary>
    public string? DefaultStateText { get; set; }

    /// <summary>
    /// Optional regex pattern to extract dynamic details from window title.
    /// </summary>
    public string? DynamicDetailsPattern { get; set; }

    /// <summary>
    /// Optional regex pattern to extract dynamic state from window title.
    /// </summary>
    public string? DynamicStatePattern { get; set; }

    #endregion
}
