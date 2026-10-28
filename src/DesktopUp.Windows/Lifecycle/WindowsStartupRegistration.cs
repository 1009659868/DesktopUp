using DesktopUp.Application;
using Microsoft.Win32;

namespace DesktopUp.Windows;

// HKCU Run is documented for unpackaged applications. Do not decode/write the
// private StartupApproved format or equate registration with Windows approval.
public sealed class WindowsStartupRegistration(string appDirectory, bool installedBuild, bool profileOverride)
    : IStartupRegistrationBackend
{
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "DesktopUp";
    public InstalledStartupTarget ResolveInstallation()
    {
        if (!installedBuild || profileOverride)
            throw new IOException("仅正式安装入口可设置自启动；开发、便携及临时配置入口不登记。");
        string app = Path.TrimEndingDirectorySeparator(Path.GetFullPath(appDirectory));
        string root = Path.GetDirectoryName(app) ?? throw new IOException("安装位置无效。");
        if (!Path.GetFileName(app).Equals("app", StringComparison.OrdinalIgnoreCase)) throw new IOException("安装目录结构无效。");
        string marker = Path.Combine(app, "preview-install.ini");
        CheckPlainPath(marker);
        if (!File.Exists(marker) || new FileInfo(marker).Length > 16384) throw new IOException("未找到有效安装标记。");
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); bool install = false;
        foreach (string raw in File.ReadAllLines(marker))
        {
            string line = raw.Trim();
            if (line.StartsWith('[')) { install = line.Equals("[Install]", StringComparison.OrdinalIgnoreCase); continue; }
            if (!install || line.Length == 0 || line.StartsWith(';')) continue;
            int equals = line.IndexOf('=');
            if (equals <= 0 || !fields.TryAdd(line[..equals].Trim(), line[(equals + 1)..].Trim()))
                throw new IOException("安装标记字段无效。");
        }
        if (fields.GetValueOrDefault("Channel") != "DesktopUp.Preview" ||
            !fields.TryGetValue("Release", out string? release) || release.Length is < 1 or > 64 ||
            release.Any(c => !char.IsAsciiDigit(c) && c != '.') ||
            !SamePath(fields.GetValueOrDefault("Path"), root)) throw new IOException("安装标记与运行位置不一致。");
        using var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var registration = user.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp") ??
            user.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp.Preview." + release);
        if (!SamePath(registration?.GetValue("InstallLocation") as string, root) ||
            registration?.GetValue("DisplayVersion") as string != release)
            throw new IOException("当前用户的安装登记与运行位置不一致；未修改启动项。");
        foreach (string file in new[] { Path.Combine(root, "DesktopUp.exe"), Path.Combine(root, "Uninstall.exe"), Path.Combine(app, "DesktopUp.exe") })
        { CheckPlainPath(file); if (!File.Exists(file)) throw new IOException("安装文件不完整。"); }
        return new(Path.Combine(root, "DesktopUp.exe"));
    }
    public StartupEntry Read()
    {
        using var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var run = user.OpenSubKey(RunKey);
        return Read(run);
    }
    private static StartupEntry Read(RegistryKey? key)
    {
        if (key is null || !key.GetValueNames().Contains(ValueName, StringComparer.OrdinalIgnoreCase)) return new(false, null);
        return new(true, key.GetValueKind(ValueName) == RegistryValueKind.String
            ? key.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string : null);
    }
    public bool TryReplace(StartupEntry expected, string? command)
    {
        // Serialize our own writers. Never create a key while disabling or just querying.
        var target = ResolveInstallation();
        if ((command is not null && !string.Equals(command, target.Command, StringComparison.OrdinalIgnoreCase)) ||
            (expected.Exists && !string.Equals(expected.Command, target.Command, StringComparison.OrdinalIgnoreCase))) return false;
        using var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var run = command is null ? user.OpenSubKey(RunKey, writable: true) : user.CreateSubKey(RunKey);
        if (Read(run) != expected) return false;
        if (command is null) run?.DeleteValue(ValueName, throwOnMissingValue: false);
        else run!.SetValue(ValueName, command, RegistryValueKind.String);
        run?.Flush(); return true;
    }
    private static bool SamePath(string? path, string expected) => !string.IsNullOrWhiteSpace(path) &&
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)).Equals(expected, StringComparison.OrdinalIgnoreCase);
    private static void CheckPlainPath(string path)
    {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("安装路径经过重解析点，不能登记自启动。");
    }
}
