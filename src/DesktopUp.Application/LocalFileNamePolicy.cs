namespace DesktopUp.Application;

public static class LocalFileNamePolicy
{
    // Explorer selects the stem, not the extension; folder names are selected in full.
    public static int RenameSelectionLength(string name, bool folder)
    {
        int dot = name.LastIndexOf('.');
        return !folder && dot > 0 ? dot : name.Length;
    }
    public static string? Error(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name is "." or ".." ||
            name.EndsWith(' ') || name.EndsWith('.') || name.Any(char.IsControl) ||
            name.IndexOfAny(['\\','/',':','*','?','"','<','>','|']) >= 0)
            return "请输入有效名称，不含路径、特殊字符或末尾空格/句点。";
        string stem = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CLOCK$" ||
            stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) &&
            "123456789¹²³".Contains(stem[3])) return "此名称被 Windows 保留。";
        return null;
    }
}
