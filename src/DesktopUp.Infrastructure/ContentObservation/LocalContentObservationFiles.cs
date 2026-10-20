using DesktopUp.Windows;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

// Read-only bounded enumeration and platform identity. No relationship or retry decisions.
public sealed class LocalContentObservationFiles : IContentObservationFiles
{
    internal Action<string>? Enumerating { get; set; }
    public string Identity(string path) => LocalTransferFiles.Identity(path);
    public string MappedItemIdentity(string path) => LocalTransferFiles.Identity(path) + ":" + File.GetCreationTimeUtc(path).Ticks;
    public IEnumerable<string> Enumerate(string directory) => Directory.EnumerateFileSystemEntries(directory);
    public string Normalize(string path) => LocalTransferFiles.Normalize(path);
    public IDisposable LockDirectory(string path) => LocalTransferFiles.LockDirectory(path);
    public bool Contains(string parent, string child) => LocalTransferFiles.Contains(parent, child);

    public MappedDirectoryScan ScanMapping(string input, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        string path = LocalTransferFiles.Normalize(input);
        using var root = LocalTransferFiles.LockDirectory(path);
        string identity = LocalTransferFiles.Identity(path);
        var timer = System.Diagnostics.Stopwatch.StartNew(); var facts = new List<MappedContentFact>();
        int count = 0;
        foreach (string entry in Directory.EnumerateFileSystemEntries(path))
        {
            token.ThrowIfCancellationRequested();
            if (++count > ContentLayoutPolicy.MaximumItems || timer.Elapsed > TimeSpan.FromSeconds(3)) throw new IOException("映射目录超过1000个直接子项或本轮读取预算，已保留原状态。");
            var attributes = File.GetAttributes(entry);
            if ((attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
            var preview = LocalContentReferenceProbe.Inspect(entry);
            var item = preview.Item ?? throw new IOException(preview.Message);
            string objectId = MappedItemIdentity(entry);
            facts.Add(new(item, objectId, $"{preview.Length}:{preview.LastWriteTicks}"));
        }
        return new(path, identity, facts.OrderBy(f => f.Item.Target.Locator, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public IReadOnlyDictionary<string, string> Inventory(string root, CancellationToken token)
    {
        var result = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        void Visit(string directory)
        {
            token.ThrowIfCancellationRequested(); Enumerating?.Invoke(directory);
            // Explicitly do not skip inaccessible entries or traverse links. Any incomplete tree defers.
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                if (elapsed.Elapsed > TimeSpan.FromSeconds(3)) throw new IOException("收纳集合核对超时，本轮保留关系。");
                if (result.Count >= LocalTransferFiles.MaximumTreeEntries) throw new IOException("收纳集合超过本轮核对预算。");
                var attributes = File.GetAttributes(entry);
                if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline | FileAttributes.Encrypted)) != 0)
                    throw new IOException("收纳集合包含不可安全核对的路径。");
                result.Add(entry, LocalTransferFiles.Identity(entry));
                if ((attributes & FileAttributes.Directory) != 0) Visit(entry);
            }
        }
        Visit(root); return result;
    }
}
