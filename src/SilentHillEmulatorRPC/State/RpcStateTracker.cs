using SilentHillEmulatorRPC.Detection;

namespace SilentHillEmulatorRPC.State;

/// <summary>
/// Thread-safe tracker for live RPC daemon state shared between worker and system tray UI.
/// </summary>
public class RpcStateTracker : IRpcStateTracker
{
    #region Fields

    private readonly object _syncLock = new();

    #endregion

    #region Properties

    public ServiceState CurrentState { get; private set; } = ServiceState.Idle;
    public GameMatchResult? CurrentMatch { get; private set; }
    public DateTime? SessionStartTimeUtc { get; private set; }

    #endregion

    #region Events

    public event Action<ServiceState, GameMatchResult?>? StateUpdated;

    #endregion

    #region Public Methods

    public void UpdateState(ServiceState state, GameMatchResult? match, DateTime? sessionStartTimeUtc)
    {
        lock (_syncLock)
        {
            CurrentState = state;
            CurrentMatch = match;
            SessionStartTimeUtc = sessionStartTimeUtc;
        }

        StateUpdated?.Invoke(state, match);
    }

    #endregion
}
