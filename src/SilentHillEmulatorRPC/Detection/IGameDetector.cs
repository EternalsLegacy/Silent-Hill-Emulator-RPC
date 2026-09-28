using SilentHillEmulatorRPC.Configuration;

namespace SilentHillEmulatorRPC.Detection;

/// <summary>
/// Service responsible for evaluating game profiles against running processes.
/// </summary>
public interface IGameDetector
{
    #region Methods

    /// <summary>
    /// Evaluates the list of configured game profiles against running processes and returns the first match, or null.
    /// </summary>
    GameMatchResult? DetectGame(IReadOnlyList<GameProfile> Profiles, IReadOnlyList<ProcessSnapshot> RunningProcesses);

    /// <summary>
    /// Checks if a previously matched game is still running and its window title (if required) still matches.
    /// Returns updated metadata if dynamic tokens or window title changed.
    /// </summary>
    bool IsMatchStillActive(GameProfile Profile, int ProcessId, IProcessProvider ProcessProvider, out GameMatchResult? UpdatedResult);

    #endregion
}
