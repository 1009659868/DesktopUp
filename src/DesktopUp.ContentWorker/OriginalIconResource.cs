using DesktopUp.Windows;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.ContentWorker;

internal static partial class Program
{
    private const int MaximumIconFrames = 256;
    private const int MaximumIconResourceBytes = 8 * 1024 * 1024;

    // Read stored link metadata only. Do NOT Resolve/Save a shortcut, run its target,
    // follow another shortcut, or replace an explicit custom icon with the target's icon.
    private static (byte[] Pixels, int Width, int Height)? TryOriginalIcon(string path)
    {
        object? linkObject = null;
        try
        {
            int index = 0;
            bool explicitResource = false;
            if (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                linkObject = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"), true)!);
                ((IPersistFile)linkObject!).Load(path, 0); // STGM_READ; no network resolution or link repair
                var link = (IStoredShellLink)linkObject;
                var location = new StringBuilder(8192);
                if (link.GetIconLocation(location, location.Capacity, out index) < 0) return null;
                explicitResource = location.Length != 0;
                if (location.Length == 0)
                {
                    index = 0;
                    if (link.GetPath(location, location.Capacity, 0, 4) < 0) return null; // SLGP_RAWPATH
                }
                path = Environment.ExpandEnvironmentVariables(location.ToString());
            }
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension is not (".ico" or ".exe" or ".dll" or ".icl")) return null;
            // A DLL/ICL viewed as a file normally has an associated Shell icon. Its internal
            // resources are authoritative only when the shortcut explicitly selects one.
            if (!explicitResource && extension is ".dll" or ".icl") return null;
            var source = LocalContentReferenceProbe.Inspect(path);
            if (source.Item is not { Kind: ContentKind.File } item) return null;
            path = item.Target.Locator;
            return extension == ".ico" ? index == 0 ? ReadIconFile(path) : null : ReadModuleIcon(path, index);
        }
        catch (Exception error) when (error is COMException or IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or InvalidCastException or OverflowException or System.Security.SecurityException)
        { return null; } // Shell remains authoritative for unsupported/dynamic sources.
        finally { if (linkObject is not null) Marshal.FinalReleaseComObject(linkObject); }
    }

    private sealed record ResourceFrame(int Width, int Height, int Depth, uint Length, uint Location);

    private static ResourceFrame[] ReadIconDirectory(byte[] directory, bool group)
    {
        int stride = group ? 14 : 16;
        if (directory.Length < 6 || BinaryPrimitives.ReadUInt16LittleEndian(directory) != 0 ||
            BinaryPrimitives.ReadUInt16LittleEndian(directory.AsSpan(2)) != 1) return [];
        int count = BinaryPrimitives.ReadUInt16LittleEndian(directory.AsSpan(4));
        if (count is < 1 or > MaximumIconFrames || directory.Length < 6 + count * stride) return [];
        var frames = new List<ResourceFrame>();
        for (int i = 0; i < count; i++)
        {
            var entry = directory.AsSpan(6 + i * stride, stride);
            int width = entry[0] == 0 ? 256 : entry[0], height = entry[1] == 0 ? 256 : entry[1];
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]);
            uint location = group ? BinaryPrimitives.ReadUInt16LittleEndian(entry[12..]) : BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]);
            if (length is 0 or > MaximumIconResourceBytes || (!group && location < 6 + count * stride)) continue;
            frames.Add(new(width, height, BinaryPrimitives.ReadUInt16LittleEndian(entry[6..]), length, location));
        }
        return frames.OrderByDescending(frame => frame.Width * frame.Height).ThenByDescending(frame => frame.Depth).ToArray();
    }

    private static (byte[] Pixels, int Width, int Height)? ReadIconFile(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length < 6 || file.Length > MaximumIconResourceBytes) return null;
        var header = new byte[6]; file.ReadExactly(header);
        int count = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(4));
        if (count is < 1 or > MaximumIconFrames || file.Length < 6 + count * 16) return null;
        var directory = new byte[6 + count * 16]; header.CopyTo(directory, 0); file.ReadExactly(directory.AsSpan(6));
        foreach (var frame in ReadIconDirectory(directory, false))
        {
            if ((ulong)frame.Location + frame.Length > (ulong)file.Length) continue;
            file.Position = frame.Location;
            var data = new byte[frame.Length]; file.ReadExactly(data);
            if (DecodeResourceFrame(data, frame) is { } image) return image;
        }
        return null;
    }

    private static (byte[] Pixels, int Width, int Height)? ReadModuleIcon(string path, int index)
    {
        if (index is < -65535 or > 65535) return null;
        // DATAFILE | IMAGE_RESOURCE: resource mapping only. No DllMain or dependency execution.
        nint module = LoadLibraryExW(path, 0, 0x22);
        if (module == 0) return null;
        try
        {
            byte[]? group = null;
            if (index < 0) group = ReadResource(module, (nint)(-index), 14);
            else
            {
                int ordinal = 0;
                EnumResourceName callback = (handle, type, name, parameter) =>
                {
                    if (ordinal++ != index) return true;
                    group = ReadResource(handle, name, 14); return false;
                };
                EnumResourceNamesW(module, 14, callback, 0);
                GC.KeepAlive(callback);
            }
            if (group is null) return null;
            foreach (var frame in ReadIconDirectory(group, true))
            {
                var bytes = ReadResource(module, (nint)frame.Location, 3);
                if (bytes is null || bytes.Length != frame.Length) continue;
                if (DecodeResourceFrame(bytes, frame) is { } image) return image;
            }
            return null;
        }
        finally { FreeLibrary(module); }
    }

    private static byte[]? ReadResource(nint module, nint name, nint type)
    {
        nint resource = FindResourceW(module, name, type);
        uint length = resource == 0 ? 0 : SizeofResource(module, resource);
        if (length is 0 or > MaximumIconResourceBytes) return null;
        nint address = LockResource(LoadResource(module, resource));
        if (address == 0) return null;
        var bytes = new byte[length]; Marshal.Copy(address, bytes, 0, bytes.Length); return bytes;
    }

    private static (byte[] Pixels, int Width, int Height)? DecodeResourceFrame(byte[] bytes, ResourceFrame frame)
    {
        // Validate encoded dimensions too; directory dimensions alone do not bound decoding.
        ReadOnlySpan<byte> data = bytes;
        bool png = data.Length >= 33 && data[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        if (png)
        {
            if (BinaryPrimitives.ReadUInt32BigEndian(data[8..]) != 13 || !data.Slice(12, 4).SequenceEqual("IHDR"u8) ||
                BinaryPrimitives.ReadUInt32BigEndian(data[16..]) != frame.Width || BinaryPrimitives.ReadUInt32BigEndian(data[20..]) != frame.Height)
                return null;
        }
        else if (data.Length < 40 || BinaryPrimitives.ReadUInt32LittleEndian(data) < 40 ||
            BinaryPrimitives.ReadInt32LittleEndian(data[4..]) != frame.Width ||
            BinaryPrimitives.ReadInt32LittleEndian(data[8..]) != frame.Height * 2) return null;
        if (frame.Width > LocalContentProtocol.MaximumIconDimension || frame.Height > LocalContentProtocol.MaximumIconDimension) return null;
        nint icon = CreateIconFromResourceEx(bytes, (uint)bytes.Length, true, 0x00030000, frame.Width, frame.Height, 0);
        if (icon == 0) return null;
        try
        {
            var pixels = Rasterize(icon, frame.Width, frame.Height);
            return pixels is not null && pixels.Where((_, i) => i % 4 == 3).Any(alpha => alpha != 0)
                ? (pixels, frame.Width, frame.Height) : null;
        }
        finally { DestroyIcon(icon); }
    }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IStoredShellLink
    {
        [PreserveSig] int GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count, nint data, uint flags);
        void GetIDList(out nint id); void SetIDList(nint id);
        void GetDescription(nint text, int count); void SetDescription(nint text);
        void GetWorkingDirectory(nint text, int count); void SetWorkingDirectory(nint text);
        void GetArguments(nint text, int count); void SetArguments(nint text);
        void GetHotkey(out short key); void SetHotkey(short key);
        void GetShowCmd(out int command); void SetShowCmd(int command);
        [PreserveSig] int GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count, out int index);
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)] private delegate bool EnumResourceName(nint module, nint type, nint name, nint parameter);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern nint LoadLibraryExW(string path, nint file, uint flags);
    [DllImport("kernel32.dll")] private static extern bool FreeLibrary(nint module);
    [DllImport("kernel32.dll", ExactSpelling = true)] private static extern bool EnumResourceNamesW(nint module, nint type, EnumResourceName callback, nint parameter);
    [DllImport("kernel32.dll", ExactSpelling = true)] private static extern nint FindResourceW(nint module, nint name, nint type);
    [DllImport("kernel32.dll")] private static extern uint SizeofResource(nint module, nint resource);
    [DllImport("kernel32.dll")] private static extern nint LoadResource(nint module, nint resource);
    [DllImport("kernel32.dll")] private static extern nint LockResource(nint resource);
    [DllImport("user32.dll")] private static extern nint CreateIconFromResourceEx(byte[] bytes, uint length, [MarshalAs(UnmanagedType.Bool)] bool icon, uint version, int width, int height, uint flags);
}
