using System.Text.Json.Serialization;

namespace SilentHillEmulatorRPC.Configuration;

/// <summary>
/// Configuration profile for a specific game or emulator target.
/// </summary>
public class GameProfile
{
    /// <summary>
    /// Unique identifier for this game profile (e.g., "SH1_DUCK", "SH3_RELOADED").
    /// </summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>
    /// Friendly display name for logs and messages.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this game profile is currently active in detection.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The Discord Application / Client ID from the Discord Developer Portal.
    /// </summary>
    public string DiscordApplicationId { get; set; } = string.Empty;

    /// <summary>
    /// List of executable names (without .exe, case-insensitive) to watch for.
    /// </summary>
    public List<string> ProcessNames { get; set; } = [];

    /// <summary>
    /// Legacy/single process name fallback if ProcessNames is omitted in config.
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
    /// Required for emulators like DuckStation running multiple games.
    /// </summary>
    public string? TitlePattern { get; set; }

    /// <summary>
    /// Asset key in Discord Developer Portal for the main cover image.
    /// </summary>
    public string LargeImageKey { get; set; } = string.Empty;

    /// <summary>
    /// Hover tooltip text for the large image. Supports tokens like {Title}.
    /// </summary>
    public string? LargeImageText { get; set; }

    /// <summary>
    /// Optional asset key in Discord Developer Portal for the small badge/logo (e.g., platform or emulator logo).
    /// </summary>
    public string? SmallImageKey { get; set; }

    /// <summary>
    /// Hover tooltip text for the small badge.
    /// </summary>
    public string? SmallImageText { get; set; }

    /// <summary>
    /// Line 1 of Discord Rich Presence (Details). Supports tokens like {Title}.
    /// </summary>
    public string? DefaultDetailsText { get; set; }

    /// <summary>
    /// Line 2 of Discord Rich Presence (State). Supports tokens like {Title}.
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
}
