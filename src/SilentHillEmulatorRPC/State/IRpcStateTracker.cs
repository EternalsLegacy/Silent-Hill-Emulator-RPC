using SilentHillEmulatorRPC.Detection;

namespace SilentHillEmulatorRPC.State;

/// <summary>
/// Service tracking the current RPC state for UI components like the System Tray icon.
/// </summary>
public interface IRpcStateTracker
{
    #region Properties

    /// <summary>
    /// Current lifecycle state of the RPC daemon.
    /// </summary>
    ServiceState CurrentState { get; }

    /// <summary>
    /// Metadata of the currently active game match, or null if idle.
    /// </summary>
    GameMatchResult? CurrentMatch { get; }

    /// <summary>
    /// Session start time in UTC.
    /// </summary>
    DateTime? SessionStartTimeUtc { get; }

    #endregion

    #region Events

    /// <summary>
    /// Event triggered when the active game or service state changes.
    /// </summary>
    event Action<ServiceState, GameMatchResult?>? StateUpdated;

    #endregion

    #region Methods

    /// <summary>
    /// Updates the state and notifies subscribers.
    /// </summary>
    void UpdateState(ServiceState state, GameMatchResult? match, DateTime? sessionStartTimeUtc);

    #endregion
}
