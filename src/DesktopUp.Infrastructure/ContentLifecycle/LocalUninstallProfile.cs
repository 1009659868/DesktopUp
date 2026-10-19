using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

// Maintenance opens only an existing profile and writes a local location list.
// It does not launch normal startup services or create a fresh database.
public sealed class LocalUninstallProfile(SqliteStateStore store, IUserDataPaths paths) : IUninstallProfile
{
    public bool OpenExisting()
    {
        string database = Path.Combine(paths.State, "desktopup.sqlite3");
        if (!File.Exists(database))
        {
            if (Directory.Exists(paths.State) && Directory.EnumerateFileSystemEntries(paths.State)
                .Any(p => File.Exists(p) || Directory.EnumerateFileSystemEntries(p).Any()))
                throw new IOException("配置库缺失但仍有历史记录；请先检查配置，未执行卸载。");
            return false;
        }
        if (new StateBackupRecovery(paths, () => true).HasPendingRecovery)
            throw new IOException("配置恢复尚未完成，停止卸载；原配置和文件保留。");
        var initialization = store.Initialize();
        if (!initialization.Succeeded) throw new IOException(initialization.Error);
        return true;
    }

    public void SaveReport(string outcome,IEnumerable<string> returned,IEnumerable<string> retained,IEnumerable<string> errors)
    {
        string directory=Path.Combine(paths.Root,"uninstall-reports");
        for(string? p=directory;p is not null;p=Path.GetDirectoryName(p))
            if((Directory.Exists(p)||File.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("位置清单路径被替换。");
        Directory.CreateDirectory(directory);
        using var file=new FileStream(Path.Combine(directory,$"contents-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.txt"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
        using var writer=new StreamWriter(file,System.Text.Encoding.UTF8);
        writer.WriteLine("DesktopUp 卸载内容位置清单（仅保存在本机）\n"+outcome+"\n配置保留："+paths.Root);
        writer.WriteLine("\n已归还位置：");foreach(string path in returned)writer.WriteLine(path);
        writer.WriteLine("\n原收纳目录（空目录也保留；映射/外部引用未搬动）：");foreach(string path in retained)writer.WriteLine(path);
        foreach(string error in errors)writer.WriteLine("\n未完成："+error);
    }
}
