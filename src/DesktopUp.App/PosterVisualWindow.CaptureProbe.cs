using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
namespace DesktopUp;
internal sealed partial class PosterVisualWindow
{
    internal async Task CapturePosterXamlForProbeAsync(string path)
    {
        var content = _window?.Content ?? throw new InvalidOperationException("Poster XAML is unavailable.");
        if (content.XamlRoot is null) throw new InvalidOperationException("Poster XAML is not connected.");
        var bitmap = new RenderTargetBitmap(); await bitmap.RenderAsync(content);
        if (bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0) throw new InvalidOperationException("Poster capture is empty.");
        var pixels = await bitmap.GetPixelsAsync();
        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(path)!);
        var file = await folder.CreateFileAsync(Path.GetFileName(path), CreationCollisionOption.FailIfExists);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
    }
    internal bool ContentVisualReadyForProbe => _window?.Content is FrameworkElement { XamlRoot: not null, ActualWidth: > 0, ActualHeight: > 0 };

}
internal sealed partial class PosterSpatialRuntime
{
    internal Task CapturePosterXamlForProbeAsync(string posterId, string path) => _windows[posterId].CapturePosterXamlForProbeAsync(path);
    internal bool ContentVisualReadyForProbe(string posterId) => !_suspended && _windows.TryGetValue(posterId, out var window) && window.ContentVisualReadyForProbe;
}
