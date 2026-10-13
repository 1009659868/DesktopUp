using System.Runtime.InteropServices;
using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterDropComponentTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public void OptionalDropUsesCoreWindowWithoutChangingInputOwnership(bool occupiedRegistration)=>Sta(()=>
    {
        var native=new Attachment();var ledger=new Ledger();var router=new PosterInputRouter();
        var host=new DesktopShellHostSnapshot(new(1,2,3),1,DateTimeOffset.UtcNow);
        var frame=new DesktopPixelRect(-3000,-3000,300,240);
        var layout=new PosterInputLayoutSnapshot(1,300,240,[new(PosterInputTargetKind.TitleBar,-1,new(10,5,280,30))],8);
        using var island=new PosterInputIslandManager(native,ledger,new Log(),()=>true,router,new("test",1),0);
        Assert.True(island.Create(host,frame,layout));
        AssertFrame(island.WindowHandle);int coreCount=ledger.ActiveResources.Count;
        if(occupiedRegistration)
        {
            using(var occupied=new NativeFileDropTarget(island.WindowHandle,()=>false,_=>{},ledger,new Log()))
            {
                Assert.Throws<COMException>(()=>island.InstallDropReceiver(()=>null,_=>{}));
                Assert.Equal(coreCount+1,ledger.ActiveResources.Count);
                AssertFrame(island.WindowHandle);
            }
            Assert.Equal(coreCount,ledger.ActiveResources.Count);
        }
        using(var drop=island.InstallDropReceiver(()=>null,_=>{}))
        {
            Assert.True(drop.IsReady);Assert.False(drop.IsHovering);
            Assert.Equal(coreCount+1,ledger.ActiveResources.Count); // only OLE registration, no extra HWND/hook/timer
            AssertFrame(island.WindowHandle);
            Assert.Throws<COMException>(()=>new NativeFileDropTarget(island.WindowHandle,()=>false,_=>{},ledger,new Log()));
            island.SuspendRouting();AssertFrame(island.WindowHandle,suspended:true);
            Assert.True(island.ApplyLayout(frame,layout with{Revision=2}));
            AssertFrame(island.WindowHandle);drop.Dispose();drop.Dispose();
            Assert.False(drop.IsReady);
        }
        Assert.Equal(coreCount,ledger.ActiveResources.Count);AssertFrame(island.WindowHandle);
        using(var again=island.InstallDropReceiver(()=>null,_=>{}))Assert.True(again.IsReady);
        island.Dispose();Assert.Empty(ledger.ActiveResources);Assert.Equal(0,router.Count);
    });
    private static void AssertFrame(nint h,bool suspended=false)
    {
        nint r=CreateRectRgn(0,0,0,0);
        try
        {
            Assert.NotEqual(0,GetWindowRgn(h,r));
            Assert.Equal(!suspended,PtInRegion(r,100,20));
            Assert.Equal(!suspended,PtInRegion(r,150,180));
            Assert.Equal(!suspended,PtInRegion(r,0,0));
            Assert.False(PtInRegion(r,300,180));Assert.False(PtInRegion(r,100,240));
        }
        finally{DeleteObject(r);}
    }
    private static void Sta(Action run)
    {
        Exception? failure=null;var t=new Thread(()=>{try{run();}catch(Exception e){failure=e;}});
        t.SetApartmentState(ApartmentState.STA);t.IsBackground=true;t.Start();Assert.True(t.Join(15000));
        if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private sealed class Attachment:IDesktopLayerAttachment
    {
        public DesktopLayerAttachResult Attach(nint h,DesktopShellHostSnapshot s,bool showWindow=false,bool inputTransparent=false)=>new(true,h,0,0,s.Generation,0,null);
        public bool ReassertDesktopBottom(nint h,DesktopShellHostSnapshot s)=>true;
        public bool ApplyRoundedRegion(nint h,int w,int height,int r)=>true;
        public bool ApplyPreparedBounds(nint h,DesktopPixelRect b)=>true;
        public bool Detach(nint h)=>true;
        public bool IsAttached(nint h,DesktopShellHostSnapshot s)=>true;
    }
    private sealed class Ledger:IResourceLedger
    {
        private readonly HashSet<string> active=[];
        public IReadOnlyCollection<string> ActiveResources=>active;
        public IDisposable Acquire(string id,string kind){string key=Guid.NewGuid().ToString();active.Add(key);return new Lease(()=>active.Remove(key));}
        public void WriteSnapshot(){}
        private sealed class Lease(Action end):IDisposable{private bool done;public void Dispose(){if(!done){done=true;end();}}}
    }
    private sealed class Log:IStructuredLog{public void Write(string l,string n,object? data=null){}}
    [DllImport("gdi32.dll")]private static extern nint CreateRectRgn(int l,int t,int r,int b);
    [DllImport("gdi32.dll")]private static extern bool DeleteObject(nint h);
    [DllImport("gdi32.dll")]private static extern bool PtInRegion(nint h,int x,int y);
    [DllImport("user32.dll")]private static extern int GetWindowRgn(nint h,nint r);
}
