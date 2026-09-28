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

    private readonly ILogger<SystemProcessProvider> Logger;

    #endregion

    #region Constructor

    public SystemProcessProvider(ILogger<SystemProcessProvider> Logger)
    {
        this.Logger = Logger;
    }

    #endregion

    #region Public Methods

    public IReadOnlyList<ProcessSnapshot> GetRunningProcesses()
    {
        List<ProcessSnapshot> Snapshots = new List<ProcessSnapshot>();
        Process[] Processes;

        try
        {
            Processes = Process.GetProcesses();
        }
        catch (Exception Ex)
        {
            Logger.LogError(Ex, "Failed to retrieve system processes.");
            return Snapshots;
        }

        foreach (Process ProcessItem in Processes)
        {
            try
            {
                ProcessSnapshot? Snapshot = CreateSnapshot(ProcessItem);
                if (Snapshot != null)
                {
                    Snapshots.Add(Snapshot);
                }
            }
            finally
            {
                ProcessItem.Dispose();
            }
        }

        return Snapshots;
    }

    public ProcessSnapshot? GetProcessById(int ProcessId)
    {
        try
        {
            using Process TargetProcess = Process.GetProcessById(ProcessId);
            if (TargetProcess.HasExited)
            {
                return null;
            }

            TargetProcess.Refresh();
            return CreateSnapshot(TargetProcess);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (Exception Ex)
        {
            Logger.LogTrace(Ex, "Could not get process snapshot for PID {ProcessId}.", ProcessId);
            return null;
        }
    }

    public bool IsProcessAlive(int ProcessId)
    {
        try
        {
            using Process TargetProcess = Process.GetProcessById(ProcessId);
            return !TargetProcess.HasExited;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Private Methods

    private ProcessSnapshot? CreateSnapshot(Process TargetProcess)
    {
        try
        {
            string ProcessName = TargetProcess.ProcessName;
            string MainTitle = string.Empty;

            try
            {
                MainTitle = TargetProcess.MainWindowTitle;
            }
            catch
            {
                // Can happen on protected/system processes
            }

            List<string> AllTitles = new List<string>();
            if (!string.IsNullOrWhiteSpace(MainTitle))
            {
                AllTitles.Add(MainTitle);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                List<string> AdditionalTitles = GetWindowsForProcess(TargetProcess.Id);
                foreach (string Title in AdditionalTitles)
                {
                    if (!AllTitles.Contains(Title, StringComparer.OrdinalIgnoreCase))
                    {
                        AllTitles.Add(Title);
                    }
                }
            }

            return new ProcessSnapshot(TargetProcess.Id, ProcessName, MainTitle, AllTitles);
        }
        catch (Exception)
        {
            return null;
        }
    }

    #endregion

    #region Win32 Window Enumeration

    private delegate bool EnumWindowsProc(nint HWnd, nint LParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc LpEnumFunc, nint LParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint HWnd, out uint LpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(nint HWnd, StringBuilder LpString, int NMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint HWnd);

    private static List<string> GetWindowsForProcess(int TargetPid)
    {
        List<string> Titles = new List<string>();

        try
        {
            EnumWindows((HWnd, _) =>
            {
                if (!IsWindowVisible(HWnd))
                    return true;

                GetWindowThreadProcessId(HWnd, out uint WindowPid);
                if (WindowPid == TargetPid)
                {
                    StringBuilder Sb = new StringBuilder(512);
                    if (GetWindowText(HWnd, Sb, Sb.Capacity) > 0)
                    {
                        string Title = Sb.ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(Title) && !Titles.Contains(Title, StringComparer.OrdinalIgnoreCase))
                        {
                            Titles.Add(Title);
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

        return Titles;
    }

    #endregion
}
