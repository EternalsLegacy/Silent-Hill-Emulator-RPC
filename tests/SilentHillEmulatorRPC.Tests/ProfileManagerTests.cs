using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SilentHillEmulatorRPC.Configuration;

namespace SilentHillEmulatorRPC.Tests;

public class ProfileManagerTests
{
    #region Tests

    [Fact]
    public void SetProfileEnabled_TogglesProfileAndRaisesEvent()
    {
        GameProfile TestProfile = new GameProfile
        {
            Identifier = "TEST_GAME",
            DisplayName = "Test Game",
            Enabled = true
        };

        AppConfig Config = new AppConfig
        {
            Games = [TestProfile]
        };

        TestOptionsMonitor<AppConfig> MockMonitor = new TestOptionsMonitor<AppConfig>(Config);
        ProfileManager Manager = new ProfileManager(MockMonitor, NullLogger<ProfileManager>.Instance);

        string? ToggledId = null;
        bool? ToggledState = null;

        Manager.ProfileToggled += (Id, State) =>
        {
            ToggledId = Id;
            ToggledState = State;
        };

        bool Result = Manager.SetProfileEnabled("TEST_GAME", false);

        Assert.True(Result);
        Assert.False(TestProfile.Enabled);
        Assert.Equal("TEST_GAME", ToggledId);
        Assert.False(ToggledState);
    }

    [Fact]
    public void SetProfileEnabled_UnknownIdentifier_ReturnsFalse()
    {
        AppConfig Config = new AppConfig
        {
            Games = []
        };

        TestOptionsMonitor<AppConfig> MockMonitor = new TestOptionsMonitor<AppConfig>(Config);
        ProfileManager Manager = new ProfileManager(MockMonitor, NullLogger<ProfileManager>.Instance);

        bool Result = Manager.SetProfileEnabled("UNKNOWN", true);

        Assert.False(Result);
    }

    #endregion

    #region Helper Class

    private class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class
    {
        public TestOptionsMonitor(T CurrentValue)
        {
            this.CurrentValue = CurrentValue;
        }

        public T CurrentValue { get; }

        public T Get(string? Name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> Listener) => null;
    }

    #endregion
}
