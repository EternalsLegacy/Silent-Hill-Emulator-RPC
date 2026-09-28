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
        var testProfile = new GameProfile
        {
            Identifier = "TEST_GAME",
            DisplayName = "Test Game",
            Enabled = true
        };

        var config = new AppConfig
        {
            Games = [testProfile]
        };

        var mockMonitor = new TestOptionsMonitor<AppConfig>(config);
        var manager = new ProfileManager(mockMonitor, NullLogger<ProfileManager>.Instance);

        string? toggledId = null;
        bool? toggledState = null;

        manager.ProfileToggled += (id, state) =>
        {
            toggledId = id;
            toggledState = state;
        };

        var result = manager.SetProfileEnabled("TEST_GAME", false);

        Assert.True(result);
        Assert.False(testProfile.Enabled);
        Assert.Equal("TEST_GAME", toggledId);
        Assert.False(toggledState);
    }

    [Fact]
    public void SetProfileEnabled_UnknownIdentifier_ReturnsFalse()
    {
        var config = new AppConfig
        {
            Games = []
        };

        var mockMonitor = new TestOptionsMonitor<AppConfig>(config);
        var manager = new ProfileManager(mockMonitor, NullLogger<ProfileManager>.Instance);

        var result = manager.SetProfileEnabled("UNKNOWN", true);

        Assert.False(result);
    }

    #endregion

    #region Helper Class

    private class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class
    {
        public TestOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    #endregion
}
