using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

public sealed class LocalContentActionFiles : IContentActionFiles
{
    public string Normalize(string path) => LocalTransferFiles.Normalize(path);
    public string Identity(string path) => LocalTransferFiles.Identity(path);
    public bool Exists(string path) => LocalTransferFiles.Exists(path);
    public bool Contains(string parent, string path) => LocalTransferFiles.Contains(parent, path);
    public IDisposable LockDirectory(string path) => LocalTransferFiles.LockDirectory(path);
    public IEnumerable<string> Enumerate(string directory) => Directory.EnumerateFileSystemEntries(directory);
    public void RenameKnown(string source, string destination, string identity, string parentIdentity) =>
        LocalTransferFiles.RenameKnown(source, destination, identity, parentIdentity);
    public byte[] EncodeShortcut(string target) => ShellShortcutFile.Encode(target);
}
