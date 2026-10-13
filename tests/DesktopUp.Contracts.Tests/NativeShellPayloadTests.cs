using System.Runtime.InteropServices;
using DesktopUp.Windows;
using F=DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class NativeShellPayloadTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public void RealShellOrMixedParentPayloadPreservesPhysicalFilesAndPreferredMove(bool mixed)
    {
        using var f=new F(); string first=f.File("actual-shortcut.lnk"), second;
        if(mixed){second=Path.Combine(f.DirectoryRoot,"second.txt");File.WriteAllText(second,"owned-content");}
        else second=f.File("second.txt");
        Sta(()=>
        {
            using var source=new NativeShellDragSource(0,[first,second],()=>true,_=>{});
            Assert.Equal(new[]{first,second},NativeFileDropTarget.ReadPaths(source.Data));
            short preferred=unchecked((short)NativeDragDataObject.RegisterClipboardFormat("Preferred DropEffect"));
            Assert.True(NativeDragDataObject.TryReadBytes(source.Data,preferred,out var bytes));Assert.Equal(2u,BitConverter.ToUInt32(bytes));
            using var helper=new ShellDropImages();
            nint bitmap=CreateBitmap(16,16,1,32,Enumerable.Repeat((byte)255,16*16*4).ToArray());
            try {helper.InitializeSource(bitmap,16,16,5,7,source.Data);bitmap=0;}
            finally {if(bitmap!=0)DeleteObject(bitmap);}
            Assert.Equal(0x40101,source.QueryContinueDrag(true,1)); // No native mouse injected.
            source.Cancel();Assert.Equal(0x40101,source.QueryContinueDrag(false,1));
        });
        Assert.Equal("owned-content",File.ReadAllText(first));Assert.Equal("owned-content",File.ReadAllText(second));
    }
    [Fact] public void StaleOnlyDragNeverAdvertisesInventedFilesToExplorer()
        => Sta(()=> {using var source=new NativeShellDragSource(0,[],()=>true,_=>{});Assert.Null(NativeFileDropTarget.ReadPaths(source.Data));});
    private static void Sta(Action check)
    {
        Exception? error=null;
        var thread=new Thread(()=>
        {
            bool initialized=false;
            try {Marshal.ThrowExceptionForHR(OleInitialize(0));initialized=true;check();}
            catch(Exception e){error=e;}
            finally{if(initialized)OleUninitialize();}
        }){IsBackground=true};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        if(error is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
    [DllImport("ole32.dll")]private static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")]private static extern void OleUninitialize();
    [DllImport("gdi32.dll")]private static extern nint CreateBitmap(int width,int height,uint planes,uint bits,byte[] pixels);
    [DllImport("gdi32.dll")]private static extern bool DeleteObject(nint bitmap);
}
