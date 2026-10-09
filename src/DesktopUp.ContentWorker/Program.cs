using System.Runtime.InteropServices;
using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp.ContentWorker;

internal static partial class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Private stdio protocol only. The worker never opens a profile/database.
        if (args.Length != 1 || args[0] != "--stdio-v1" || !Console.IsInputRedirected || !Console.IsOutputRedirected) return 2;
        int initialized = CoInitializeEx(0, 2);
        try
        {
            var chars = new char[LocalContentProtocol.MaximumRequestCharacters + 1];
            int count = Console.In.ReadBlock(chars, 0, chars.Length);
            if (count > LocalContentProtocol.MaximumRequestCharacters) return 3;
            var request = JsonSerializer.Deserialize<LocalContentRequest>(new string(chars, 0, count));
            if (request is null || request.RequestId == Guid.Empty || !Enum.IsDefined(request.Action) ||
                !LocalContentProtocol.IsSupported(request.Item)) return 4;
            LocalContentResponse response;
            try { response = Execute(request); }
            catch (Exception) { response = new(request.RequestId, ContentAvailability.Unavailable, "Windows 未完成操作；未自动重试。"); }
            Console.Write(JsonSerializer.Serialize(response));
            return 0;
        }
        catch (Exception) { return 5; }
        finally { if (initialized >= 0) CoUninitialize(); }
    }

    private static LocalContentResponse Execute(LocalContentRequest request)
    {
        var preview = LocalContentReferenceProbe.Inspect(request.Item.Target.Locator, request.Item.Id);
        if (preview.Item is not { } live)
            return new(request.RequestId, ContentAvailability.Unavailable, preview.Message);
        if (live.Kind != request.Item.Kind || live.Target != request.Item.Target)
            return new(request.RequestId, ContentAvailability.Unavailable, "来源类型已变化，请修复引用。");
        string path = live.Target.Locator;
        if (request.Action is LocalContentAction.Inspect or LocalContentAction.Icon)
        {
            var info = new ShellFileInfo();
            byte[]? pixels = null;
            int width = 0, height = 0;
            // Actual item icon, not extension guessing; all Shell extension work stays in this process.
            nint result = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<ShellFileInfo>(),
                request.Action == LocalContentAction.Icon ? 0x100u | 0x400u : 0x400u);
            try
            {
                if (request.Action == LocalContentAction.Icon)
                {
                    var large = TryLargeShellIcon(path);
                    if (large is { } icon) { pixels = icon.Pixels; width = icon.Width; height = icon.Height; }
                    else if (info.Icon != 0) { pixels = Rasterize(info.Icon, 64); if (pixels is not null) width = height = 64; }
                }
            }
            finally { if (info.Icon != 0) DestroyIcon(info.Icon); }
            return new(request.RequestId, ContentAvailability.Available, result == 0 ? "图标暂不可用，保留类型占位。" : "",
                live.DisplayName, string.IsNullOrWhiteSpace(info.TypeName) ? live.Kind.ToString() : info.TypeName,
                pixels, width, height);
        }
        if (request.Action == LocalContentAction.Reveal)
        {
            int parsed = SHParseDisplayName(path, 0, out nint item, 0, out _);
            if (parsed < 0 || item == 0) return new(request.RequestId, ContentAvailability.Unavailable, "无法定位此来源。");
            try
            {
                int result = SHOpenFolderAndSelectItems(item, 0, 0, 0);
                return new(request.RequestId, result >= 0 ? ContentAvailability.Available : ContentAvailability.Unavailable,
                    result >= 0 ? "已交给资源管理器定位。" : "资源管理器未接受定位请求。",
                    Evidence: result >= 0 ? ContentOpenEvidence.ProviderConfirmed : ContentOpenEvidence.None);
            }
            finally { Marshal.FreeCoTaskMem(item); }
        }
        // Standard 'open', no runas, no command interpreter, no bypass of attachment/security checks.
        var execute = new ShellExecuteInfo
        {
            Size = Marshal.SizeOf<ShellExecuteInfo>(), Mask = 0x40 | 0x100 | 0x400,
            Verb = "open", File = path, Show = 1
        };
        bool accepted = ShellExecuteEx(ref execute);
        int error = accepted ? 0 : Marshal.GetLastWin32Error();
        if (execute.Process != 0) CloseHandle(execute.Process); // never wait for/terminate the user's launched app
        return new(request.RequestId, accepted ? ContentAvailability.Available : ContentAvailability.Unavailable,
            accepted ? "Windows 已接收打开请求。" : error == 1223 ? "打开已取消。" : $"Windows 未接受打开请求（{error}）。",
            Evidence: accepted ? ContentOpenEvidence.ShellAccepted : ContentOpenEvidence.None, Cancelled: error == 1223)
        { Outcome = accepted ? ContentOpenOutcome.Succeeded : error == 1223 ? ContentOpenOutcome.Cancelled : ContentOpenOutcome.Failed };
    }

    // Render twice to recover alpha for legacy AND-mask icons as well as modern alpha icons.
    private static byte[]? Rasterize(nint icon, int size)
    {
        byte[]? Paint(byte background)
        {
            nint dc = CreateCompatibleDC(0), bitmap = 0, old = 0;
            try
            {
                var header = new BitmapInfo { Size = 40, Width = size, Height = -size, Planes = 1, BitCount = 32 };
                bitmap = CreateDIBSection(dc, ref header, 0, out nint bits, 0, 0);
                if (dc == 0 || bitmap == 0 || bits == 0) return null;
                old = SelectObject(dc, bitmap);
                var bytes = new byte[size * size * 4]; Array.Fill(bytes, background); Marshal.Copy(bytes, 0, bits, bytes.Length);
                if (!DrawIconEx(dc, 0, 0, icon, size, size, 0, 0, 3)) return null;
                Marshal.Copy(bits, bytes, 0, bytes.Length); return bytes;
            }
            finally { if (old != 0) SelectObject(dc, old); if (bitmap != 0) DeleteObject(bitmap); if (dc != 0) DeleteDC(dc); }
        }
        var black = Paint(0); var white = Paint(255);
        if (black is null || white is null) return null;
        for (int i = 0; i < black.Length; i += 4)
        {
            int alpha = 255 - Math.Clamp((white[i] - black[i] + white[i + 1] - black[i + 1] + white[i + 2] - black[i + 2]) / 3, 0, 255);
            black[i + 3] = (byte)alpha;
            for (int j = 0; j < 3; j++) black[i + j] = (byte)Math.Min(alpha, black[i + j]);
        }
        return black;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo { public nint Icon; public int IconIndex; public uint Attributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellExecuteInfo
    {
        public int Size; public uint Mask; public nint Window;
        public string? Verb, File, Parameters, Directory; public int Show; public nint Instance, IdList;
        public string? Class; public nint ClassKey; public uint HotKey; public nint Icon, Process;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo { public uint Size; public int Width, Height; public ushort Planes, BitCount; public uint Compression, SizeImage; public int XPels, YPels; public uint Used, Important; }
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint model);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern nint SHGetFileInfo(string path, uint attributes, ref ShellFileInfo info, uint size, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShellExecuteEx(ref ShellExecuteInfo info);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHParseDisplayName(string name, nint context, out nint id, uint mask, out uint attributes);
    [DllImport("shell32.dll")] private static extern int SHOpenFolderAndSelectItems(nint id, uint count, nint children, uint flags);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] private static extern bool DrawIconEx(nint dc, int x, int y, nint icon, int width, int height, uint step, nint brush, uint flags);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint value);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
}
