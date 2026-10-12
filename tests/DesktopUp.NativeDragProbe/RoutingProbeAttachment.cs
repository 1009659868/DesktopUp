using DesktopUp.Application;

namespace DesktopUp.NativeDragProbe;

// Test-only placement keeps our opaque click witness above unrelated apps. No Explorer handles,
// changes to desktop attachment, or product topmost policy. The production component is unchanged.
internal sealed class RoutingProbeAttachment(nint owner):IDesktopLayerAttachment
{
    public DesktopLayerAttachResult Attach(nint windowHandle,DesktopShellHostSnapshot host,bool showWindow=false,bool inputTransparent=false)
    {RoutingNative.SetWindowLongPtr(windowHandle,-8,owner);bool ok=RoutingNative.SetWindowPos(windowHandle,-1,0,0,0,0,0x53);return new(ok,windowHandle,owner,owner,host.Generation,0,null);}
    public bool ReassertDesktopBottom(nint windowHandle,DesktopShellHostSnapshot host)=>RoutingNative.SetWindowPos(windowHandle,-1,0,0,0,0,0x13);
    public bool ApplyRoundedRegion(nint h,int w,int height,int r)=>false;
    public bool ApplyPreparedBounds(nint h,DesktopPixelRect b)=>false;
    public bool Detach(nint h)=>true;
    public bool IsAttached(nint h,DesktopShellHostSnapshot host)=>true;
}
internal sealed class RoutingProbeLog(Action<string,object?> record):IStructuredLog
{public void Write(string level,string name,object? data=null)=>record(name,new{level,data});}
internal sealed class RoutingProbeLedger:IResourceLedger
{
    private readonly HashSet<string> leases=[];
    public IReadOnlyCollection<string> ActiveResources=>leases;
    public IDisposable Acquire(string id,string kind)
    {string key=id+Guid.NewGuid().ToString("N");leases.Add(key);return new Lease(()=>leases.Remove(key));}
    public void WriteSnapshot(){}
    private sealed class Lease(Action close):IDisposable
    {private bool disposed;public void Dispose(){if(!disposed){disposed=true;close();}}}
}
