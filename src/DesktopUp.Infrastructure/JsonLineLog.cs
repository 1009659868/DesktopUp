using System.Text.Json;
using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

public sealed class JsonLineLog : IStructuredLog
{
    private readonly object _gate = new();
    private readonly string _path;

    public JsonLineLog(IUserDataPaths paths)
    {
        paths.EnsureCreated();
        _path = Path.Combine(paths.Logs, "desktopup.jsonl");
    }

    public void Write(string level, string eventName, object? data = null)
    {
        string line = JsonSerializer.Serialize(new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            level,
            eventName,
            processId = Environment.ProcessId,
            data
        });

        lock (_gate)
        {
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }
}
