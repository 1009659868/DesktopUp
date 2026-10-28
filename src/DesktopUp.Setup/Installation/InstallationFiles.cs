using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DesktopUp.Setup;

internal sealed record PayloadFile(string Path, long Length, string SHA256);
internal sealed record FileChange(string Area, string Path, string SHA256);

// This helper has no dependency on the app, content worker, database or file-return use cases.
internal static partial class InstallationFiles
{
    public const string WorkPrefix = ".desktopup-upgrade-";
    public static string Digest(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static Version Release(string value) => ReleasePattern().IsMatch(value) && Version.TryParse(value, out var result)
        ? result : throw new IOException("无效的安装版本。");

    public static string Root(string path)
    {
        string full = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));
        if (!System.IO.Path.IsPathFullyQualified(path) || full.StartsWith(@"\\") || full.Length is < 4 or > 180)
            throw new IOException("安装目录必须是明确的本地子目录。");
        string data = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopUp");
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (Within(full, data) || Within(full, windows)) throw new IOException("不能替换配置或系统目录。");
        foreach (var folder in new[] { Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.DesktopDirectory,
            Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
            if (Same(full, Environment.GetFolderPath(folder))) throw new IOException("不能替换用户根目录。");
        Plain(full);
        return full;
    }

    public static bool Same(string a, string b) => string.Equals(System.IO.Path.TrimEndingDirectorySeparator(a),
        System.IO.Path.TrimEndingDirectorySeparator(b), StringComparison.OrdinalIgnoreCase);
    public static bool Within(string path, string parent) => Same(path, parent) || path.StartsWith(parent + "\\", StringComparison.OrdinalIgnoreCase);

    public static void Plain(string path)
    {
        for (string? p = System.IO.Path.GetFullPath(path); p is not null; p = System.IO.Path.GetDirectoryName(p))
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("路径经过联接或符号链接，未修改安装。");
    }

    public static string PayloadPath(string root, string relative)
    {
        string[] parts = relative.Split('/');
        if (parts.Length == 0 || parts.Any(p => p.Length == 0 || p is "." or ".." || p.EndsWith(' ') || p.EndsWith('.') ||
            p.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0) ||
            (parts.Length == 1 ? relative is not ("DesktopUp.exe" or "Uninstall.exe" or "PREVIEW-README.txt") : parts[0] is not ("app" or "licenses")))
            throw new IOException("安装清单包含非法或非程序路径。");
        string path = System.IO.Path.Combine(root, relative.Replace('/', '\\'));
        Plain(path);
        return path;
    }

    public static List<FileChange> ReadPayload(string root, bool installed)
    {
        string manifest = PayloadPath(root, "app/payload-manifest.json");
        if (new FileInfo(manifest).Length > 8 * 1024 * 1024) throw new IOException("安装清单过大。");
        var entries = JsonSerializer.Deserialize<List<PayloadFile>>(File.ReadAllText(manifest)) ?? throw new IOException("安装清单为空。");
        if (entries.Count is < 4 or > 20000) throw new IOException("安装清单范围无效。");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<FileChange>();
        foreach (var entry in entries)
        {
            string path = PayloadPath(root, entry.Path);
            if (!seen.Add(entry.Path) || entry.Path is "Uninstall.exe" or "app/preview-install.ini" or "app/payload-manifest.json")
                throw new IOException("安装清单重复或含动态登记文件。");
            if (!File.Exists(path) || new FileInfo(path).Length != entry.Length || Digest(path) != entry.SHA256)
                throw new IOException("程序文件缺失或已变化：" + path + "。未覆盖，请保留现场。");
            result.Add(new("payload", entry.Path, entry.SHA256));
        }
        foreach (string required in new[] { "DesktopUp.exe", "app/DesktopUp.exe", "app/preview-build.json" })
            if (!seen.Contains(required)) throw new IOException("安装清单缺少必要程序文件。");
        foreach (string relative in installed
            ? new[] { "app/payload-manifest.json", "app/preview-install.ini", "Uninstall.exe" }
            : new[] { "app/payload-manifest.json", "Uninstall.exe" })
            result.Add(new("payload", relative, Digest(PayloadPath(root, relative))));
        return result;
    }

    public static void RequireHash(string path, string hash)
    {
        Plain(path);
        if (!File.Exists(path) || Digest(path) != hash) throw new IOException("文件已变化，停止处理：" + path);
    }

    public static void CopyVerified(string from, string to, string hash)
    {
        Plain(from); Plain(to);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(to)!);
        File.Copy(from, to, overwrite: false);
        RequireHash(to, hash);
    }

    [GeneratedRegex(@"^\d{8}\.\d{1,8}$", RegexOptions.CultureInvariant)] private static partial Regex ReleasePattern();
}
