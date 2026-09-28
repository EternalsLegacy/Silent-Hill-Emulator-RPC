using SilentHillEmulatorRPC.Detection;

namespace SilentHillEmulatorRPC.State;

/// <summary>
/// Thread-safe tracker for live RPC daemon state shared between worker and system tray UI.
/// </summary>
public class RpcStateTracker : IRpcStateTracker
{
    #region Fields

    private readonly object SyncLock = new();

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

    public void UpdateState(ServiceState State, GameMatchResult? Match, DateTime? SessionStartTimeUtc)
    {
        lock (SyncLock)
        {
            CurrentState = State;
            CurrentMatch = Match;
            this.SessionStartTimeUtc = SessionStartTimeUtc;
        }

        StateUpdated?.Invoke(State, Match);
    }

    #endregion
}
