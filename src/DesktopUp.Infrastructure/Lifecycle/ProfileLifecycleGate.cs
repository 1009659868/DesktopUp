namespace DesktopUp.Infrastructure;

// The installer takes the same file exclusively for its whole deletion section. The
// ordinary app holds a readable lease for its lifetime; maintenance uses the instance mutex.
public static class ProfileLifecycleGate
{
    public const string FileName = "uninstall-session.lock";
    public static IDisposable Acquire(string root)
    {
        for (string? p=Path.GetFullPath(root);p is not null;p=Path.GetDirectoryName(p))
            if((Directory.Exists(p)||File.Exists(p)) && (File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)
                throw new IOException("配置路径为联接或符号链接，无法取得维护保护。");
        Directory.CreateDirectory(root);
        string path=Path.Combine(root,FileName);
        if(File.Exists(path) && (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("维护保护文件被替换。");
        return new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read);
    }
}
