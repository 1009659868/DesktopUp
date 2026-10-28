using System.Buffers.Binary;
using System.Security.Cryptography;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

/// <summary>Design-only, profile-scoped storage. Content files and their ownership never enter here.</summary>
public sealed class CanvasWorkspace : ICanvasWorkspace
{
    public const int MaxImageBytes = 8 * 1024 * 1024;
    private readonly string root;
    public CanvasWorkspace(IUserDataPaths paths) => root = Path.Combine(paths.Root, "canvas");
    public (int Width, int Height) ValidateImage(byte[] bytes) => ImageSize(bytes);
    private string Location(string name)
    {
        var path = Path.Combine(root, name);
        for (var p = path; p is not null; p = Path.GetDirectoryName(p))
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("设计资源位置包含重解析点，未读写。");
        return path;
    }
    private static void Key(string id)
    { if (id is not { Length: > 0 and <= 64 } || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) throw new ArgumentException("无效设计资源 ID。"); }
    private static void Name(string name)
    { if (string.IsNullOrWhiteSpace(name) || name.Length > 64 || name.Any(char.IsControl)) throw new ArgumentException("名称须为 1–64 个可见字符。"); }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public CanvasAsset Import(string name, byte[] bytes)
    {
        Name(name); var size = ImageSize(bytes); string id = Hash(bytes); string path = Location(id + ".image");
        Directory.CreateDirectory(root);
        if (!File.Exists(path))
        {
            var files = new DirectoryInfo(root).EnumerateFiles("*.image").Take(129).ToArray();
            if (files.Length >= 128 || files.Sum(f => f.Length) + bytes.Length > 256L * 1024 * 1024) throw new IOException("设计素材库已达本轮预算，请保留原文件并整理后再导入。");
            Atomic(path, bytes, false);
        }
        var asset = new CanvasAsset(id, name, size.Width, size.Height);
        if (!File.Exists(Location(id + ".asset.json"))) Write(id + ".asset.json", asset);
        return asset;
    }
    public byte[] ReadAsset(string id)
    {
        if (!CanvasDeclaration.Accepts("asset", new(Text: id))) throw new ArgumentException("图片必须使用资源摘要。");
        var bytes = Read(Location(id + ".image"), MaxImageBytes);
        if (Hash(bytes) != id) throw new IOException("设计图片摘要不符，原设计保留。");
        ImageSize(bytes); return bytes;
    }
    public IReadOnlyList<CanvasAsset> Assets() => List<CanvasAsset>("*.asset.json");
    public IReadOnlyList<CanvasWork> Works()
    {
        var assembly = typeof(CanvasWorkspace).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith("DesktopUp.Canvas.", StringComparison.Ordinal) && n.EndsWith(".jpg", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var memory = new MemoryStream(); stream.CopyTo(memory);
            Import("雨窗与木桌", memory.ToArray());
        }
        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith("DesktopUp.Canvas.", StringComparison.Ordinal) && n.EndsWith(".work.json", StringComparison.Ordinal)))
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
            var work = CanvasDeclaration.Read<CanvasWork>(reader.ReadToEnd());
            if (!File.Exists(Location(work.Id + ".work.json"))) SaveWork(work.Id, work.Name, work.Design);
            foreach (string id in work.LibraryGroups ?? [])
            {
                var authoring = new CanvasAuthoring(work.Design); var nodes = authoring.Export(id);
                var artboard = work.Design.Recipe.Artboard;
                var box = CanvasLayout.ForAuthoring(work.Design, artboard?.Width ?? 540, artboard?.Height ?? 674).Nodes.Single(n => n.Node.Id == id).Bounds;
                var size = new CanvasCombinationSize(box.Width, box.Height);
                SaveCombination(new(CanvasCombination.Digest(nodes, size), nodes[0].Name ?? work.Name, nodes, size));
            }
        }
        return List<CanvasWork>("*.work.json");
    }
    public IReadOnlyList<CanvasCombination> Combinations()
    {
        var combinations = List<CanvasCombination>("*.combo.json");
        foreach (var combination in combinations)
        {
            Key(combination.Id); Name(combination.Name);
            ValidateSize(combination.Size);
            if (combination.Nodes is null || CanvasCombination.Digest(combination.Nodes, combination.Size) != combination.Id)
                throw new ArgumentException("组合内容摘要不符，未载入。");
            var check = new CanvasAuthoring(CanvasAuthoring.Blank()); check.Insert(combination.Nodes, "root");
        }
        return combinations;
    }
    public void SaveWork(string id, string name, CanvasDesign design, bool showContentReference = true)
    { Key(id); Name(name); design = CanvasDeclaration.Freeze(design); Verify(design); Write(id + ".work.json", new CanvasWork(id, name, design, ShowContentReference: showContentReference)); }
    public void SaveCombination(CanvasCombination combination)
    {
        Key(combination.Id); Name(combination.Name);
        ValidateSize(combination.Size);
        var check = new CanvasAuthoring(CanvasAuthoring.Blank());
        check.Insert(combination.Nodes, "root"); Verify(check.Design);
        string digest = CanvasCombination.Digest(combination.Nodes, combination.Size);
        if (combination.Id != digest) throw new ArgumentException("组合身份必须为内容摘要。");
        string path = Location(digest + ".combo.json");
        if (!File.Exists(path)) Write(digest + ".combo.json", combination);
    }
    private static void ValidateSize(CanvasCombinationSize? size)
    {
        if (size is not null && (!double.IsFinite(size.Width) || !double.IsFinite(size.Height) ||
            size.Width is < 1 or > 4096 || size.Height is < 1 or > 4096))
            throw new ArgumentException("组合尺寸超出预算。");
    }
    public void Verify(CanvasDesign design)
    {
        CanvasDeclaration.Validate(design);
        foreach (var id in design.Recipe.Nodes.Where(n => n.Type == "draw.image@1").Select(n => CanvasDeclaration.Property(design, n, "asset").Text!).Distinct()) ReadAsset(id);
    }
    private T[] List<T>(string pattern)
    {
        Location("check"); if (!Directory.Exists(root)) return [];
        return Directory.EnumerateFiles(root, pattern).Order(StringComparer.Ordinal).Take(128)
            .Select(p => CanvasDeclaration.Read<T>(System.Text.Encoding.UTF8.GetString(Read(Location(Path.GetFileName(p)), CanvasDeclaration.MaximumBytes * 3)))).ToArray();
    }
    private void Write<T>(string name, T value)
    {
        string path = Location(name); Directory.CreateDirectory(root);
        var bytes = System.Text.Encoding.UTF8.GetBytes(CanvasDeclaration.Serialize(value));
        if (bytes.Length > CanvasDeclaration.MaximumBytes * 3) throw new ArgumentException("设计数据超出预算。");
        if (!File.Exists(path) && Directory.EnumerateFiles(root, "*.json").Take(385).Count() >= 384) throw new IOException("设计库已达预算。");
        Atomic(path, bytes, true);
    }
    private void Atomic(string path, byte[] bytes, bool overwrite)
    {
        string temp = Location("write-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); }
            Location(Path.GetFileName(path)); File.Move(temp, path, overwrite);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private static byte[] Read(string path, int limit)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is < 1 || stream.Length > limit) throw new IOException("设计资源大小不合法。");
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes); return bytes;
    }
    // Header budgets precede the platform's real decode. No SVG, URI or executable payloads.
    public static (int Width, int Height) ImageSize(byte[] bytes)
    {
        if (bytes.Length is < 24 or > MaxImageBytes) throw new ArgumentException("PNG/JPEG 图片需小于 8 MB。");
        int width = 0, height = 0;
        if (bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        { width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)); height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)); }
        else if (bytes[0] == 255 && bytes[1] == 216)
        {
            int p = 2;
            while (p + 4 <= bytes.Length)
            {
                if (bytes[p++] != 255) break;
                while (p < bytes.Length && bytes[p] == 255) p++;
                if (p >= bytes.Length) break;
                int marker = bytes[p++]; if (marker is 217 or 218) break;
                if (marker is 1 or >= 208 and <= 215) continue;
                if (p + 2 > bytes.Length) break;
                int length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(p, 2));
                if (length < 2 || p + length > bytes.Length) break;
                if (marker is 192 or 193 or 194)
                { if (length < 8) break; height = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(p + 3, 2)); width = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(p + 5, 2)); break; }
                p += length;
            }
        }
        if (width is < 1 or > 8192 || height is < 1 or > 8192 || (long)width * height > 24_000_000)
            throw new ArgumentException("仅支持有效 PNG/JPEG，单边不超过 8192，像素不超过 2400 万。");
        return (width, height);
    }
}
