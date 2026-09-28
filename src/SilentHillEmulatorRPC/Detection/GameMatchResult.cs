using SilentHillEmulatorRPC.Configuration;

namespace SilentHillEmulatorRPC.Detection;

/// <summary>
/// Result of a successful match between a running process and a game profile.
/// </summary>
public record GameMatchResult(
    GameProfile Profile,
    int ProcessId,
    string ProcessName,
    string WindowTitle,
    string Details,
    string State,
    string LargeImageKey,
    string? LargeImageText,
    string? SmallImageKey,
    string? SmallImageText
);
