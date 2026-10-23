using System.Diagnostics;
using System.Text;

namespace DesktopUp.Setup;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args is not ["--upgrade", _, _, _, _] and not ["--recover", _, _]) return 2;
        string report = args[^1];
        try
        {
            string root = InstallationFiles.Root(args[0] == "--upgrade" ? args[2] : args[1]);
            string profile = Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT") ??
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "Preview");
            InstallationFiles.Plain(profile); Directory.CreateDirectory(profile);
            string gate = Path.Combine(profile, "uninstall-session.lock"); InstallationFiles.Plain(gate);
            // Same protocol as ProfileLifecycleGate and the uninstaller. Held through rollback/cleanup.
            using var lease = new FileStream(gate, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            foreach (var process in Process.GetProcessesByName("DesktopUp"))
                using (process)
                {
                    string? executable = process.MainModule?.FileName;
                    if (executable is null || InstallationFiles.Within(executable, root))
                        throw new IOException("请先从托盘正常退出旧版，再执行升级。不会强制结束进程。");
                }
            var transaction = new UpgradeTransaction(new WindowsInstallRegistry(), new WindowsInstallationLinks(), InstallationLocations.Current);
            string message;
            if (args[0] == "--recover") { transaction.Recover(root); message = "已完成上一轮恢复/收尾。如已恢复旧版，可重新运行安装包；若新版已安装，则可正常使用。"; }
            else
            {
                if (args[3] is not ("0" or "1")) throw new IOException("无效桌面入口选项。");
                message = transaction.Execute(args[1], root, args[3] == "1");
            }
            Report(report, message); return 0;
        }
        catch (Exception e)
        {
            try { Report(report, e.Message); } catch { }
            return e is PendingUpgradeException ? 42 : 41;
        }
    }
    private static void Report(string path, string message)
    {
        // The NSIS plugin directory disappears on exit. Keep a distinct diagnostic
        // outside it; never overwrite first-failure evidence or open the database.
        try
        {
            string profile = Environment.GetEnvironmentVariable("DESKTOPUP_V3_TEST_DATA_ROOT") ??
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp", "Preview");
            string reports = Path.Combine(profile, "install-reports"); InstallationFiles.Plain(reports);
            Directory.CreateDirectory(reports);
            string diagnostic = Path.Combine(reports, $"upgrade-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.txt");
            using (var file = new FileStream(diagnostic, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(file, new UTF8Encoding(false))) writer.Write(message);
            message += " 报告：" + diagnostic;
        }
        catch { /* The transaction journal and caller report remain the recovery authority. */ }
        InstallationFiles.Plain(path);
        File.WriteAllText(path, "[Upgrade]\r\nMessage=" + message.Replace('\r', ' ').Replace('\n', ' ') + "\r\n", Encoding.Unicode);
    }
}
