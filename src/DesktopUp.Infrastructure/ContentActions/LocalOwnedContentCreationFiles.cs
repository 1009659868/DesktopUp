using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

// The receipt retains the physical witness, not a copy of business state.
public sealed class LocalOwnedContentCreationFiles : IOwnedContentCreationFiles
{
    public IOwnedContentCreation Create(string path, NewContentKind kind, byte[] bytes)
    {
        var witness = kind == NewContentKind.Folder
            ? LocalTransferFiles.CreateOwnedDirectory(path)
            : LocalTransferFiles.CreateOwnedFile(path, bytes);
        return new Created(path, witness);
    }

    private sealed class Created(string path, LocalTransferObject witness) : IOwnedContentCreation
    {
        public IDisposable PinUnchanged()
        {
            var pin = new LocalTransferFiles.PinnedObject(path, delete: false);
            try
            {
                if (pin.Snapshot() != witness || witness.Directory && Directory.EnumerateFileSystemEntries(path).Any())
                    throw new IOException("新建内容已被外部改变，已保留，未提交新建结果。");
                return pin;
            }
            catch { pin.Dispose(); throw; }
        }
        public void RemoveIfUnchanged() => RemoveExactEmptyOrFile(path, witness);
    }

    internal static void RemoveExactEmptyOrFile(string path, LocalTransferObject expected)
    {
        try
        {
            if (!LocalTransferFiles.Exists(path)) return;
            using var pin = new LocalTransferFiles.PinnedObject(path, delete: true);
            if (pin.Snapshot() != expected || expected.Directory && Directory.EnumerateFileSystemEntries(path).Any()) return;
            pin.Delete();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* Preserve unknown ownership/bytes. */ }
    }
}
