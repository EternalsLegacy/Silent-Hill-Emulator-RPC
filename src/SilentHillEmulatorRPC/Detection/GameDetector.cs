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

    private readonly ILogger<GameDetector> _logger;

    #endregion

    #region Constructor

    public GameDetector(ILogger<GameDetector> logger)
    {
        _logger = logger;
    }

    #endregion

    #region Public Methods

    public GameMatchResult? DetectGame(IReadOnlyList<GameProfile> profiles, IReadOnlyList<ProcessSnapshot> runningProcesses)
    {
        var enabledProfiles = profiles.Where(p => p.Enabled).ToList();
        if (enabledProfiles.Count == 0 || runningProcesses.Count == 0)
        {
            return null;
        }

        foreach (var profile in enabledProfiles)
        {
            var match = TryMatchProfile(profile, runningProcesses);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    public bool IsMatchStillActive(GameProfile profile, int processId, IProcessProvider processProvider, out GameMatchResult? updatedResult)
    {
        updatedResult = null;

        var snapshot = processProvider.GetProcessById(processId);
        if (snapshot == null)
        {
            return false;
        }

        // Verify process name still matches
        if (!MatchesProcessName(profile, snapshot.ProcessName))
        {
            return false;
        }

        // Verify window title if a pattern is required
        if (!string.IsNullOrWhiteSpace(profile.TitlePattern))
        {
            if (!MatchesTitlePattern(profile.TitlePattern, snapshot, out var matchedTitle, out var capturedGroups))
            {
                return false;
            }

            updatedResult = BuildMatchResult(profile, snapshot, matchedTitle, capturedGroups);
            return true;
        }

        updatedResult = BuildMatchResult(profile, snapshot, snapshot.BestWindowTitle, new Dictionary<string, string>());
        return true;
    }

    #endregion

    #region Private Matching Logic

    private GameMatchResult? TryMatchProfile(GameProfile profile, IReadOnlyList<ProcessSnapshot> processes)
    {
        foreach (var process in processes)
        {
            if (!MatchesProcessName(profile, process.ProcessName))
            {
                continue;
            }

            // If no title pattern required, match immediately
            if (string.IsNullOrWhiteSpace(profile.TitlePattern))
            {
                _logger.LogDebug("Matched profile '{Identifier}' on process '{ProcessName}' (PID {Pid}).",
                    profile.Identifier, process.ProcessName, process.Id);

                return BuildMatchResult(profile, process, process.BestWindowTitle, new Dictionary<string, string>());
            }

            // Window title check
            if (MatchesTitlePattern(profile.TitlePattern, process, out var matchedTitle, out var capturedGroups))
            {
                _logger.LogDebug("Matched profile '{Identifier}' on process '{ProcessName}' (PID {Pid}) with title '{Title}'.",
                    profile.Identifier, process.ProcessName, process.Id, matchedTitle);

                return BuildMatchResult(profile, process, matchedTitle, capturedGroups);
            }
        }

        return null;
    }

    private static bool MatchesProcessName(GameProfile profile, string processName)
    {
        var cleanProcessName = StripExe(processName);

        if (profile.ProcessNames.Count > 0)
        {
            return profile.ProcessNames.Any(name =>
                string.Equals(StripExe(name), cleanProcessName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(profile.ProcessName))
        {
            return string.Equals(StripExe(profile.ProcessName), cleanProcessName, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool MatchesTitlePattern(
        string pattern,
        ProcessSnapshot snapshot,
        out string matchedTitle,
        out Dictionary<string, string> capturedGroups)
    {
        matchedTitle = string.Empty;
        capturedGroups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Titles to check: first MainWindowTitle, then any other window titles
        var candidateTitles = new List<string>();
        if (!string.IsNullOrWhiteSpace(snapshot.MainWindowTitle))
        {
            candidateTitles.Add(snapshot.MainWindowTitle);
        }
        foreach (var title in snapshot.AllWindowTitles)
        {
            if (!candidateTitles.Contains(title, StringComparer.OrdinalIgnoreCase))
            {
                candidateTitles.Add(title);
            }
        }

        if (candidateTitles.Count == 0)
        {
            return false;
        }

        // Try regex match first
        Regex? regex = null;
        try
        {
            regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
        }
        catch
        {
            // Invalid regex, will fallback to substring check
        }

        foreach (var title in candidateTitles)
        {
            if (regex != null)
            {
                try
                {
                    var match = regex.Match(title);
                    if (match.Success)
                    {
                        matchedTitle = title;
                        foreach (var groupName in regex.GetGroupNames())
                        {
                            if (!string.IsNullOrEmpty(groupName) && groupName != "0")
                            {
                                capturedGroups[groupName] = match.Groups[groupName].Value;
                            }
                        }
                        return true;
                    }
                }
                catch
                {
                    // Regex timeout or evaluation error
                }
            }

            // Fallback substring check (case-insensitive)
            if (title.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                matchedTitle = title;
                return true;
            }
        }

        return false;
    }

    private static GameMatchResult BuildMatchResult(
        GameProfile profile,
        ProcessSnapshot snapshot,
        string matchedTitle,
        Dictionary<string, string> capturedGroups)
    {
        var rawDetails = profile.DefaultDetailsText ?? string.Empty;
        var rawState = profile.DefaultStateText ?? string.Empty;

        // Apply dynamic pattern extraction if specified
        if (!string.IsNullOrWhiteSpace(profile.DynamicDetailsPattern))
        {
            var extracted = ExtractRegexMatch(profile.DynamicDetailsPattern, matchedTitle);
            if (!string.IsNullOrWhiteSpace(extracted))
            {
                rawDetails = extracted;
            }
        }

        if (!string.IsNullOrWhiteSpace(profile.DynamicStatePattern))
        {
            var extracted = ExtractRegexMatch(profile.DynamicStatePattern, matchedTitle);
            if (!string.IsNullOrWhiteSpace(extracted))
            {
                rawState = extracted;
            }
        }

        var details = InterpolateTokens(rawDetails, profile, snapshot, matchedTitle, capturedGroups);
        var state = InterpolateTokens(rawState, profile, snapshot, matchedTitle, capturedGroups);
        var largeText = InterpolateTokens(profile.LargeImageText ?? profile.DisplayName, profile, snapshot, matchedTitle, capturedGroups);
        var smallText = InterpolateTokens(profile.SmallImageText ?? string.Empty, profile, snapshot, matchedTitle, capturedGroups);

        return new GameMatchResult(
            profile,
            snapshot.Id,
            snapshot.ProcessName,
            matchedTitle,
            details,
            state,
            profile.LargeImageKey,
            string.IsNullOrWhiteSpace(largeText) ? null : largeText,
            string.IsNullOrWhiteSpace(profile.SmallImageKey) ? null : profile.SmallImageKey,
            string.IsNullOrWhiteSpace(smallText) ? null : smallText
        );
    }

    private static string InterpolateTokens(
        string template,
        GameProfile profile,
        ProcessSnapshot snapshot,
        string matchedTitle,
        Dictionary<string, string> capturedGroups)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        var result = template
            .Replace("{Title}", matchedTitle, StringComparison.OrdinalIgnoreCase)
            .Replace("{ProcessName}", snapshot.ProcessName, StringComparison.OrdinalIgnoreCase)
            .Replace("{DisplayName}", profile.DisplayName, StringComparison.OrdinalIgnoreCase)
            .Replace("{Identifier}", profile.Identifier, StringComparison.OrdinalIgnoreCase);

        foreach (var (key, value) in capturedGroups)
        {
            result = result.Replace($"{{{key}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static string? ExtractRegexMatch(string pattern, string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        try
        {
            var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));
            if (match.Success)
            {
                return match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
            }
        }
        catch
        {
            // Regex error
        }

        return null;
    }

    private static string StripExe(string name)
    {
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return name[..^4];
        }
        return name;
    }

    #endregion
}
