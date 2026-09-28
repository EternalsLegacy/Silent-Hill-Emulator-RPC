using SilentHillEmulatorRPC.Configuration;
using SilentHillEmulatorRPC.Detection;
using SilentHillEmulatorRPC.State;

namespace SilentHillEmulatorRPC.Tests;

public class RpcStateMachineTests
{
    #region Test Setup

    private readonly GameProfile _profile = new()
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
        var machine = new RpcStateMachine();

        Assert.Equal(ServiceState.Idle, machine.CurrentState);
        Assert.Null(machine.CurrentMatch);
        Assert.Null(machine.SessionStartTimeUtc);
    }

    [Fact]
    public void TransitionToActive_SetsActiveStateAndSessionTimer()
    {
        var machine = new RpcStateMachine();
        var match = new GameMatchResult(
            _profile, 100, "test", "Test Title", "Details", "State", "large", null, null, null);

        ServiceState? reportedOld = null;
        ServiceState? reportedNew = null;
        machine.StateChanged += (o, n) =>
        {
            reportedOld = o;
            reportedNew = n;
        };

        machine.TransitionToActive(match);

        Assert.Equal(ServiceState.ActiveGame, machine.CurrentState);
        Assert.NotNull(machine.CurrentMatch);
        Assert.NotNull(machine.SessionStartTimeUtc);
        Assert.Equal(ServiceState.Idle, reportedOld);
        Assert.Equal(ServiceState.ActiveGame, reportedNew);
    }

    [Fact]
    public void UpdateActiveMatch_PreservesOriginalSessionStartTime()
    {
        var machine = new RpcStateMachine();
        var initialMatch = new GameMatchResult(
            _profile, 100, "test", "Old Title", "Old Details", "State", "large", null, null, null);

        machine.TransitionToActive(initialMatch);
        var initialStartTime = machine.SessionStartTimeUtc;

        var updatedMatch = new GameMatchResult(
            _profile, 100, "test", "New Title", "New Details", "State", "large", null, null, null);

        machine.UpdateActiveMatch(updatedMatch);

        Assert.Equal("New Details", machine.CurrentMatch?.Details);
        Assert.Equal(initialStartTime, machine.SessionStartTimeUtc);
    }

    [Fact]
    public void TransitionToTerminating_And_Idle_ResetsMatchAndTimer()
    {
        var machine = new RpcStateMachine();
        var match = new GameMatchResult(
            _profile, 100, "test", "Test Title", "Details", "State", "large", null, null, null);

        machine.TransitionToActive(match);
        machine.TransitionToTerminating();

        Assert.Equal(ServiceState.Terminating, machine.CurrentState);

        machine.TransitionToIdle();

        Assert.Equal(ServiceState.Idle, machine.CurrentState);
        Assert.Null(machine.CurrentMatch);
        Assert.Null(machine.SessionStartTimeUtc);
    }

    #endregion
}
