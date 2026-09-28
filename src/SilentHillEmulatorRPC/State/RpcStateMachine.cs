using SilentHillEmulatorRPC.Detection;

namespace SilentHillEmulatorRPC.State;

/// <summary>
/// State machine managing transitions between Idle, ActiveGame, and Terminating states,
/// while maintaining session start time for elapsed time metrics.
/// </summary>
public class RpcStateMachine
{
    #region Properties

    public ServiceState CurrentState { get; private set; } = ServiceState.Idle;
    public GameMatchResult? CurrentMatch { get; private set; }
    public DateTime? SessionStartTimeUtc { get; private set; }

    #endregion

    #region Events

    /// <summary>
    /// Event triggered when the state transitions from oldState to newState.
    /// </summary>
    public event Action<ServiceState, ServiceState>? StateChanged;

    #endregion

    #region State Transition Methods

    /// <summary>
    /// Transition from Idle or Terminating into ActiveGame upon game detection.
    /// Initializes session elapsed timer.
    /// </summary>
    public void TransitionToActive(GameMatchResult Match)
    {
        ArgumentNullException.ThrowIfNull(Match);

        ServiceState OldState = CurrentState;
        CurrentState = ServiceState.ActiveGame;
        CurrentMatch = Match;
        SessionStartTimeUtc ??= DateTime.UtcNow;

        StateChanged?.Invoke(OldState, CurrentState);
    }

    /// <summary>
    /// Updates metadata for the currently active game (e.g., dynamic title or stage change)
    /// without resetting the session start time.
    /// </summary>
    public void UpdateActiveMatch(GameMatchResult UpdatedMatch)
    {
        ArgumentNullException.ThrowIfNull(UpdatedMatch);

        if (CurrentState == ServiceState.ActiveGame)
        {
            CurrentMatch = UpdatedMatch;
        }
    }

    /// <summary>
    /// Transition from ActiveGame to Terminating when the target process exits or window title changes.
    /// </summary>
    public void TransitionToTerminating()
    {
        if (CurrentState == ServiceState.Terminating)
            return;

        ServiceState OldState = CurrentState;
        CurrentState = ServiceState.Terminating;

        StateChanged?.Invoke(OldState, CurrentState);
    }

    /// <summary>
    /// Transition from Terminating (or any state) back to Idle.
    /// Resets session timer and match info.
    /// </summary>
    public void TransitionToIdle()
    {
        ServiceState OldState = CurrentState;
        CurrentState = ServiceState.Idle;
        CurrentMatch = null;
        SessionStartTimeUtc = null;

        StateChanged?.Invoke(OldState, CurrentState);
    }

    #endregion
}
