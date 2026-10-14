using System.Runtime.InteropServices;
using DesktopUp.Application;
using DesktopUp.ContentWorker;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed class OriginalIconResourceTests
{
    [Fact]
    public async Task PngCompressedIcoFrameRetainsNativeAlphaAndColour()
    {
        using var fixture = new Fixture();
        string path = Path.Combine(fixture.Root, "compressed.ico");
        Fixture.WriteIco(path, [(16, Fixture.Frame(16, 0, 0, 240)), (128, ContentIconDecodeTests.Png(128, 128, 220))]);
        var result = await Read(path);
        Assert.Equal(128, result.Width); Assert.Equal(128, result.Height);
        Assert.InRange(result.Pixels![0], 109, 111); Assert.InRange(result.Pixels[3], 127, 129);
        AssertPremultiplied(result);
    }

    [Theory]
    [InlineData(0)] [InlineData(48)]
    public async Task Native128ResourceKeepsResolutionAndIntentionalWhitespace(int margin)
    {
        using var fixture = new Fixture();
        string source = fixture.Icon("native.ico", 128, margin, 240, 0);
        byte[] before = File.ReadAllBytes(source);
        var result = await Read(source);
        Assert.Equal(128, result.Width); Assert.Equal(128, result.Height);
        Assert.Equal(255, result.Pixels![(64 * 128 + 64) * 4 + 3]);
        Assert.Equal(margin == 0 ? 255 : 0, result.Pixels[3]);
        Assert.Equal(before, File.ReadAllBytes(source));
        AssertPremultiplied(result);
    }

    [Fact]
    public async Task ExecutableAndStoredShortcutReadLargestOriginalFrameWithoutRunningOrRewriting()
    {
        using var fixture = new Fixture();
        string executable = fixture.Module();
        string shortcut = fixture.Shortcut("原始图标.lnk", executable, null);
        byte[] executableBefore = File.ReadAllBytes(executable), linkBefore = File.ReadAllBytes(shortcut);
        foreach (string path in new[] { executable, shortcut })
        {
            var result = await Read(path);
            Assert.Equal(128, result.Width); Assert.Equal(128, result.Height);
            Assert.True(result.Pixels![(64 * 128 + 64) * 4] > 200); // large cyan, not small red
            AssertPremultiplied(result);
        }
        Assert.Equal(executableBefore, File.ReadAllBytes(executable));
        Assert.Equal(linkBefore, File.ReadAllBytes(shortcut));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "opened.marker")));
    }

    [Theory]
    [InlineData(1)] [InlineData(-108)]
    public async Task ExplicitShortcutResourceIndexIsNotReplacedWithTargetsFirstGroup(int index)
    {
        using var fixture = new Fixture();
        string executable = fixture.Module();
        string shortcut = fixture.Shortcut("指定图标.lnk", executable, executable + "," + index);
        var result = await Read(shortcut);
        Assert.Equal(96, result.Width);
        int center = (48 * 96 + 48) * 4;
        Assert.True(result.Pixels![center + 2] > 200 && result.Pixels[center] < 20, "Expected custom red group, not default cyan.");
    }

    [Fact]
    public async Task ExplicitIcoWinsOverTargetAndMalformedLargeFrameFallsBackToValidSmallerFrame()
    {
        using var fixture = new Fixture();
        string executable = fixture.Module();
        string icon = fixture.Icon("custom.ico", 128, 0, 0, 240);
        var result = await Read(fixture.Shortcut("定制.lnk", executable, icon + ",0"));
        Assert.True(result.Pixels![(64 * 128 + 64) * 4 + 2] > 200);
        string damaged = Path.Combine(fixture.Root, "bad-large.ico");
        Fixture.WriteIco(damaged, [(256, new byte[40]), (32, Fixture.Frame(32, 0, 0, 240))]);
        var fallback = await Read(damaged);
        Assert.Equal(32, fallback.Width); Assert.Equal(32, fallback.Height);
        Assert.True(fallback.Pixels![(16 * 32 + 16) * 4 + 2] > 200);
    }

    [Fact]
    public void FilledShellCanvasNeedsMatchingIndependentSmallCandidate_NotCropping()
    {
        byte[] padded = Image(256, 48, 240, 255, true), small = Image(48, 48, 240, 255);
        byte[] before = padded.ToArray();
        Assert.True(ShellIconQuality.PreferSmaller(padded, 256, 256, small, 48, 48));
        Assert.Equal(before, padded);
        Assert.False(ShellIconQuality.PreferSmaller(padded, 256, 256, Image(48, 8, 240, 255), 48, 48)); // intentional padding scales
        Assert.False(ShellIconQuality.PreferSmaller(padded, 256, 256, Image(48, 48, 0, 255), 48, 48)); // different artwork
        Assert.False(ShellIconQuality.IsPossiblyPadded(Image(256, 48, 80, 96), 256, 256)); // translucent art
    }

    private static byte[] Image(int size, int body, byte blue, byte alpha, bool faintBorder = false)
    {
        byte[] pixels = new byte[size * size * 4]; int margin = (size - body) / 2;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            int at = (y * size + x) * 4;
            if (x >= margin && x < margin + body && y >= margin && y < margin + body)
            { pixels[at] = blue; pixels[at + 3] = alpha; }
            else if (faintBorder && (x == 0 || y == 0 || x == size - 1 || y == size - 1)) pixels[at + 3] = 24;
        }
        return pixels;
    }

    private static async Task<LocalContentResponse> Read(string path)
    {
        using var worker = new LocalContentWorkerClient(Path.Combine(AppContext.BaseDirectory, "content-test-helpers", "DesktopUp.ContentWorker.exe"));
        var result = await worker.RunAsync(new(Guid.NewGuid(), LocalContentAction.Icon, LocalContentReferenceProbe.Inspect(path).Item!), CancellationToken.None);
        Assert.True(LocalContentProtocol.ValidPixels(result.Pixels, result.Width, result.Height), result.Message);
        await worker.StopAsync(); return result;
    }

    private static void AssertPremultiplied(LocalContentResponse result)
    {
        for (int at = 0; at < result.Pixels!.Length; at += 4)
            for (int channel = 0; channel < 3; channel++) Assert.True(result.Pixels[at + channel] <= result.Pixels[at + 3]);
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "DesktopUp-original-icon-" + Guid.NewGuid().ToString("N"));
        public Fixture() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, true); // Only this fixture's newly created directory.
        public string Icon(string name, int size, int margin, byte blue, byte red)
        { string path = Path.Combine(Root, name); WriteIco(path, [(size, Frame(size, margin, blue, red))]); return path; }
        public static byte[] Frame(int size, int margin, byte blue, byte red)
        {
            using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory);
            int stride = ((size + 31) / 32) * 4;
            writer.Write(40); writer.Write(size); writer.Write(size * 2); writer.Write((short)1); writer.Write((short)32);
            writer.Write(0); writer.Write(size * size * 4 + stride * size); for (int i = 0; i < 4; i++) writer.Write(0);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                bool inside = x >= margin && y >= margin && x < size - margin && y < size - margin;
                writer.Write(inside ? blue : (byte)0); writer.Write((byte)0); writer.Write(inside ? red : (byte)0); writer.Write(inside ? (byte)255 : (byte)0);
            }
            writer.Write(new byte[stride * size]); return memory.ToArray();
        }
        public static void WriteIco(string path, (int Size, byte[] Data)[] frames)
        {
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write((short)0); writer.Write((short)1); writer.Write((short)frames.Length);
            int offset = 6 + frames.Length * 16;
            foreach (var frame in frames)
            { Entry(writer, frame.Size, frame.Data.Length); writer.Write(offset); offset += frame.Data.Length; }
            foreach (var frame in frames) writer.Write(frame.Data);
        }
        private static void Entry(BinaryWriter writer, int size, int length)
        {
            writer.Write((byte)(size == 256 ? 0 : size)); writer.Write((byte)(size == 256 ? 0 : size)); writer.Write((short)0);
            writer.Write((short)1); writer.Write((short)32); writer.Write(length);
        }
        public string Module()
        {
            string path = Path.Combine(Root, "resource-only.exe");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "content-test-helpers", "DesktopUp.ContentFixture.exe"), path);
            nint update = BeginUpdateResourceW(path, true); Assert.NotEqual(0, update);
            bool committed = false;
            try
            {
                void Group(int id, (int Size, byte[] Data, ushort Id)[] frames)
                {
                    using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory);
                    writer.Write((short)0); writer.Write((short)1); writer.Write((short)frames.Length);
                    foreach (var frame in frames)
                    { Entry(writer, frame.Size, frame.Data.Length); writer.Write(frame.Id); Assert.True(UpdateResourceW(update, 3, frame.Id, 0, frame.Data, (uint)frame.Data.Length)); }
                    byte[] directory = memory.ToArray(); Assert.True(UpdateResourceW(update, 14, id, 0, directory, (uint)directory.Length));
                }
                Group(107, [(32, Frame(32, 0, 0, 240), 1), (128, Frame(128, 0, 240, 0), 2)]);
                Group(108, [(96, Frame(96, 0, 0, 240), 3)]);
                Assert.True(EndUpdateResourceW(update, false)); committed = true;
            }
            finally { if (!committed) EndUpdateResourceW(update, true); }
            return path;
        }
        public string Shortcut(string name, string target, string? icon)
        {
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true)!)!;
            object? shortcut = null;
            try
            {
                string path = Path.Combine(Root, name); shortcut = ((dynamic)shell).CreateShortcut(path);
                ((dynamic)shortcut).TargetPath = target;
                if (icon is not null) ((dynamic)shortcut).IconLocation = icon;
                ((dynamic)shortcut).Save(); return path;
            }
            finally { if (shortcut is not null) Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell); }
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern nint BeginUpdateResourceW(string path, [MarshalAs(UnmanagedType.Bool)] bool delete);
        [DllImport("kernel32.dll", ExactSpelling = true)] private static extern bool UpdateResourceW(nint update, nint type, nint name, ushort language, byte[] data, uint length);
        [DllImport("kernel32.dll", ExactSpelling = true)] private static extern bool EndUpdateResourceW(nint update, [MarshalAs(UnmanagedType.Bool)] bool discard);
    }
}
