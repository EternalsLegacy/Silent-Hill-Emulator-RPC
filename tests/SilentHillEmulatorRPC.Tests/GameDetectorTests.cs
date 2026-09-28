using Microsoft.Extensions.Logging.Abstractions;
using SilentHillEmulatorRPC.Configuration;
using SilentHillEmulatorRPC.Detection;

namespace SilentHillEmulatorRPC.Tests;

public class GameDetectorTests
{
    #region Test Fixtures & Setup

    private readonly GameDetector _detector = new(NullLogger<GameDetector>.Instance);

    private readonly GameProfile _sh1DuckProfile = new()
    {
        Identifier = "SH1_DUCK",
        DisplayName = "Silent Hill 1 (DuckStation)",
        Enabled = true,
        DiscordApplicationId = "1111111111111111",
        ProcessNames = ["duckstation-qt-x64-ReleaseLTCG", "duckstation-nogui-x64-ReleaseLTCG", "duckstation"],
        TitlePattern = @"Silent Hill (\((?<Region>[^)]+)\))?",
        LargeImageKey = "sh1_cover",
        LargeImageText = "Silent Hill (1999) - {Region}",
        SmallImageKey = "duckstation_icon",
        SmallImageText = "DuckStation PS1",
        DefaultDetailsText = "Playing {Title}",
        DefaultStateText = "Region: {Region}"
    };

    private readonly GameProfile _sh3ReloadedProfile = new()
    {
        Identifier = "SH3_RELOADED",
        DisplayName = "Silent Hill 3 (PC / Reloaded-II)",
        Enabled = true,
        DiscordApplicationId = "3333333333333333",
        ProcessNames = ["sh3"],
        TitlePattern = null,
        LargeImageKey = "sh3_cover",
        LargeImageText = "Silent Hill 3",
        SmallImageKey = "reloaded_icon",
        SmallImageText = "Reloaded-II",
        DefaultDetailsText = "Heather's Nightmare",
        DefaultStateText = "In the Otherworld"
    };

    #endregion

    #region Direct Process Detection Tests

    [Fact]
    public void DetectGame_MatchesDirectProcess_WithoutTitlePattern()
    {
        var mockProvider = new MockProcessProvider();
        mockProvider.AddProcess(101, "sh3", "SILENT HILL 3");

        var match = _detector.DetectGame([_sh3ReloadedProfile], mockProvider.GetRunningProcesses());

        Assert.NotNull(match);
        Assert.Equal("SH3_RELOADED", match.Profile.Identifier);
        Assert.Equal(101, match.ProcessId);
        Assert.Equal("sh3", match.ProcessName);
        Assert.Equal("Heather's Nightmare", match.Details);
        Assert.Equal("In the Otherworld", match.State);
    }

    [Fact]
    public void DetectGame_RespectsDisabledProfile()
    {
        var disabledProfile = new GameProfile
        {
            Identifier = "SH3_DISABLED",
            Enabled = false,
            ProcessNames = ["sh3"]
        };

        var mockProvider = new MockProcessProvider();
        mockProvider.AddProcess(101, "sh3", "Silent Hill 3");

        var match = _detector.DetectGame([disabledProfile], mockProvider.GetRunningProcesses());

        Assert.Null(match);
    }

    #endregion

    #region Emulator & Window Title Detection Tests

    [Fact]
    public void DetectGame_DuckStationWithSilentHillTitle_MatchesSuccessfully()
    {
        var mockProvider = new MockProcessProvider();
        mockProvider.AddProcess(202, "duckstation-qt-x64-ReleaseLTCG", "DuckStation - Silent Hill (USA) [SLUS-00898] [60.0 FPS]");

        var match = _detector.DetectGame([_sh1DuckProfile, _sh3ReloadedProfile], mockProvider.GetRunningProcesses());

        Assert.NotNull(match);
        Assert.Equal("SH1_DUCK", match.Profile.Identifier);
        Assert.Equal(202, match.ProcessId);
        Assert.Contains("DuckStation - Silent Hill (USA)", match.Details);
        Assert.Equal("Region: USA", match.State);
        Assert.Equal("Silent Hill (1999) - USA", match.LargeImageText);
    }

    [Fact]
    public void DetectGame_DuckStationWithOtherGame_DoesNotMatchSilentHill()
    {
        var mockProvider = new MockProcessProvider();
        mockProvider.AddProcess(303, "duckstation-qt-x64-ReleaseLTCG", "DuckStation - Resident Evil 2 (USA) [SLUS-00748]");

        var match = _detector.DetectGame([_sh1DuckProfile], mockProvider.GetRunningProcesses());

        Assert.Null(match);
    }

    [Fact]
    public void DetectGame_DuckStationIdleNoDisc_DoesNotMatch()
    {
        var mockProvider = new MockProcessProvider();
        mockProvider.AddProcess(404, "duckstation-qt-x64-ReleaseLTCG", "DuckStation");

        var match = _detector.DetectGame([_sh1DuckProfile], mockProvider.GetRunningProcesses());

        Assert.Null(match);
    }

    [Fact]
    public void DetectGame_MatchesWhenMainWindowTitleIsEmpty_ButChildWindowMatches()
    {
        var mockProvider = new MockProcessProvider();
        mockProvider.AddProcess(505, "duckstation-qt-x64-ReleaseLTCG", "", "DuckStation - Silent Hill (USA) [SLUS-00898]");

        var match = _detector.DetectGame([_sh1DuckProfile], mockProvider.GetRunningProcesses());

        Assert.NotNull(match);
        Assert.Equal(505, match.ProcessId);
        Assert.Contains("Silent Hill (USA)", match.WindowTitle);
    }

    [Fact]
    public void DetectGame_ExtractsDynamicDetailsPattern()
    {
        var dynamicProfile = new GameProfile
        {
            Identifier = "DYNAMIC_SH",
            Enabled = true,
            ProcessNames = ["pcsx2-qt"],
            TitlePattern = "Silent Hill",
            DynamicDetailsPattern = @"(Silent Hill \d?: [^\[]+)",
            DefaultDetailsText = "Fallback Details",
            DefaultStateText = "In Emulator"
        };

        var mockProvider = new MockProcessProvider();
        mockProvider.AddProcess(606, "pcsx2-qt", "PCSX2 - Silent Hill 2: Director's Cut [SLES-51156] (60 FPS)");

        var match = _detector.DetectGame([dynamicProfile], mockProvider.GetRunningProcesses());

        Assert.NotNull(match);
        Assert.Equal("Silent Hill 2: Director's Cut ", match.Details);
        Assert.Equal("In Emulator", match.State);
    }

    #endregion

    #region IsMatchStillActive Tests

    [Fact]
    public void IsMatchStillActive_ReturnsTrue_WhenProcessAliveAndTitleMatches()
    {
        var mockProvider = new MockProcessProvider();
        mockProvider.AddProcess(202, "duckstation", "DuckStation - Silent Hill (USA)");

        var isStillActive = _detector.IsMatchStillActive(_sh1DuckProfile, 202, mockProvider, out var updatedResult);

        Assert.True(isStillActive);
        Assert.NotNull(updatedResult);
        Assert.Equal(202, updatedResult.ProcessId);
    }

    [Fact]
    public void IsMatchStillActive_ReturnsFalse_WhenProcessExited()
    {
        var mockProvider = new MockProcessProvider();

        var isStillActive = _detector.IsMatchStillActive(_sh1DuckProfile, 202, mockProvider, out var updatedResult);

        Assert.False(isStillActive);
        Assert.Null(updatedResult);
    }

    [Fact]
    public void IsMatchStillActive_ReturnsFalse_WhenDuckStationSwitchesGame()
    {
        var mockProvider = new MockProcessProvider();
        mockProvider.AddProcess(202, "duckstation", "DuckStation - Metal Gear Solid (USA)");

        var isStillActive = _detector.IsMatchStillActive(_sh1DuckProfile, 202, mockProvider, out var updatedResult);

        Assert.False(isStillActive);
        Assert.Null(updatedResult);
    }

    #endregion
}
