using Microsoft.Win32;

namespace DesktopUp.Setup;

internal sealed record ExistingInstallation(string Id, string Root, string Release, List<RegistryValue> Registration);

internal static class InstallationIdentity
{
    public static ExistingInstallation? Discover(IInstallRegistry registry)
    {
        var ids = registry.Ids();
        if (ids.Count > 1) throw new IOException("当前用户有多份 DesktopUp 安装登记；请先核对并处理旧安装，不能继续新增或任意选择一份：" + string.Join(", ", ids));
        if (ids.Count == 0) return null;
        string id = ids[0];
        var values = registry.Read(id) ?? throw new IOException("安装登记在读取期间消失。");
        string root = InstallationFiles.Root(InstallationMetadata.Text(values, "InstallLocation") ?? "");
        string release = InstallationMetadata.Text(values, "DisplayVersion") ?? "";
        _ = InstallationFiles.Release(release);
        string marker = InstallationFiles.PayloadPath(root, "app/preview-install.ini");
        if ((id != WindowsInstallRegistry.StableId && id != release) ||
            InstallationMetadata.Text(values, "UninstallString") != $"\"{root}\\Uninstall.exe\"" ||
            InstallationMetadata.ReadIni(marker, "Channel") != "DesktopUp.Preview" ||
            InstallationMetadata.ReadIni(marker, "Release") != release ||
            !InstallationFiles.Same(InstallationMetadata.ReadIni(marker, "Path"), root))
            throw new IOException("安装登记与标记不一致，未覆盖或新增安装：" + root);
        return new(id, root, release, values);
    }
}

internal interface IInstallationStartup
{
    string? Read();
    void Replace(string? expected, string? next);
}

// Only the documented Run value is updated. StartupApproved is intentionally untouched.
internal sealed class WindowsInstallationStartup : IInstallationStartup
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public string? Read()
    {
        using var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var run = user.OpenSubKey(Key);
        if (run is null || !run.GetValueNames().Contains("DesktopUp", StringComparer.OrdinalIgnoreCase)) return null;
        if (run.GetValueKind("DesktopUp") != RegistryValueKind.String) throw new IOException("自启动登记类型异常，未修改。");
        return (string?)run.GetValue("DesktopUp", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
    }
    public void Replace(string? expected, string? next)
    {
        if (Read() != expected) throw new IOException("自启动设置已变化，未覆盖。");
        if (expected == next) return;
        using var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var run = next is null ? user.OpenSubKey(Key, true) : user.CreateSubKey(Key);
        if (next is null) run?.DeleteValue("DesktopUp", false);
        else run!.SetValue("DesktopUp", next, RegistryValueKind.String);
        run?.Flush();
        if (Read() != next) throw new IOException("自启动登记未完成。");
    }
}
