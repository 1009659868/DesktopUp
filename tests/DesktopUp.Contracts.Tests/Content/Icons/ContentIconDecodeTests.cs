using DesktopUp.Windows;
using System.Buffers.Binary;
using System.IO.Compression;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentIconDecodeTests
{
    private sealed class Files : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "DesktopUp.Icon.Decode." + Guid.NewGuid().ToString("N"));
        public Files() => Directory.CreateDirectory(Root);
        public ContentItem Write(string name, byte[] bytes)
        {
            string path = Path.Combine(Root, name); File.WriteAllBytes(path, bytes);
            return LocalContentReferenceProbe.Inspect(path).Item!;
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
    private static LocalContentWorkerClient Worker(string name = "DesktopUp.ContentWorker") =>
        new(Path.Combine(AppContext.BaseDirectory, "content-test-helpers", name + ".exe"));

    [Theory] [InlineData(32,16,32,16)] [InlineData(512,128,256,64)] [InlineData(128,512,64,256)]
    public async Task PngPreservesAspectAlphaAndSourceAndNeverUpscales(int w,int h,int expectedW,int expectedH)
    {
        using var f = new Files(); byte[] source = Png(w,h); var item = f.Write("透明图标.PNG",source);
        using var worker = Worker();
        var asset = await ContentIconImport.LoadAsync(new LocalContentIconSource(), worker,
            item.Target.Locator, CancellationToken.None);
        Assert.Equal(expectedW,asset.Width); Assert.Equal(expectedH,asset.Height);
        var pixels = asset.Pixels;
        Assert.InRange(pixels[0],39,41); Assert.InRange(pixels[1],19,21);
        Assert.InRange(pixels[2],59,61); Assert.Equal(128,pixels[3]);
        Assert.Equal(source,File.ReadAllBytes(item.Target.Locator));
        await worker.StopAsync();
    }
    [Fact] public async Task IcoSelectsLargestFrameInsteadOfScalingTheSmallOne()
    {
        using var f = new Files(); using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        byte[][] images = [Png(16,16),Png(256,256,220)];
        writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)2);
        int offset = 38;
        for (int i=0;i<2;i++)
        {
            writer.Write((byte)(i==0?16:0)); writer.Write((byte)(i==0?16:0)); writer.Write((ushort)0);
            writer.Write((ushort)1);writer.Write((ushort)32); writer.Write(images[i].Length); writer.Write(offset); offset+=images[i].Length;
        }
        foreach(var bytes in images)writer.Write(bytes);
        var item = f.Write("frames.ico",stream.ToArray()); using var worker = Worker();
        var asset = await ContentIconImport.DecodeAsync(worker,item,CancellationToken.None);
        Assert.Equal(256,asset.Width); Assert.Equal(110,asset.Pixels[0]); await worker.StopAsync();
    }
    [Fact] public async Task StaticJpegUsesSameIsolatedDecoderAndProducesOpaquePixels()
    {
        using var f = new Files(); var item = f.Write("gray.jpeg",Jpeg()); using var worker = Worker();
        var asset = await ContentIconImport.DecodeAsync(worker,item,CancellationToken.None);
        Assert.Equal(3,asset.Width);Assert.Equal(2,asset.Height);
        Assert.All(asset.Pixels.Where((_,i)=>i%4==3),x=>Assert.Equal(255,x));
        Assert.InRange(asset.Pixels[0],126,130);await worker.StopAsync();
    }
    [Theory] [InlineData("svg")] [InlineData("gif")] [InlineData("mismatch")] [InlineData("truncated")]
    [InlineData("animated")] [InlineData("dimensions")] [InlineData("budget")] [InlineData("ico-count")]
    public async Task UnsupportedOrOversizedImagesNeverBecomeAssets(string kind)
    {
        using var f = new Files();byte[] bytes=Png(2,2);string ext=".png";
        switch(kind)
        {
            case "svg":ext=".svg";bytes="<svg onload='bad'/>"u8.ToArray();break;
            case "gif":ext=".gif";bytes="GIF89a"u8.ToArray();break;
            case "mismatch":ext=".jpg";break;
            case "truncated":bytes=bytes[..40];break;
            case "animated":
                using(var s=new MemoryStream()){s.Write(bytes.AsSpan(0,33));Chunk(s,"acTL"u8,new byte[8]);s.Write(bytes.AsSpan(33));bytes=s.ToArray();}
                break;
            case "dimensions": BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16),4097);break;
            case "budget":bytes=new byte[ContentIconImport.MaximumInputBytes+1];break;
            case "ico-count":ext=".ico";bytes=[0,0,1,0,33,0];break;
        }
        var item=f.Write("rejected"+ext,bytes);using var worker=Worker();
        await Assert.ThrowsAsync<InvalidDataException>(()=>ContentIconImport.DecodeAsync(worker,item,CancellationToken.None).AsTask());
        Assert.Equal(bytes,File.ReadAllBytes(item.Target.Locator));await worker.StopAsync();
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task CancellationAndServiceExitReapOnlyOwnedDecodeHelper(bool exit)
    {
        using var f=new Files();var item=f.Write("image.png",Png(2,2)) with { DisplayName="hang" };
        using var worker=Worker("DesktopUp.ContentFixture");using var cancel=new CancellationTokenSource();
        var task=ContentIconImport.DecodeAsync(worker,item,cancel.Token).AsTask();
        await Task.Delay(150);
        if(exit)await worker.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));else cancel.Cancel();
        var error=await Record.ExceptionAsync(()=>task);
        Assert.True(error is OperationCanceledException or InvalidDataException);
        await worker.StopAsync();Assert.True(File.Exists(item.Target.Locator));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledOrRejectedInspectionNeverStartsDecode(bool cancel)
    {
        using var f = new Files();
        var item = f.Write("image.png", Png(2, 2));
        using var stopping = new CancellationTokenSource();
        var source = new InspectSource(() =>
        {
            if (!cancel) throw new InvalidDataException("Rejected source");
            stopping.Cancel();
            return item;
        });
        var worker = new NoDecodeWorker();
        var error = await Record.ExceptionAsync(() =>
            ContentIconImport.LoadAsync(source, worker, item.Target.Locator, stopping.Token).AsTask());
        Assert.True(cancel ? error is OperationCanceledException : error is InvalidDataException);
        Assert.Equal(0, worker.Calls);
        Assert.True(File.Exists(item.Target.Locator));
    }
    private sealed class InspectSource(Func<ContentItem> inspect) : IContentIconSource
    {
        public ContentItem Inspect(string path) => inspect();
    }
    private sealed class NoDecodeWorker : ILocalContentWorker
    {
        public int Calls { get; private set; }
        public ValueTask<LocalContentResponse> RunAsync(LocalContentRequest request, CancellationToken token)
        {
            Calls++;
            throw new InvalidOperationException("Decode must not start.");
        }
    }

    // Owned test fixtures: minimal static RGBA PNG and grayscale baseline JPEG; no external assets.
    internal static byte[] Png(int w,int h,byte blue=80)
    {
        using var result=new MemoryStream();result.Write(new byte[]{137,80,78,71,13,10,26,10});
        byte[] header=new byte[13];BinaryPrimitives.WriteInt32BigEndian(header,w);BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4),h);
        header[8]=8;header[9]=6;Chunk(result,"IHDR"u8,header);
        using var compressed=new MemoryStream();
        using(var z=new ZLibStream(compressed,CompressionLevel.Fastest,true))
            for(int y=0;y<h;y++){z.WriteByte(0);for(int x=0;x<w;x++)z.Write(new byte[]{120,40,blue,128});}
        Chunk(result,"IDAT"u8,compressed.ToArray());Chunk(result,"IEND"u8,[]);return result.ToArray();
    }
    private static void Chunk(Stream s,ReadOnlySpan<byte> type,byte[] data)
    {
        Span<byte> n=stackalloc byte[4];BinaryPrimitives.WriteInt32BigEndian(n,data.Length);s.Write(n);s.Write(type);s.Write(data);
        uint crc=uint.MaxValue;foreach(byte b in type.ToArray().Concat(data))
        {crc^=b;for(int i=0;i<8;i++)crc=(crc&1)!=0?0xedb88320^(crc>>1):crc>>1;}
        BinaryPrimitives.WriteUInt32BigEndian(n,~crc);s.Write(n);
    }
    private static byte[] Jpeg()
    {
        using var s=new MemoryStream();s.Write(new byte[]{255,216});
        void Marker(byte marker,byte[] payload)
        {s.WriteByte(255);s.WriteByte(marker);s.WriteByte((byte)((payload.Length+2)>>8));s.WriteByte((byte)(payload.Length+2));s.Write(payload);}
        Marker(0xdb,new byte[]{0}.Concat(Enumerable.Repeat((byte)1,64)).ToArray());
        Marker(0xc0,[8,0,2,0,3,1,1,0x11,0]);
        byte[] table=new byte[18];table[1]=1;Marker(0xc4,table);
        table=(byte[])table.Clone();table[0]=0x10;Marker(0xc4,table);
        Marker(0xda,[1,1,0,0,63,0]);s.Write(new byte[]{0x3f,255,217});return s.ToArray();
    }
}
