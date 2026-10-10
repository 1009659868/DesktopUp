using System.Buffers.Binary;
using System.Security.Cryptography;

namespace DesktopUp.Domain;

// Canonical, immutable premultiplied BGRA. No source pathname/metadata is persisted.
public sealed class ContentIconAsset
{
    public const int MaximumDimension = 256;
    public const int MaximumBytes = MaximumDimension * MaximumDimension * 4;
    public const int MaximumAssets = 512;
    public const long MaximumStoreBytes = 64 * 1024 * 1024;
    private readonly byte[] pixels;
    public string Id { get; }
    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels => pixels.ToArray();
    private ContentIconAsset(int width, int height, byte[] data)
    {
        Width = width; Height = height; pixels = data;
        byte[] canonical = new byte[12 + data.Length];
        BinaryPrimitives.WriteInt32LittleEndian(canonical, 1); // canonical raster format version
        BinaryPrimitives.WriteInt32LittleEndian(canonical.AsSpan(4), width);
        BinaryPrimitives.WriteInt32LittleEndian(canonical.AsSpan(8), height);
        data.CopyTo(canonical, 12);
        Id = "icon-v1-" + Convert.ToHexStringLower(SHA256.HashData(canonical));
    }
    public static bool ValidId(string? id) => id is { Length: 72 } && id.StartsWith("icon-v1-", StringComparison.Ordinal) &&
        id.AsSpan(8).IndexOfAnyExcept("0123456789abcdef") < 0;
    public static ContentIconAsset Create(int width, int height, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (width is < 1 or > MaximumDimension || height is < 1 or > MaximumDimension || data.Length != width * height * 4)
            throw new ArgumentException("Icon raster exceeds the canonical budget.");
        var copy = data.ToArray();
        for (int i = 0; i < copy.Length; i += 4)
            if (copy[i] > copy[i + 3] || copy[i + 1] > copy[i + 3] || copy[i + 2] > copy[i + 3])
                throw new ArgumentException("Icon raster is not premultiplied BGRA.");
        return new(width, height, copy);
    }
}
