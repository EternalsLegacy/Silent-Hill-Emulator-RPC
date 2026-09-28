using SilentHillEmulatorRPC.Configuration;
using SilentHillEmulatorRPC.Detection;
using SilentHillEmulatorRPC.State;

namespace SilentHillEmulatorRPC.Tests;

public class RpcStateMachineTests
{
    #region Test Setup

    private readonly GameProfile Profile = new()
    {
        Identifier = "TEST_GAME",
        DisplayName = "Test Game",
        DiscordApplicationId = "123456789"
    };

    #endregion

    #region State Transition Tests

    [Fact]
    public void InitialState_IsIdle()
    {
        RpcStateMachine Machine = new RpcStateMachine();

        Assert.Equal(ServiceState.Idle, Machine.CurrentState);
        Assert.Null(Machine.CurrentMatch);
        Assert.Null(Machine.SessionStartTimeUtc);
    }

    [Fact]
    public void TransitionToActive_SetsActiveStateAndSessionTimer()
    {
        RpcStateMachine Machine = new RpcStateMachine();
        GameMatchResult Match = new GameMatchResult(
            Profile, 100, "test", "Test Title", "Details", "State", "large", null, null, null);

        ServiceState? ReportedOld = null;
        ServiceState? ReportedNew = null;
        Machine.StateChanged += (OldState, NewState) =>
        {
            ReportedOld = OldState;
            ReportedNew = NewState;
        };

        Machine.TransitionToActive(Match);

        Assert.Equal(ServiceState.ActiveGame, Machine.CurrentState);
        Assert.NotNull(Machine.CurrentMatch);
        Assert.NotNull(Machine.SessionStartTimeUtc);
        Assert.Equal(ServiceState.Idle, ReportedOld);
        Assert.Equal(ServiceState.ActiveGame, ReportedNew);
    }

    [Fact]
    public void UpdateActiveMatch_PreservesOriginalSessionStartTime()
    {
        RpcStateMachine Machine = new RpcStateMachine();
        GameMatchResult InitialMatch = new GameMatchResult(
            Profile, 100, "test", "Old Title", "Old Details", "State", "large", null, null, null);

        Machine.TransitionToActive(InitialMatch);
        DateTime? InitialStartTime = Machine.SessionStartTimeUtc;

        GameMatchResult UpdatedMatch = new GameMatchResult(
            Profile, 100, "test", "New Title", "New Details", "State", "large", null, null, null);

        Machine.UpdateActiveMatch(UpdatedMatch);

        Assert.Equal("New Details", Machine.CurrentMatch?.Details);
        Assert.Equal(InitialStartTime, Machine.SessionStartTimeUtc);
    }

    [Fact]
    public void TransitionToTerminating_And_Idle_ResetsMatchAndTimer()
    {
        RpcStateMachine Machine = new RpcStateMachine();
        GameMatchResult Match = new GameMatchResult(
            Profile, 100, "test", "Test Title", "Details", "State", "large", null, null, null);

        Machine.TransitionToActive(Match);
        Machine.TransitionToTerminating();

        Assert.Equal(ServiceState.Terminating, Machine.CurrentState);

        Machine.TransitionToIdle();

        Assert.Equal(ServiceState.Idle, Machine.CurrentState);
        Assert.Null(Machine.CurrentMatch);
        Assert.Null(Machine.SessionStartTimeUtc);
    }

    #endregion
}
