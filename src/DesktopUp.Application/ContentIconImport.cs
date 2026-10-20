using System.Buffers.Binary;
using DesktopUp.Domain;

namespace DesktopUp.Application;

public enum ContentIconFileFormat { Png, Jpeg, Ico }
public static class ContentIconImport
{
    // File metadata may block; never inspect it on the window thread. Decode keeps the existing worker budget.
    public static async ValueTask<ContentIconAsset> LoadAsync(IContentIconSource source,
        ILocalContentWorker worker, string path, CancellationToken cancellation)
    {
        var image = await Task.Run(() => source.Inspect(path), cancellation).ConfigureAwait(false);
        cancellation.ThrowIfCancellationRequested();
        return await DecodeAsync(worker, image, cancellation).ConfigureAwait(false);
    }

    public const int MaximumInputBytes = 8 * 1024 * 1024;
    public const uint MaximumSourceDimension = 4096;
    public const long MaximumSourcePixels = 16 * 1024 * 1024;
    public const uint MaximumIconFrames = 32;
    public static ContentIconFileFormat Inspect(byte[] bytes, string extension)
    {
        if (bytes.Length is < 4 or > MaximumInputBytes) throw new ArgumentException("Image input budget exceeded.");
        extension = extension.ToLowerInvariant();
        if (extension == ".png" && bytes.AsSpan().StartsWith(new byte[] { 137,80,78,71,13,10,26,10 }))
        {
            int offset = 8; bool ended = false;
            while (offset <= bytes.Length - 12)
            {
                uint length = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset));
                if (length > bytes.Length - offset - 12) throw new ArgumentException("Truncated PNG chunk.");
                var type = bytes.AsSpan(offset + 4, 4);
                if (type.SequenceEqual("acTL"u8)) throw new ArgumentException("Animated PNG is not supported.");
                if (offset == 8 && (!type.SequenceEqual("IHDR"u8) || length != 13)) throw new ArgumentException("Invalid PNG header.");
                if (offset == 8) ValidateSize(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16)), BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20)));
                offset += checked((int)length + 12);
                if (type.SequenceEqual("IEND"u8)) { ended = length == 0 && offset == bytes.Length; break; }
            }
            if (!ended) throw new ArgumentException("Incomplete PNG.");
            return ContentIconFileFormat.Png;
        }
        if (extension is ".jpg" or ".jpeg" && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255)
            return ContentIconFileFormat.Jpeg;
        if (extension == ".ico" && bytes.Length >= 6 && bytes.AsSpan(0,4).SequenceEqual(new byte[] {0,0,1,0}))
        {
            int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4));
            if (count is < 1 or > (int)MaximumIconFrames || bytes.Length < 6 + count * 16) throw new ArgumentException("Invalid ICO frame directory.");
            for (int i = 0; i < count; i++)
            {
                var entry = bytes.AsSpan(6 + i * 16, 16);
                uint length = BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]);
                uint start = BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]);
                if (start < 6 + count * 16 || length == 0 || (ulong)start + length > (ulong)bytes.Length) throw new ArgumentException("Invalid ICO frame bounds.");
            }
            return ContentIconFileFormat.Ico;
        }
        throw new ArgumentException("Only matching static PNG, JPEG and ICO inputs are supported.");
    }
    public static void ValidateSize(uint width, uint height)
    {
        if (width is 0 or > MaximumSourceDimension || height is 0 or > MaximumSourceDimension || (ulong)width * height > MaximumSourcePixels)
            throw new ArgumentException("Decoded image dimensions exceed budget.");
    }
    public static async ValueTask<ContentIconAsset> DecodeAsync(ILocalContentWorker worker, ContentItem image, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var request = new LocalContentRequest(Guid.NewGuid(), LocalContentAction.DecodeIcon, image);
        var response = await worker.RunAsync(request, cancellation).ConfigureAwait(false);
        cancellation.ThrowIfCancellationRequested();
        if (!LocalContentProtocol.Validate(request, response) || response.Cancelled || response.Availability != ContentAvailability.Available || response.Pixels is null)
            throw new InvalidDataException("图像无法安全解码，原图标保持不变。");
        return ContentIconAsset.Create(response.Width, response.Height, response.Pixels);
    }
}
