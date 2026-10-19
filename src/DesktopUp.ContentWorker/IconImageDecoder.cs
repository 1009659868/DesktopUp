using DesktopUp.Windows;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.ContentWorker;

// Only system PNG/JPEG/ICO decoders, in the disposable worker; never a UI-thread decoder.
internal static class IconImageDecoder
{
    internal static LocalContentResponse Decode(LocalContentRequest request)
    {
        var source = LocalContentReferenceProbe.Inspect(request.Item.Target.Locator);
        if (source.Item is not { Kind: ContentKind.File } item || item.Target != request.Item.Target)
            throw new InvalidDataException("A local regular image file is required.");
        using var file = new FileStream(item.Target.Locator, FileMode.Open, FileAccess.Read, FileShare.Read);
        var finalName = new StringBuilder(8192);
        uint size = GetFinalPathNameByHandle(file.SafeFileHandle, finalName, (uint)finalName.Capacity, 0);
        string opened = finalName.ToString();
        if (opened.StartsWith(@"\\?\", StringComparison.Ordinal)) opened = opened[4..];
        if (size == 0 || size >= finalName.Capacity || !string.Equals(opened, item.Target.Locator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Image source changed or redirected.");
        if (file.Length is < 4 or > ContentIconImport.MaximumInputBytes) throw new InvalidDataException("Image input budget exceeded.");
        var bytes = new byte[(int)file.Length]; file.ReadExactly(bytes);
        var format = ContentIconImport.Inspect(bytes, Path.GetExtension(opened));
        Guid decoderClass = new(format switch
        {
            ContentIconFileFormat.Png => "389ea17b-5078-4cde-b6ef-25c15175c751",
            ContentIconFileFormat.Jpeg => "9456a480-e88b-43ea-9e73-0b2d9b71b1ca",
            _ => "c61bfcdf-2e0f-4aad-a8d7-e06bafebcdfe"
        });
        object? decoderObject = null, factoryObject = null, selected = null;
        IWicBitmapScaler? scaler = null; IWicBitmapSource? converted = null; IStream? stream = null;
        try
        {
            stream = SHCreateMemStream(bytes, (uint)bytes.Length) ?? throw new IOException("Cannot allocate bounded image stream.");
            Guid decoderIid = typeof(IWicBitmapDecoder).GUID;
            Marshal.ThrowExceptionForHR(CoCreateInstance(ref decoderClass, 0, 1, ref decoderIid, out decoderObject));
            var decoder = (IWicBitmapDecoder)decoderObject;
            decoder.Initialize(stream, 0); decoder.GetFrameCount(out uint count);
            if (count is < 1 or > ContentIconImport.MaximumIconFrames || format != ContentIconFileFormat.Ico && count != 1)
                throw new InvalidDataException("Only static images/icon representations are supported.");
            uint width = 0, height = 0; long aggregate = 0;
            for (uint i = 0; i < count; i++)
            {
                decoder.GetFrame(i, out object frame);
                try
                {
                    ((IWicBitmapSource)frame).GetSize(out uint w, out uint h); ContentIconImport.ValidateSize(w, h);
                    aggregate += (long)w * h;
                    if (aggregate > ContentIconImport.MaximumSourcePixels) throw new InvalidDataException("Aggregate frame budget exceeded.");
                    if ((long)w * h > (long)width * height)
                    { Release(selected); selected = frame; width = w; height = h; frame = null!; }
                }
                finally { Release(frame); }
            }
            var bitmap = (IWicBitmapSource)selected!;
            Guid factoryClass = new("cacaf262-9370-4615-a13b-9f5539da4c0a"), factoryIid = typeof(IWicImagingFactory).GUID;
            Marshal.ThrowExceptionForHR(CoCreateInstance(ref factoryClass, 0, 1, ref factoryIid, out factoryObject));
            double factor = Math.Min(1, ContentIconAsset.MaximumDimension / (double)Math.Max(width, height));
            uint outWidth = Math.Max(1, (uint)Math.Round(width * factor)), outHeight = Math.Max(1, (uint)Math.Round(height * factor));
            if (outWidth != width || outHeight != height)
            {
                ((IWicImagingFactory)factoryObject).CreateBitmapScaler(out scaler);
                scaler.Initialize(bitmap, outWidth, outHeight, 3); // WICBitmapInterpolationModeFant; never upscale.
                bitmap = (IWicBitmapSource)scaler;
            }
            Guid pbgra = new("6fddc324-4e03-4bfe-b185-3d77768dc910");
            Marshal.ThrowExceptionForHR(WICConvertBitmapSource(ref pbgra, bitmap, out converted));
            var pixels = new byte[checked(outWidth * outHeight * 4)];
            converted.CopyPixels(0, outWidth * 4, (uint)pixels.Length, pixels);
            _ = ContentIconAsset.Create((int)outWidth, (int)outHeight, pixels);
            return new(request.RequestId, ContentAvailability.Available, "", Pixels: pixels, Width: (int)outWidth, Height: (int)outHeight);
        }
        finally
        {
            // WIC may return the very same RCW for an already-converted bitmap.
            var released = new HashSet<object>(ReferenceEqualityComparer.Instance);
            foreach (var value in new object?[] { converted, scaler, selected, decoderObject, factoryObject, stream })
                if (value is not null && released.Add(value)) Release(value);
        }
    }
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle file, StringBuilder path, uint length, uint flags);
    [DllImport("shlwapi.dll")] [return: MarshalAs(UnmanagedType.Interface)]
    private static extern IStream? SHCreateMemStream(byte[] data, uint length);
    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(ref Guid clsid, nint outer, uint context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object value);
    [DllImport("windowscodecs.dll")]
    private static extern int WICConvertBitmapSource(ref Guid format, IWicBitmapSource source, out IWicBitmapSource converted);

    // Slots/GUIDs checked against Windows SDK wincodec.h. Unused slots are never called.
    [ComImport, Guid("00000120-a8f2-4877-ba0a-fd2b6645fb94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWicBitmapSource
    {
        void GetSize(out uint width, out uint height); void GetPixelFormat(out Guid format);
        void GetResolution(out double x, out double y); void CopyPalette(nint palette);
        void CopyPixels(nint rectangle, uint stride, uint size, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] bytes);
    }
    [ComImport, Guid("00000302-a8f2-4877-ba0a-fd2b6645fb94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWicBitmapScaler
    {
        void GetSize(out uint width, out uint height); void GetPixelFormat(out Guid format);
        void GetResolution(out double x, out double y); void CopyPalette(nint palette);
        void CopyPixels(nint rectangle, uint stride, uint size, nint bytes);
        void Initialize(IWicBitmapSource source, uint width, uint height, uint interpolation);
    }
    [ComImport, Guid("9edde9e7-8dee-47ea-99df-e6faf2ed44bf"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWicBitmapDecoder
    {
        void QueryCapability(); void Initialize(IStream stream, uint options); void GetContainerFormat();
        void GetDecoderInfo(); void CopyPalette(); void GetMetadataQueryReader(); void GetPreview(); void GetColorContexts(); void GetThumbnail();
        void GetFrameCount(out uint count); void GetFrame(uint index, [MarshalAs(UnmanagedType.Interface)] out object frame);
    }
    [ComImport, Guid("ec5ec8a9-c395-4314-9c77-54d7a935ff70"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWicImagingFactory
    {
        void CreateDecoderFromFilename(); void CreateDecoderFromStream(); void CreateDecoderFromFileHandle();
        void CreateComponentInfo(); void CreateDecoder(); void CreateEncoder(); void CreatePalette(); void CreateFormatConverter();
        void CreateBitmapScaler(out IWicBitmapScaler scaler);
    }
}
