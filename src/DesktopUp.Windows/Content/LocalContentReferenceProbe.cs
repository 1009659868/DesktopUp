using DesktopUp.Domain;
using DesktopUp.Application;

namespace DesktopUp.Windows;

// Metadata only; never opens/executes targets, follows shortcuts, enumerates directories or moves files.
// The GUID is durable identity. Path equality is a conservative reuse hint, NOT file-ID equivalence.
public static class LocalContentReferenceProbe
{
    public static LocalReferencePreview Inspect(string input, Guid? id = null)
    {
        try
        {
            if (!LocalContentCompatibility.TryNormalize(input.Trim().Trim('"'), true, out string path, out string reason))
                return new(input, null, reason);
            var drive = new DriveInfo(Path.GetPathRoot(path)!);
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                return new(input, null, "此入口暂不访问网络或非本地驱动器。");
            // Walk outward-in: reject a linked ancestor before asking for metadata below it.
            string ancestor = Path.GetPathRoot(path)!;
            var segments = path[ancestor.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments.Take(Math.Max(0, segments.Length - 1)))
            {
                ancestor = Path.Combine(ancestor, segment);
                if (!LocalContentCompatibility.IsMaterialized(File.GetAttributes(ancestor)))
                    return new(input, null, "路径经过重解析或离线目录，此入口暂不跟随。");
            }
            var attributes = File.GetAttributes(path);
            if (!LocalContentCompatibility.IsMaterialized(attributes))
                return new(input, null, "重解析点或离线占位暂不跟随、不下载，请使用已落地的本机内容。");
            bool folder = attributes.HasFlag(FileAttributes.Directory);
            ContentKind kind = LocalContentCompatibility.Kind(path, attributes);
            bool shortcut = kind == ContentKind.Shortcut;
            // A selected .exe is still a file reference, not proof of an installed/Shell application identity.
            FileSystemInfo info = folder ? new DirectoryInfo(path) : new FileInfo(path);
            string name = info.Name.Length == 0 ? path : info.Name;
            var item = new ContentItem(id ?? Guid.NewGuid(), kind, new("local-file-reference-v1", path),
                new(shortcut ? ContentTargetKind.Shortcut : ContentTargetKind.FileSystem, path), name,
                ContentCapability.Reference | ContentCapability.Open | ContentCapability.Reveal);
            return new(input, item, shortcut ? "引用快捷方式本身；目标/参数在打开阶段解析，本次不执行。" : folder ? "引用文件夹；不扫描子项。" : "仅添加引用；原文件不动。",
                info is FileInfo file ? file.Length : null, info.LastWriteTimeUtc.Ticks);
        }
        catch (UnauthorizedAccessException) { return new(input, null, "无权读取来源，已跳过。"); }
        catch (FileNotFoundException) { return new(input, null, "来源不存在，可在找到文件后重试或修复已有引用。"); }
        catch (DirectoryNotFoundException) { return new(input, null, "目录不存在，已跳过。"); }
        catch (Exception e) when (e is IOException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        { return new(input, null, "来源暂不可用，未添加：" + e.Message); }
    }

    public static bool IsCurrent(LocalReferencePreview preview)
    {
        if (preview.Item is null) return false;
        var current = Inspect(preview.Input, preview.Item.Id);
        return current.Item == preview.Item && current.Length == preview.Length && current.LastWriteTicks == preview.LastWriteTicks;
    }
}
