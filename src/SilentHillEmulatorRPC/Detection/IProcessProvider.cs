namespace SilentHillEmulatorRPC.Detection;

/// <summary>
/// Abstraction for polling operating system processes.
/// Allows easy unit testing with mocked processes.
/// </summary>
public interface IProcessProvider
{
    #region Methods

    /// <summary>
    /// Returns snapshots of all currently running processes.
    /// </summary>
    IReadOnlyList<ProcessSnapshot> GetRunningProcesses();

    /// <summary>
    /// Gets a snapshot for a specific process ID, or null if the process is no longer running.
    /// </summary>
    ProcessSnapshot? GetProcessById(int ProcessId);

    /// <summary>
    /// Checks if a process with the specified ID is still alive.
    /// </summary>
    bool IsProcessAlive(int ProcessId);

    #endregion
}
