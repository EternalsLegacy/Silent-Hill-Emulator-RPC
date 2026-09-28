using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SilentHillEmulatorRPC.Configuration;

namespace SilentHillEmulatorRPC.Detection;

/// <summary>
/// Implements matching logic for process names and window titles.
/// </summary>
public class GameDetector : IGameDetector
{
    #region Fields

    private readonly ILogger<GameDetector> Logger;

    #endregion

    #region Constructor

    public GameDetector(ILogger<GameDetector> Logger)
    {
        this.Logger = Logger;
    }

    #endregion

    #region Public Methods

    public GameMatchResult? DetectGame(IReadOnlyList<GameProfile> Profiles, IReadOnlyList<ProcessSnapshot> RunningProcesses)
    {
        List<GameProfile> EnabledProfiles = Profiles.Where(P => P.Enabled).ToList();
        if (EnabledProfiles.Count == 0 || RunningProcesses.Count == 0)
        {
            return null;
        }

        foreach (GameProfile Profile in EnabledProfiles)
        {
            GameMatchResult? Match = TryMatchProfile(Profile, RunningProcesses);
            if (Match != null)
            {
                return Match;
            }
        }

        return null;
    }

    public bool IsMatchStillActive(GameProfile Profile, int ProcessId, IProcessProvider ProcessProvider, out GameMatchResult? UpdatedResult)
    {
        UpdatedResult = null;

        ProcessSnapshot? Snapshot = ProcessProvider.GetProcessById(ProcessId);
        if (Snapshot == null)
        {
            return false;
        }

        if (!MatchesProcessName(Profile, Snapshot.ProcessName))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Profile.TitlePattern))
        {
            if (!MatchesTitlePattern(Profile.TitlePattern, Snapshot, out string MatchedTitle, out Dictionary<string, string> CapturedGroups))
            {
                return false;
            }

            UpdatedResult = BuildMatchResult(Profile, Snapshot, MatchedTitle, CapturedGroups);
            return true;
        }

        UpdatedResult = BuildMatchResult(Profile, Snapshot, Snapshot.BestWindowTitle, new Dictionary<string, string>());
        return true;
    }

    #endregion

    #region Private Matching Logic

    private GameMatchResult? TryMatchProfile(GameProfile Profile, IReadOnlyList<ProcessSnapshot> Processes)
    {
        foreach (ProcessSnapshot ProcessItem in Processes)
        {
            if (!MatchesProcessName(Profile, ProcessItem.ProcessName))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(Profile.TitlePattern))
            {
                Logger.LogDebug("Matched profile '{Identifier}' on process '{ProcessName}' (PID {Pid}).",
                    Profile.Identifier, ProcessItem.ProcessName, ProcessItem.Id);

                return BuildMatchResult(Profile, ProcessItem, ProcessItem.BestWindowTitle, new Dictionary<string, string>());
            }

            if (MatchesTitlePattern(Profile.TitlePattern, ProcessItem, out string MatchedTitle, out Dictionary<string, string> CapturedGroups))
            {
                Logger.LogDebug("Matched profile '{Identifier}' on process '{ProcessName}' (PID {Pid}) with title '{Title}'.",
                    Profile.Identifier, ProcessItem.ProcessName, ProcessItem.Id, MatchedTitle);

                return BuildMatchResult(Profile, ProcessItem, MatchedTitle, CapturedGroups);
            }
        }

        return null;
    }

    private static bool MatchesProcessName(GameProfile Profile, string ProcessName)
    {
        string CleanProcessName = StripExe(ProcessName);

        if (Profile.ProcessNames.Count > 0)
        {
            return Profile.ProcessNames.Any(Name =>
                string.Equals(StripExe(Name), CleanProcessName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(Profile.ProcessName))
        {
            return string.Equals(StripExe(Profile.ProcessName), CleanProcessName, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool MatchesTitlePattern(
        string Pattern,
        ProcessSnapshot Snapshot,
        out string MatchedTitle,
        out Dictionary<string, string> CapturedGroups)
    {
        MatchedTitle = string.Empty;
        CapturedGroups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        List<string> CandidateTitles = new List<string>();
        if (!string.IsNullOrWhiteSpace(Snapshot.MainWindowTitle))
        {
            CandidateTitles.Add(Snapshot.MainWindowTitle);
        }
        foreach (string Title in Snapshot.AllWindowTitles)
        {
            if (!CandidateTitles.Contains(Title, StringComparer.OrdinalIgnoreCase))
            {
                CandidateTitles.Add(Title);
            }
        }

        if (CandidateTitles.Count == 0)
        {
            return false;
        }

        Regex? CompiledRegex = null;
        try
        {
            CompiledRegex = new Regex(Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
        }
        catch
        {
            // Invalid regex, will fallback to substring check
        }

        foreach (string Title in CandidateTitles)
        {
            if (CompiledRegex != null)
            {
                try
                {
                    Match RegexMatch = CompiledRegex.Match(Title);
                    if (RegexMatch.Success)
                    {
                        MatchedTitle = Title;
                        foreach (string GroupName in CompiledRegex.GetGroupNames())
                        {
                            if (!string.IsNullOrEmpty(GroupName) && GroupName != "0")
                            {
                                CapturedGroups[GroupName] = RegexMatch.Groups[GroupName].Value;
                            }
                        }
                        return true;
                    }
                }
                catch
                {
                    // Regex evaluation timeout
                }
            }

            if (Title.Contains(Pattern, StringComparison.OrdinalIgnoreCase))
            {
                MatchedTitle = Title;
                return true;
            }
        }

        return false;
    }

    private static GameMatchResult BuildMatchResult(
        GameProfile Profile,
        ProcessSnapshot Snapshot,
        string MatchedTitle,
        Dictionary<string, string> CapturedGroups)
    {
        string RawDetails = Profile.DefaultDetailsText ?? string.Empty;
        string RawState = Profile.DefaultStateText ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(Profile.DynamicDetailsPattern))
        {
            string? Extracted = ExtractRegexMatch(Profile.DynamicDetailsPattern, MatchedTitle);
            if (!string.IsNullOrWhiteSpace(Extracted))
            {
                RawDetails = Extracted;
            }
        }

        if (!string.IsNullOrWhiteSpace(Profile.DynamicStatePattern))
        {
            string? Extracted = ExtractRegexMatch(Profile.DynamicStatePattern, MatchedTitle);
            if (!string.IsNullOrWhiteSpace(Extracted))
            {
                RawState = Extracted;
            }
        }

        string Details = InterpolateTokens(RawDetails, Profile, Snapshot, MatchedTitle, CapturedGroups);
        string State = InterpolateTokens(RawState, Profile, Snapshot, MatchedTitle, CapturedGroups);
        string LargeText = InterpolateTokens(Profile.LargeImageText ?? Profile.DisplayName, Profile, Snapshot, MatchedTitle, CapturedGroups);
        string SmallText = InterpolateTokens(Profile.SmallImageText ?? string.Empty, Profile, Snapshot, MatchedTitle, CapturedGroups);

        return new GameMatchResult(
            Profile,
            Snapshot.Id,
            Snapshot.ProcessName,
            MatchedTitle,
            Details,
            State,
            Profile.LargeImageKey,
            string.IsNullOrWhiteSpace(LargeText) ? null : LargeText,
            string.IsNullOrWhiteSpace(Profile.SmallImageKey) ? null : Profile.SmallImageKey,
            string.IsNullOrWhiteSpace(SmallText) ? null : SmallText
        );
    }

    private static string InterpolateTokens(
        string Template,
        GameProfile Profile,
        ProcessSnapshot Snapshot,
        string MatchedTitle,
        Dictionary<string, string> CapturedGroups)
    {
        if (string.IsNullOrEmpty(Template))
            return Template;

        string Result = Template
            .Replace("{Title}", MatchedTitle, StringComparison.OrdinalIgnoreCase)
            .Replace("{ProcessName}", Snapshot.ProcessName, StringComparison.OrdinalIgnoreCase)
            .Replace("{DisplayName}", Profile.DisplayName, StringComparison.OrdinalIgnoreCase)
            .Replace("{Identifier}", Profile.Identifier, StringComparison.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, string> Pair in CapturedGroups)
        {
            Result = Result.Replace($"{{{Pair.Key}}}", Pair.Value, StringComparison.OrdinalIgnoreCase);
        }

        return Result;
    }

    private static string? ExtractRegexMatch(string Pattern, string Input)
    {
        if (string.IsNullOrWhiteSpace(Input))
            return null;

        try
        {
            Match RegexMatch = Regex.Match(Input, Pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));
            if (RegexMatch.Success)
            {
                return RegexMatch.Groups.Count > 1 ? RegexMatch.Groups[1].Value : RegexMatch.Value;
            }
        }
        catch
        {
            // Regex error
        }

        return null;
    }

    private static string StripExe(string Name)
    {
        if (Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return Name[..^4];
        }
        return Name;
    }

    #endregion
}
