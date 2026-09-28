using SilentHillEmulatorRPC.Detection;

namespace SilentHillEmulatorRPC.Tests;

public class MockProcessProvider : IProcessProvider
{
    #region Fields

    private readonly List<ProcessSnapshot> Processes = [];

    #endregion

    #region Mock Setup Methods

    public void SetProcesses(IEnumerable<ProcessSnapshot> ProcessSnapshots)
    {
        Processes.Clear();
        Processes.AddRange(ProcessSnapshots);
    }

    public void AddProcess(int Id, string Name, string MainTitle, params string[] OtherTitles)
    {
        List<string> AllTitles = new List<string>();
        if (!string.IsNullOrWhiteSpace(MainTitle)) AllTitles.Add(MainTitle);
        allTitlesAddRange(OtherTitles);

        void allTitlesAddRange(string[] Titles)
        {
            AllTitles.AddRange(Titles);
        }

        Processes.Add(new ProcessSnapshot(Id, Name, MainTitle, AllTitles));
    }

    public void RemoveProcess(int Id)
    {
        Processes.RemoveAll(P => P.Id == Id);
    }

    public void UpdateTitle(int Id, string NewTitle)
    {
        ProcessSnapshot? Existing = Processes.FirstOrDefault(P => P.Id == Id);
        if (Existing != null)
        {
            Processes.Remove(Existing);
            Processes.Add(new ProcessSnapshot(Id, Existing.ProcessName, NewTitle, [NewTitle]));
        }
    }

    #endregion

    #region IProcessProvider Implementation

    public IReadOnlyList<ProcessSnapshot> GetRunningProcesses() => Processes.AsReadOnly();

    public ProcessSnapshot? GetProcessById(int ProcessId) => Processes.FirstOrDefault(P => P.Id == ProcessId);

    public bool IsProcessAlive(int ProcessId) => Processes.Any(P => P.Id == ProcessId);

    #endregion
}
