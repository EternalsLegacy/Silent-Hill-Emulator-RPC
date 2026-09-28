using Microsoft.Extensions.Logging.Abstractions;
using SilentHillEmulatorRPC.Configuration;
using SilentHillEmulatorRPC.Detection;

namespace SilentHillEmulatorRPC.Tests;

public class GameDetectorTests
{
    #region Test Fixtures & Setup

    private readonly GameDetector Detector = new(NullLogger<GameDetector>.Instance);

    private readonly GameProfile Sh1DuckProfile = new()
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

    private readonly GameProfile Sh3ReloadedProfile = new()
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
        MockProcessProvider MockProvider = new MockProcessProvider();
        MockProvider.AddProcess(101, "sh3", "SILENT HILL 3");

        GameMatchResult? Match = Detector.DetectGame([Sh3ReloadedProfile], MockProvider.GetRunningProcesses());

        Assert.NotNull(Match);
        Assert.Equal("SH3_RELOADED", Match.Profile.Identifier);
        Assert.Equal(101, Match.ProcessId);
        Assert.Equal("sh3", Match.ProcessName);
        Assert.Equal("Heather's Nightmare", Match.Details);
        Assert.Equal("In the Otherworld", Match.State);
    }

    [Fact]
    public void DetectGame_RespectsDisabledProfile()
    {
        GameProfile DisabledProfile = new GameProfile
        {
            Identifier = "SH3_DISABLED",
            Enabled = false,
            ProcessNames = ["sh3"]
        };

        MockProcessProvider MockProvider = new MockProcessProvider();
        MockProvider.AddProcess(101, "sh3", "Silent Hill 3");

        GameMatchResult? Match = Detector.DetectGame([DisabledProfile], MockProvider.GetRunningProcesses());

        Assert.Null(Match);
    }

    #endregion

    #region Emulator & Window Title Detection Tests

    [Fact]
    public void DetectGame_DuckStationWithSilentHillTitle_MatchesSuccessfully()
    {
        MockProcessProvider MockProvider = new MockProcessProvider();
        MockProvider.AddProcess(202, "duckstation-qt-x64-ReleaseLTCG", "DuckStation - Silent Hill (USA) [SLUS-00898] [60.0 FPS]");

        GameMatchResult? Match = Detector.DetectGame([Sh1DuckProfile, Sh3ReloadedProfile], MockProvider.GetRunningProcesses());

        Assert.NotNull(Match);
        Assert.Equal("SH1_DUCK", Match.Profile.Identifier);
        Assert.Equal(202, Match.ProcessId);
        Assert.Contains("DuckStation - Silent Hill (USA)", Match.Details);
        Assert.Equal("Region: USA", Match.State);
        Assert.Equal("Silent Hill (1999) - USA", Match.LargeImageText);
    }

    [Fact]
    public void DetectGame_DuckStationWithOtherGame_DoesNotMatchSilentHill()
    {
        MockProcessProvider MockProvider = new MockProcessProvider();
        MockProvider.AddProcess(303, "duckstation-qt-x64-ReleaseLTCG", "DuckStation - Resident Evil 2 (USA) [SLUS-00748]");

        GameMatchResult? Match = Detector.DetectGame([Sh1DuckProfile], MockProvider.GetRunningProcesses());

        Assert.Null(Match);
    }

    [Fact]
    public void DetectGame_DuckStationIdleNoDisc_DoesNotMatch()
    {
        MockProcessProvider MockProvider = new MockProcessProvider();
        MockProvider.AddProcess(404, "duckstation-qt-x64-ReleaseLTCG", "DuckStation");

        GameMatchResult? Match = Detector.DetectGame([Sh1DuckProfile], MockProvider.GetRunningProcesses());

        Assert.Null(Match);
    }

    [Fact]
    public void DetectGame_MatchesWhenMainWindowTitleIsEmpty_ButChildWindowMatches()
    {
        MockProcessProvider MockProvider = new MockProcessProvider();
        MockProvider.AddProcess(505, "duckstation-qt-x64-ReleaseLTCG", "", "DuckStation - Silent Hill (USA) [SLUS-00898]");

        GameMatchResult? Match = Detector.DetectGame([Sh1DuckProfile], MockProvider.GetRunningProcesses());

        Assert.NotNull(Match);
        Assert.Equal(505, Match.ProcessId);
        Assert.Contains("Silent Hill (USA)", Match.WindowTitle);
    }

    [Fact]
    public void DetectGame_ExtractsDynamicDetailsPattern()
    {
        GameProfile DynamicProfile = new GameProfile
        {
            Identifier = "DYNAMIC_SH",
            Enabled = true,
            ProcessNames = ["pcsx2-qt"],
            TitlePattern = "Silent Hill",
            DynamicDetailsPattern = @"(Silent Hill \d?: [^\[]+)",
            DefaultDetailsText = "Fallback Details",
            DefaultStateText = "In Emulator"
        };

        MockProcessProvider MockProvider = new MockProcessProvider();
        MockProvider.AddProcess(606, "pcsx2-qt", "PCSX2 - Silent Hill 2: Director's Cut [SLES-51156] (60 FPS)");

        GameMatchResult? Match = Detector.DetectGame([DynamicProfile], MockProvider.GetRunningProcesses());

        Assert.NotNull(Match);
        Assert.Equal("Silent Hill 2: Director's Cut ", Match.Details);
        Assert.Equal("In Emulator", Match.State);
    }

    #endregion

    #region IsMatchStillActive Tests

    [Fact]
    public void IsMatchStillActive_ReturnsTrue_WhenProcessAliveAndTitleMatches()
    {
        MockProcessProvider MockProvider = new MockProcessProvider();
        MockProvider.AddProcess(202, "duckstation", "DuckStation - Silent Hill (USA)");

        bool IsStillActive = Detector.IsMatchStillActive(Sh1DuckProfile, 202, MockProvider, out GameMatchResult? UpdatedResult);

        Assert.True(IsStillActive);
        Assert.NotNull(UpdatedResult);
        Assert.Equal(202, UpdatedResult.ProcessId);
    }

    [Fact]
    public void IsMatchStillActive_ReturnsFalse_WhenProcessExited()
    {
        MockProcessProvider MockProvider = new MockProcessProvider();

        bool IsStillActive = Detector.IsMatchStillActive(Sh1DuckProfile, 202, MockProvider, out GameMatchResult? UpdatedResult);

        Assert.False(IsStillActive);
        Assert.Null(UpdatedResult);
    }

    [Fact]
    public void IsMatchStillActive_ReturnsFalse_WhenDuckStationSwitchesGame()
    {
        MockProcessProvider MockProvider = new MockProcessProvider();
        MockProvider.AddProcess(202, "duckstation", "DuckStation - Metal Gear Solid (USA)");

        bool IsStillActive = Detector.IsMatchStillActive(Sh1DuckProfile, 202, MockProvider, out GameMatchResult? UpdatedResult);

        Assert.False(IsStillActive);
        Assert.Null(UpdatedResult);
    }

    #endregion
}
