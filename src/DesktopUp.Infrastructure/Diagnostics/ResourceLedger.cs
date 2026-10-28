using System.Text.Json;
using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

public sealed class ResourceLedger : IResourceLedger
{
    private readonly object _gate = new();
    private readonly Dictionary<string, LedgerEntry> _active = new(StringComparer.Ordinal);
    private readonly string _path;

    public ResourceLedger(IUserDataPaths paths)
    {
        paths.EnsureCreated();
        _path = Path.Combine(paths.State, "resource-ledger.json");
        WriteSnapshot();
    }

    public IReadOnlyCollection<string> ActiveResources
    {
        get
        {
            lock (_gate)
            {
                return _active.Keys.ToArray();
            }
        }
    }

    public IDisposable Acquire(string resourceId, string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        lock (_gate)
        {
            if (!_active.TryAdd(resourceId, new LedgerEntry(kind, DateTimeOffset.UtcNow)))
            {
                throw new InvalidOperationException($"Resource '{resourceId}' is already active.");
            }
            WriteSnapshotCore();
        }

        return new Lease(this, resourceId);
    }

    public void WriteSnapshot()
    {
        lock (_gate)
        {
            WriteSnapshotCore();
        }
    }

    private void Release(string resourceId)
    {
        lock (_gate)
        {
            _active.Remove(resourceId);
            WriteSnapshotCore();
        }
    }

    private void WriteSnapshotCore()
    {
        var snapshot = new
        {
            processId = Environment.ProcessId,
            updatedAtUtc = DateTimeOffset.UtcNow,
            clean = _active.Count == 0,
            resources = _active.Select(static pair => new
            {
                id = pair.Key,
                kind = pair.Value.Kind,
                acquiredAtUtc = pair.Value.AcquiredAtUtc
            }).ToArray()
        };

        string temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        File.Move(temporaryPath, _path, overwrite: true);
    }

    private sealed record LedgerEntry(string Kind, DateTimeOffset AcquiredAtUtc);

    private sealed class Lease(ResourceLedger owner, string resourceId) : IDisposable
    {
        private ResourceLedger? _owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Release(resourceId);
        }
    }
}
