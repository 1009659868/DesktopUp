namespace DesktopUp.Infrastructure;

public static partial class LocalTransferFiles
{
    internal static LocalTransferObject CreateOwnedFile(string path, byte[] bytes)
    {
        using var parents = LockParents(Normalize(path));
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough);
        stream.Write(bytes); stream.Flush(true);
        return SnapshotHandle(stream.SafeFileHandle, "");
    }
}
