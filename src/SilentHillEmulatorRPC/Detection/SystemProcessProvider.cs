using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace SilentHillEmulatorRPC.Detection;

/// <summary>
/// Default implementation of IProcessProvider that queries the Windows/OS process table.
/// </summary>
public class SystemProcessProvider : IProcessProvider
{
    #region Fields

    private readonly ILogger<SystemProcessProvider> _logger;

    #endregion

    #region Constructor

    public SystemProcessProvider(ILogger<SystemProcessProvider> logger)
    {
        _logger = logger;
    }

    #endregion

    #region Public Methods

    public IReadOnlyList<ProcessSnapshot> GetRunningProcesses()
    {
        var snapshots = new List<ProcessSnapshot>();
        Process[] processes;

        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve system processes.");
            return snapshots;
        }

        foreach (var process in processes)
        {
            try
            {
                var snapshot = CreateSnapshot(process);
                if (snapshot != null)
                {
                    snapshots.Add(snapshot);
                }
            }
            finally
            {
                process.Dispose();
            }
        }

        return snapshots;
    }

    public ProcessSnapshot? GetProcessById(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.HasExited)
            {
                return null;
            }

            process.Refresh();
            return CreateSnapshot(process);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Could not get process snapshot for PID {ProcessId}.", processId);
            return null;
        }
    }

    public bool IsProcessAlive(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Private Methods

    private ProcessSnapshot? CreateSnapshot(Process process)
    {
        try
        {
            var name = process.ProcessName;
            var mainTitle = string.Empty;

            try
            {
                mainTitle = process.MainWindowTitle;
            }
            catch
            {
                // Can happen on protected/system processes
            }

            var allTitles = new List<string>();
            if (!string.IsNullOrWhiteSpace(mainTitle))
            {
                allTitles.Add(mainTitle);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var additionalTitles = GetWindowsForProcess(process.Id);
                foreach (var title in additionalTitles)
                {
                    if (!allTitles.Contains(title, StringComparer.OrdinalIgnoreCase))
                    {
                        allTitles.Add(title);
                    }
                }
            }

            return new ProcessSnapshot(process.Id, name, mainTitle, allTitles);
        }
        catch (Exception)
        {
            return null;
        }
    }

    #endregion

    #region Win32 Window Enumeration

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hWnd);

    private static List<string> GetWindowsForProcess(int targetPid)
    {
        var titles = new List<string>();

        try
        {
            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                GetWindowThreadProcessId(hWnd, out var windowPid);
                if (windowPid == targetPid)
                {
                    var sb = new StringBuilder(512);
                    if (GetWindowText(hWnd, sb, sb.Capacity) > 0)
                    {
                        var title = sb.ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(title) && !titles.Contains(title, StringComparer.OrdinalIgnoreCase))
                        {
                            titles.Add(title);
                        }
                    }
                }

                return true;
            }, nint.Zero);
        }
        catch
        {
            // Ignore Win32 enumeration errors
        }

        return titles;
    }

    #endregion
}
