namespace SilentHillEmulatorRPC.Detection;

/// <summary>
/// Immutable snapshot of a running system process and its window titles.
/// </summary>
public record ProcessSnapshot(
    int Id,
    string ProcessName,
    string MainWindowTitle,
    IReadOnlyList<string> AllWindowTitles
)
{
    /// <summary>
    /// Returns true if any window title (main or child/top-level) contains non-whitespace text.
    /// </summary>
    public bool HasAnyWindowTitle => 
        !string.IsNullOrWhiteSpace(MainWindowTitle) || 
        AllWindowTitles.Any(t => !string.IsNullOrWhiteSpace(t));

    /// <summary>
    /// Returns the most descriptive window title available.
    /// </summary>
    public string BestWindowTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(MainWindowTitle))
                return MainWindowTitle;

            return AllWindowTitles.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? string.Empty;
        }
    }
}
