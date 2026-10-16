using DesktopUp.Domain;

namespace DesktopUp.Application;

// Lexical admission is deliberately IO-free: safe to use in OLE hover, worker dispatch
// and metadata probing. It is NOT permission to move a file; identity/volume/journal
// checks remain in the existing executor.
public static class LocalContentCompatibility
{
    public const int MaximumPathCharacters = 8192;
    // RECALL_ON_DATA_ACCESS: data is not fully local. Do not confuse 0x40000 (EA in
    // GetFileAttributes) with RECALL_ON_OPEN, which belongs to directory enumeration.
    public const FileAttributes UnmaterializedAttributes =
        FileAttributes.ReparsePoint | FileAttributes.Offline | (FileAttributes)0x00400000;

    public static bool IsMaterialized(FileAttributes attributes) =>
        (attributes & UnmaterializedAttributes) == 0;

    public static ContentKind Kind(string path, FileAttributes attributes) =>
        (attributes & FileAttributes.Directory) != 0 ? ContentKind.Folder :
        Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(path).Equals(".url", StringComparison.OrdinalIgnoreCase)
            ? ContentKind.Shortcut : ContentKind.File;

    public static bool TryNormalize(string? input, bool allowRoot, out string path, out string reason)
    {
        path = "";
        reason = "仅支持本机完整文件路径；网络、协议及裸 Shell/虚拟对象暂不支持。";
        if (string.IsNullOrWhiteSpace(input) || input.Length is < 3 or > MaximumPathCharacters ||
            input.Any(char.IsControl) || !char.IsAsciiLetter(input[0]) || input[1] != ':' ||
            input[2] is not ('\\' or '/') || input.AsSpan(2).Contains(':') ||
            input.IndexOfAny(['*', '?', '"', '<', '>', '|']) >= 0) return false;
        try
        {
            var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(input));
            if (!allowRoot && normalized.Length <= 3)
            {
                reason = "不能将整个磁盘作为文件收纳或搬动。";
                return false;
            }
            // Validate the original components: GetFullPath may already strip a trailing
            // dot/space from an ancestor, concealing the ambiguous source from later checks.
            foreach (string segment in input[3..].Replace('/', '\\').Split('\\', StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment is "." or "..") continue;
                string stem = segment.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
                if (segment.EndsWith(' ') || segment.EndsWith('.') ||
                    stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
                    stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) ||
                        stem.StartsWith("LPT", StringComparison.Ordinal)) && "123456789¹²³".Contains(stem[3]))
                {
                    reason = "设备名称或末尾空格/点的路径暂不支持，未访问来源。";
                    return false;
                }
            }
            path = normalized;
            reason = "";
            return true;
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        { return false; }
    }
}
