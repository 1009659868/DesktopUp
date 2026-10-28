namespace DesktopUp.Infrastructure;

public static partial class LocalTransferFiles
{
    // Same-parent identity-checked rename; no copy, overwrite, or recursive content read.
    internal static void RenameKnown(string source, string destination, string identity, string parentIdentity)
    {
        source = Normalize(source); destination = Normalize(destination);
        if (!string.Equals(Path.GetDirectoryName(source), Path.GetDirectoryName(destination), StringComparison.OrdinalIgnoreCase))
            throw new IOException("改名不能更换所在目录。");
        using var parents = LockParents(source);
        using var parent = Open(Path.GetDirectoryName(source)!, false, attributesOnly: true);
        if (Id(parent) != parentIdentity) throw new IOException("所在目录已变化，未改名。");
        using var file = Open(source, true);
        if (Id(file) != identity) throw new IOException("对象已被替换，未改名。");
        if (!string.Equals(source, destination, StringComparison.OrdinalIgnoreCase) && Exists(destination))
            throw new IOException("同名对象已存在，未覆盖。");
        Rename(file, destination);
    }
}
