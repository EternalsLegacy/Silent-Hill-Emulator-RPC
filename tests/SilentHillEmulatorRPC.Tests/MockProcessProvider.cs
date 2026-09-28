using SilentHillEmulatorRPC.Detection;

namespace SilentHillEmulatorRPC.Tests;

public class MockProcessProvider : IProcessProvider
{
    private readonly List<ProcessSnapshot> _processes = [];

    public void SetProcesses(IEnumerable<ProcessSnapshot> processes)
    {
        _processes.Clear();
        _processes.AddRange(processes);
    }

    public void AddProcess(int id, string name, string mainTitle, params string[] otherTitles)
    {
        var allTitles = new List<string>();
        if (!string.IsNullOrWhiteSpace(mainTitle)) allTitles.Add(mainTitle);
        allTitles.AddRange(otherTitles);

        _processes.Add(new ProcessSnapshot(id, name, mainTitle, allTitles));
    }

    public void RemoveProcess(int id)
    {
        _processes.RemoveAll(p => p.Id == id);
    }

    public void UpdateTitle(int id, string newTitle)
    {
        var existing = _processes.FirstOrDefault(p => p.Id == id);
        if (existing != null)
        {
            _processes.Remove(existing);
            _processes.Add(new ProcessSnapshot(id, existing.ProcessName, newTitle, [newTitle]));
        }
    }

    public IReadOnlyList<ProcessSnapshot> GetRunningProcesses() => _processes.AsReadOnly();

    public ProcessSnapshot? GetProcessById(int processId) => _processes.FirstOrDefault(p => p.Id == processId);

    public bool IsProcessAlive(int processId) => _processes.Any(p => p.Id == processId);
}
