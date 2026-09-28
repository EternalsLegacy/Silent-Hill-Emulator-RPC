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
    #region Properties

    /// <summary>
    /// Returns true if any window title (main or child/top-level) contains non-whitespace text.
    /// </summary>
    public bool HasAnyWindowTitle => 
        !string.IsNullOrWhiteSpace(MainWindowTitle) || 
        AllWindowTitles.Any(T => !string.IsNullOrWhiteSpace(T));

    /// <summary>
    /// Returns the most descriptive window title available.
    /// </summary>
    public string BestWindowTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(MainWindowTitle))
                return MainWindowTitle;

            return AllWindowTitles.FirstOrDefault(T => !string.IsNullOrWhiteSpace(T)) ?? string.Empty;
        }
    }

    #endregion
}
