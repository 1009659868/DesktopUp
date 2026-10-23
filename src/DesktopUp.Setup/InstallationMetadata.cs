using Microsoft.Win32;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace DesktopUp.Setup;

internal sealed record RegistryValue(string Name, RegistryValueKind Kind, string[] Data);
internal interface IInstallRegistry
{
    List<RegistryValue>? Read(string release);
    void Write(string release, List<RegistryValue> values);
    void Remove(string release);
}

internal sealed class WindowsInstallRegistry : IInstallRegistry
{
    private const string Prefix = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopUp.Preview.";
    public List<RegistryValue>? Read(string release)
    {
        _ = InstallationFiles.Release(release);
        using var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var key = user.OpenSubKey(Prefix + release);
        if (key is null) return null;
        if (key.SubKeyCount != 0) throw new IOException("卸载登记有未知子项，未修改。");
        return key.GetValueNames().Order(StringComparer.Ordinal).Select(name =>
        {
            var kind = key.GetValueKind(name);
            object value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)!;
            string[] data = kind switch
            {
                RegistryValueKind.String or RegistryValueKind.ExpandString => [(string)value],
                RegistryValueKind.DWord => [((int)value).ToString(CultureInfo.InvariantCulture)],
                RegistryValueKind.QWord => [((long)value).ToString(CultureInfo.InvariantCulture)],
                RegistryValueKind.Binary => [Convert.ToBase64String((byte[])value)],
                RegistryValueKind.MultiString => (string[])value,
                _ => throw new IOException("不支持的卸载登记类型。")
            };
            return new RegistryValue(name, kind, data);
        }).ToList();
    }
    public void Write(string release, List<RegistryValue> values)
    {
        _ = InstallationFiles.Release(release);
        using var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var key = user.CreateSubKey(Prefix + release);
        foreach (var value in values)
        {
            object data = value.Kind switch
            {
                RegistryValueKind.String or RegistryValueKind.ExpandString => value.Data.Single(),
                RegistryValueKind.DWord => int.Parse(value.Data.Single(), CultureInfo.InvariantCulture),
                RegistryValueKind.QWord => long.Parse(value.Data.Single(), CultureInfo.InvariantCulture),
                RegistryValueKind.Binary => Convert.FromBase64String(value.Data.Single()),
                RegistryValueKind.MultiString => value.Data,
                _ => throw new IOException("不支持的卸载登记类型。")
            };
            key.SetValue(value.Name, data, value.Kind);
        }
        key.Flush();
    }
    public void Remove(string release)
    {
        _ = InstallationFiles.Release(release);
        using var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        user.DeleteSubKey(Prefix + release, throwOnMissingSubKey: false);
    }
}

internal interface IInstallationLinks
{
    string? Target(string path);
    void Create(string path, string target);
}
internal sealed class WindowsInstallationLinks : IInstallationLinks
{
    public string? Target(string path)
    {
        if (!File.Exists(path)) return null;
        InstallationFiles.Plain(path);
        return WithLink(path, link => (string)link.TargetPath);
    }
    public void Create(string path, string target) => WithLink(path, link =>
    {
        link.TargetPath = target;
        link.WorkingDirectory = Path.GetDirectoryName(target);
        link.Save(); return (string?)null;
    });
    private static string? WithLink(string path, Func<dynamic, string?> action)
    {
        object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", throwOnError: true)!)!;
        object? link = null;
        try { link = ((dynamic)shell).CreateShortcut(path); return action(link); }
        finally { if (link is not null) Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell); }
    }
}

internal sealed record InstallationLocations(string Desktop, string Programs)
{
    public static InstallationLocations Current => new(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        Environment.GetFolderPath(Environment.SpecialFolder.Programs));
    public string Resolve(string root, string release, FileChange file)
    {
        _ = InstallationFiles.Release(release);
        string product = "DesktopUp Preview " + release;
        string result = file.Area switch
        {
            "payload" => InstallationFiles.PayloadPath(root, file.Path),
            "desktop" when file.Path == "entry.lnk" => Path.Combine(Desktop, product + ".lnk"),
            "menu" when file.Path == "entry.lnk" => Path.Combine(Programs, product, "DesktopUp Preview.lnk"),
            "menu" when file.Path == "readme.lnk" => Path.Combine(Programs, product, "预览说明.lnk"),
            _ => throw new IOException("升级日志含未知入口。")
        };
        InstallationFiles.Plain(result); return result;
    }
}

internal static class InstallationMetadata
{
    public static string ReadIni(string path, string field)
    {
        InstallationFiles.Plain(path);
        var value = new StringBuilder(2048);
        if (GetPrivateProfileString("Install", field, "", value, value.Capacity, path) >= value.Capacity - 1)
            throw new IOException("安装标记过长。");
        return value.ToString();
    }
    public static bool Equal(List<RegistryValue>? a, List<RegistryValue>? b) => JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);
    public static string? Text(List<RegistryValue> values, string name) => values.SingleOrDefault(v => v.Name == name && v.Kind == RegistryValueKind.String)?.Data.Single();
    public static List<RegistryValue> Registration(string root, string release, long bytes) => new[]
    {
        new RegistryValue("DisplayName", RegistryValueKind.String, [$"DesktopUp Preview {release} (x64)"]),
        new("DisplayVersion", RegistryValueKind.String, [release]),
        new("InstallLocation", RegistryValueKind.String, [root]),
        new("UninstallString", RegistryValueKind.String, [$"\"{root}\\Uninstall.exe\""]),
        new("QuietUninstallString", RegistryValueKind.String, [$"\"{root}\\Uninstall.exe\" /S /CONTENT=keep"]),
        new("DisplayIcon", RegistryValueKind.String, [Path.Combine(root, "DesktopUp.exe")]),
        new("NoModify", RegistryValueKind.DWord, ["1"]), new("NoRepair", RegistryValueKind.DWord, ["1"]),
        new("EstimatedSize", RegistryValueKind.DWord, [Math.Ceiling(bytes / 1024d).ToString(CultureInfo.InvariantCulture)])
    }.OrderBy(v => v.Name, StringComparer.Ordinal).ToList();

    [DllImport("kernel32.dll", EntryPoint = "GetPrivateProfileStringW", CharSet = CharSet.Unicode)]
    private static extern int GetPrivateProfileString(string section, string key, string fallback, StringBuilder value, int size, string file);
}
