using DesktopUp.Application;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DesktopUp;

/// <summary>UI-thread decoded image cache shared by studio and native scene; bounded, no polling.</summary>
internal sealed class CanvasImages(ICanvasWorkspace workspace)
{
    private readonly Dictionary<string, BitmapImage> cache = new(StringComparer.Ordinal);
    private readonly Queue<string> order = new();
    private readonly Dictionary<string, BitmapImage> thumbnails = new(StringComparer.Ordinal);
    internal BitmapImage Thumbnail(string id)
    {
        if (thumbnails.TryGetValue(id, out var found)) return found;
        var bytes = workspace.ReadAsset(id); var image = Decode(bytes, workspace.ValidateImage(bytes), 96);
        if (thumbnails.Count < 128) thumbnails[id] = image;
        return image;
    }
    internal BitmapImage Load(string id)
    {
        if (cache.TryGetValue(id, out var found)) return found;
        var bytes = workspace.ReadAsset(id); var image = Decode(bytes, workspace.ValidateImage(bytes));
        while (cache.Count >= 8) cache.Remove(order.Dequeue());
        cache.Add(id, image); order.Enqueue(id); return image;
    }
    internal static BitmapImage Decode(byte[] bytes, (int Width, int Height) size, int longestSide = 2048)
    {
        using var stream = new MemoryStream(bytes, false);
        using var random = stream.AsRandomAccessStream();
        double scale = Math.Min(1, (double)longestSide / Math.Max(size.Width, size.Height));
        var image = new BitmapImage { DecodePixelWidth = Math.Max(1, (int)(size.Width * scale)), DecodePixelHeight = Math.Max(1, (int)(size.Height * scale)) };
        image.SetSource(random);
        if (image.PixelWidth < 1 || image.PixelHeight < 1) throw new ArgumentException("图片无法解码，未加入设计。");
        return image;
    }
}
