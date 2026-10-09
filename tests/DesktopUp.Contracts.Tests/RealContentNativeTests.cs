using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed class RealContentNativeTests
{
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "DesktopUp-content-native-" + Guid.NewGuid().ToString("N"));
        public Fixture() => Directory.CreateDirectory(Root);
        public string File(string name) { string path = Path.Combine(Root,name); System.IO.File.WriteAllText(path,"owned-content"); return path; }
        public static string Helper(string name) => Path.Combine(AppContext.BaseDirectory,"content-test-helpers",name+".exe");
        public void Dispose() => Directory.Delete(Root,true);
    }
    [Fact] public async Task ActualShellIconIsBoundedPremultipliedRasterAndNamesAreUnicode()
    {
        using var f=new Fixture();var path=f.File("测试文件.txt");
        using var worker=new LocalContentWorkerClient(Fixture.Helper("DesktopUp.ContentWorker"));
        var item=LocalContentReferenceProbe.Inspect(path).Item!;
        var result=await worker.RunAsync(new(Guid.NewGuid(),LocalContentAction.Icon,item),CancellationToken.None);
        Assert.Equal(ContentAvailability.Available,result.Availability);Assert.Equal("测试文件.txt",result.Name);
        Assert.True(LocalContentProtocol.ValidPixels(result.Pixels,result.Width,result.Height),result.Message);
        Assert.Contains(result.Pixels!.Where((_,i)=>i%4==3),alpha=>alpha>0);
        var pixels=result.Pixels!;
        for(int i=0;i<pixels.Length;i+=4)
            for(int j=0;j<3;j++) Assert.True(pixels[i+j]<=pixels[i+3]);
        Assert.Equal("owned-content",System.IO.File.ReadAllText(path));await worker.StopAsync();
    }
    [Fact] public async Task MissingSourceIsNotOpenedAndOriginalIdentitySurvives()
    {
        using var f=new Fixture();string path=f.File("missing.txt");var item=LocalContentReferenceProbe.Inspect(path).Item!;
        System.IO.File.Delete(path); // this test's own file only
        using var worker=new LocalContentWorkerClient(Fixture.Helper("DesktopUp.ContentWorker"));
        var result=await worker.RunAsync(new(Guid.NewGuid(),LocalContentAction.Open,item),CancellationToken.None);
        Assert.NotEqual(ContentAvailability.Available,result.Availability);Assert.Equal(ContentOpenEvidence.None,result.Evidence);
        Assert.Equal(path,item.Target.Locator);await worker.StopAsync();
    }
    [Fact] public async Task ShellReadsTheLargeIconResourceInsteadOfUpscalingTheSmallRedFrame()
    {
        using var f = new Fixture(); string path = Path.Combine(f.Root, "多尺寸图标.ico");
        byte[] Frame(int size)
        {
            using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
            int stride = ((size + 31) / 32) * 4;
            w.Write(40); w.Write(size); w.Write(size * 2); w.Write((short)1); w.Write((short)32);
            w.Write(0); w.Write(size * size * 4 + stride * size); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                bool edge = x < 4 || y < 4 || x >= size - 4 || y >= size - 4;
                w.Write((byte)(edge ? 0 : size == 256 ? 240 : 0));
                w.Write((byte)(edge ? 0 : size == 256 ? (x % 2 == 0 ? 160 : 80) : 0));
                w.Write((byte)(edge ? 0 : size == 256 ? 0 : 240)); w.Write((byte)(edge ? 0 : 255));
            }
            for (int y = 0; y < size; y++) for (int b = 0; b < stride; b++)
            {
                byte mask = 0;
                for (int bit = 0; bit < 8; bit++) { int x = b * 8 + bit; if (x < 4 || y < 4 || x >= size - 4 || y >= size - 4) mask |= (byte)(128 >> bit); }
                w.Write(mask);
            }
            return stream.ToArray();
        }
        int[] sizes = [16, 32, 48, 256]; var frames = sizes.Select(Frame).ToArray();
        using (var file = new BinaryWriter(System.IO.File.Create(path)))
        {
            file.Write((short)0); file.Write((short)1); file.Write((short)frames.Length);
            int offset = 6 + frames.Length * 16;
            for (int i = 0; i < frames.Length; i++)
            {
                file.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); file.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                file.Write((short)0); file.Write((short)1); file.Write((short)32); file.Write(frames[i].Length); file.Write(offset); offset += frames[i].Length;
            }
            foreach (var frame in frames) file.Write(frame);
        }
        byte[] original = System.IO.File.ReadAllBytes(path);
        using var worker = new LocalContentWorkerClient(Fixture.Helper("DesktopUp.ContentWorker"));
        var item = LocalContentReferenceProbe.Inspect(path).Item!;
        var result = await worker.RunAsync(new(Guid.NewGuid(), LocalContentAction.Icon, item), CancellationToken.None);
        Assert.True(LocalContentProtocol.ValidPixels(result.Pixels, result.Width, result.Height), result.Message);
        Assert.Equal(256, result.Width); Assert.Equal(256, result.Height);
        byte[] pixels = result.Pixels!; int center = (128 * 256 + 128) * 4;
        Assert.True(pixels[center] > 200 && pixels[center + 2] < 30, "Large cyan frame was not chosen.");
        Assert.True(Math.Abs(pixels[center + 1] - pixels[center + 5]) >= 60, "Native single-pixel detail was lost.");
        Assert.Equal(0, pixels[3]); Assert.Equal(255, pixels[center + 3]);
        Assert.Equal(original, System.IO.File.ReadAllBytes(path)); await worker.StopAsync();
    }
    [Fact] public async Task ShellHandoffToOwnedNoUiExecutableIsRealAndDoesNotWaitForTarget()
    {
        using var f=new Fixture();string source=Path.GetDirectoryName(Fixture.Helper("DesktopUp.ContentFixture"))!;
        foreach(string file in Directory.GetFiles(source,"DesktopUp.ContentFixture.*")) System.IO.File.Copy(file,Path.Combine(f.Root,Path.GetFileName(file)));
        var item=LocalContentReferenceProbe.Inspect(Path.Combine(f.Root,"DesktopUp.ContentFixture.exe")).Item!;
        using var worker=new LocalContentWorkerClient(Fixture.Helper("DesktopUp.ContentWorker"));
        var result=await worker.RunAsync(new(Guid.NewGuid(),LocalContentAction.Open,item),CancellationToken.None);
        Assert.Equal(ContentOpenEvidence.ShellAccepted,result.Evidence);Assert.Equal(ContentOpenOutcome.Succeeded,result.Outcome);
        for(int i=0;i<100&&!System.IO.File.Exists(Path.Combine(f.Root,"opened.marker"));i++) await Task.Delay(50);
        Assert.Equal("owned-fixture-opened",System.IO.File.ReadAllText(Path.Combine(f.Root,"opened.marker")));
        await worker.StopAsync(); await Task.Delay(100); // target exits independently; never terminated by the host
    }
    [Theory] [InlineData("crash")] [InlineData("oversize")] [InlineData("invalid")]
    public async Task BrokenDisposableHelperFailsClosed(string mode)
    {
        using var f=new Fixture();var item=LocalContentReferenceProbe.Inspect(f.File("source.txt")).Item! with {DisplayName=mode};
        using var worker=new LocalContentWorkerClient(Fixture.Helper("DesktopUp.ContentFixture"));
        var result=await worker.RunAsync(new(Guid.NewGuid(),LocalContentAction.Icon,item),CancellationToken.None);
        Assert.Equal(ContentOpenEvidence.None,result.Evidence);Assert.Null(result.Pixels);await worker.StopAsync();
    }
    [Fact] public async Task ExitCancelsHangingOwnedHelpersAndWaitingQueue()
    {
        using var f=new Fixture();var item=LocalContentReferenceProbe.Inspect(f.File("source.txt")).Item! with {DisplayName="hang"};
        using var worker=new LocalContentWorkerClient(Fixture.Helper("DesktopUp.ContentFixture"));
        var pending=Enumerable.Range(0,8).Select(_=>worker.RunAsync(new(Guid.NewGuid(),LocalContentAction.Icon,item),CancellationToken.None).AsTask()).ToArray();
        await Task.Delay(200);await worker.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        foreach(var result in await Task.WhenAll(pending)) Assert.Equal(ContentOpenOutcome.Cancelled,result.Outcome);
    }
    [Fact] public async Task KnownHandoffIsNotErasedByCancellationWhileHelperExits()
    {
        using var f=new Fixture();var item=LocalContentReferenceProbe.Inspect(f.File("source.txt")).Item! with {DisplayName="ack-then-wait"};
        using var worker=new LocalContentWorkerClient(Fixture.Helper("DesktopUp.ContentFixture"));
        using var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var result=await worker.RunAsync(new(Guid.NewGuid(),LocalContentAction.Open,item),cancellation.Token);
        Assert.Equal(ContentOpenEvidence.ShellAccepted,result.Evidence);await worker.StopAsync();
    }
}
