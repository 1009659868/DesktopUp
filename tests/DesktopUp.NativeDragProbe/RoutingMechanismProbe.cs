using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using DesktopUp.Windows;

namespace DesktopUp.NativeDragProbe;

// Isolated native routing integration: private payload or a nonexistent fixture path;
// no file executor, source file access or user data mutation.
// The receiver, click underlay and OLE source are distinct processes. No Explorer hooks.
internal sealed class RoutingMechanismProbe : Form
{
    private readonly ProbeLog log;
    private readonly string root;
    private readonly Label status = new() { Dock=DockStyle.Top, Height=60, Text="原生命中组合自检 · 仅测试窗口 · 不移动文件" };
    private readonly List<object> results=[];
    public RoutingMechanismProbe(ProbeLog log,string root)
    {
        this.log=log;this.root=root;Text="DesktopUp isolated routing";StartPosition=FormStartPosition.CenterScreen;
        ClientSize=new(600,430);Controls.Add(status);Shown+=async (_,_)=>await Run();
    }
    private async Task Run()
    {
        Process? bottom=null,source=null;TargetSurface? surface=null;
        try
        {
            if(!new NativeDesktopPointerPlatform().TryReadCursor(out _))throw new IOException("Interactive desktop unavailable; no input sent.");
            var r=RectangleToScreen(new(40,90,500,280));
            File.WriteAllText(Path.Combine(root,"routing-window.json"),JsonSerializer.Serialize(new RoutingArea(Handle.ToInt64(),Environment.ProcessId,r.X,r.Y,r.Width,r.Height)));
            bottom=Child("--routing-bottom");source=Child("--routing-source");
            await Wait("routing-ready");
            foreach(int mode in new[]{3,0,4,5,1,2,6,7,8,9})
            {
                // Test-only foreground stack; never used by the desktop product host.
                RoutingNative.SetWindowPos(new nint(long.Parse(File.ReadAllText(Path.Combine(root,"routing-bottom-hwnd")))),-1,0,0,0,0,0x53);
                int enters=0,drops=0,resources=-1;
                int before=int.Parse(File.ReadAllText(Path.Combine(root,"routing-clicks")));
                surface=new TargetSurface(mode,false,()=>true,(name,data)=>
                {if(name=="enter")enters++;if(name=="drop")drops++;if(name=="component-resources")resources=JsonSerializer.SerializeToElement(data).GetProperty("active").GetInt32();log.Write("routing-target",new{mode,name,data});});
                surface.Bounds=r;surface.Show(this);surface.ApplyDiagnosticAlpha();
                RoutingNative.SetWindowPos(surface.Handle,-1,0,0,0,0,0x53);
                status.Text=$"原生命中组合自检：机制 {mode}，请勿在测试区域操作";
                await Task.Delay(250);
                File.WriteAllText(Path.Combine(root,$"routing-go-{mode}"),"private source only");
                await Wait($"routing-done-{mode}");await Task.Delay(150);
                string worker=File.ReadAllText(Path.Combine(root,$"routing-done-{mode}"));
                int clicks=int.Parse(File.ReadAllText(Path.Combine(root,"routing-clicks")))-before;
                surface.Close();surface.Dispose();surface=null;
                bool pass=worker.StartsWith('{') && drops==(mode==7?2:mode is 0 or 3 or 6?1:0) &&
                    clicks==(mode is 0 or 3?0:mode>=7?6:mode==6?4:2) && (mode<7||resources==0);
                var result=new{mode,enters,drops,worker,clicks,resources,pass};
                results.Add(result);log.Write("routing-result",result);if(!pass)Environment.ExitCode=1;
                await Task.Delay(150);
            }
            File.WriteAllText(Path.Combine(root,"routing-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception e){log.Write("routing-failure",e.ToString());Environment.ExitCode=1;}
        finally
        {
            surface?.Close();File.WriteAllText(Path.Combine(root,"routing-stop"),"normal exit");
            await Task.Delay(500);bottom?.Dispose();source?.Dispose();Close();
        }
    }
    private async Task Wait(string name)
    {var timer=Stopwatch.StartNew();while(!File.Exists(Path.Combine(root,name))){if(timer.ElapsedMilliseconds>12000)throw new TimeoutException(name);await Task.Delay(80);}}
    private Process Child(string mode)
    {var p=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden};p.ArgumentList.Add(mode);p.ArgumentList.Add(root);return Process.Start(p)!;}
}

internal sealed record RoutingArea(long Owner,int ParentPid,int X,int Y,int Width,int Height);
internal sealed class RoutingMechanismWorker : Form
{
    private readonly System.Windows.Forms.Timer timer=new(){Interval=50};
    private readonly RoutingArea area;
    private readonly HashSet<int> completed=[];
    private readonly bool bottom;
    private readonly string root;
    private readonly ProbeLog log;
    private bool busy;
    private int clicks;
    public RoutingMechanismWorker(ProbeLog log,string root,bool bottom)
    {
        this.log=log;this.root=root;this.bottom=bottom;
        area=JsonSerializer.Deserialize<RoutingArea>(File.ReadAllText(Path.Combine(root,"routing-window.json")))!;
        FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;BackColor=Color.Teal;
        Bounds=bottom?new(area.X,area.Y,area.Width,area.Height):new(area.X-20,area.Y-20,4,4);
        Shown+=(_,_)=>
        {
            Bounds=bottom?new(area.X,area.Y,area.Width,area.Height):new(area.X-20,area.Y-20,4,4);
            log.Write("worker-geometry",new{bottom,Bounds,area});
            RoutingNative.SetWindowLongPtr(Handle,-8,new nint(area.Owner));
            if(bottom){File.WriteAllText(Path.Combine(root,"routing-bottom-hwnd"),Handle.ToInt64().ToString());File.WriteAllText(Path.Combine(root,"routing-ready"),Environment.ProcessId.ToString());Save();}
            timer.Start();
        };
        MouseDown+=(_,e)=>{clicks++;log.Write("native-underlay-click",new{clicks,e.Button});Save();};
        timer.Tick+=async (_,_)=>
        {
            if(File.Exists(Path.Combine(root,"routing-stop"))){if(!busy)Close();return;}
            if(bottom||busy)return;
            foreach(int mode in new[]{3,0,4,5,1,2,6,7,8,9})
                if(!completed.Contains(mode)&&File.Exists(Path.Combine(root,$"routing-go-{mode}")))
                {completed.Add(mode);busy=true;try{await Sample(mode);}finally{busy=false;}break;}
        };
        FormClosed+=(_,_)=>{timer.Stop();timer.Dispose();};
    }
    protected override bool ShowWithoutActivation=>true;
    protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000080;return p;}}
    protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=3;return;}base.WndProc(ref m);}
    private void Save()=>File.WriteAllText(Path.Combine(root,"routing-clicks"),clicks.ToString());
    private async Task Sample(int mode)
    {
        var old=Cursor.Position;var point=new Point(area.X+250,area.Y+200);
        bool initialized=false;
        try
        {
            // Guard against any unrelated window taking over the test point. Never click it.
            nint hit=TargetSurface.WindowAt(point);RoutingNative.GetWindowThreadProcessId(hit,out uint pid);
            int underlay=int.Parse(File.ReadAllText(Path.Combine(root,"routing-ready")));
            if(pid!=area.ParentPid && pid!=underlay)throw new IOException($"Test area obstructed by pid {pid}, hwnd {hit:X} at {point}; no input sent.");
            if(mode>=7)
            {
                RoutingNative.SetCapture(Handle);await Task.Delay(100);
                var ordinary=TargetSurface.WindowAt(point);RoutingNative.GetWindowThreadProcessId(ordinary,out uint ordinaryPid);
                RoutingNative.ReleaseCapture();
                if(ordinaryPid!=underlay)throw new IOException("Ordinary capture incorrectly armed OLE reception.");
            }
            Cursor.Position=point;await Task.Delay(100);
            RoutingNative.Click(false);await Task.Delay(130);RoutingNative.Click(true);await Task.Delay(130);
            Marshal.ThrowExceptionForHR(RoutingNative.OleInitialize(0));initialized=true;
            var attempts=new List<object>();
            for(int attempt=0;attempt<(mode>=7?2:1);attempt++)
            {
                using var data=new ProbeDataObject(Guid.NewGuid().ToByteArray());
                if(mode>=7)
                {
                    // Production decoder, but no executor. This nonexistent private path is never opened.
                    byte[] names=System.Text.Encoding.Unicode.GetBytes(Path.Combine(root,"never-executed.txt")+"\0\0");
                    byte[] bytes=new byte[20+names.Length];BitConverter.GetBytes(20).CopyTo(bytes,0);BitConverter.GetBytes(1).CopyTo(bytes,16);names.CopyTo(bytes,20);data.Put(15,bytes);
                }
                var clock=Stopwatch.StartNew();
                using var cancellation=new CancellationTokenSource();
                using var motion=new System.Threading.Timer(_=>
                {if(new NativeDesktopPointerPlatform().TryReadCursor(out var ignoredPoint))Cursor.Position=new(point.X+(clock.ElapsedMilliseconds/70%2==0?0:1),point.Y);else cancellation.Cancel();},null,70,70);
                var source=new TimedRoutingSource(clock,log.Write,mode==8,()=>cancellation.IsCancellationRequested);
                int result=ProbeSource.DoDragDrop(data,source,2,out uint effect);motion.Change(Timeout.Infinite,Timeout.Infinite);
                if(mode>=6){RoutingNative.Click(false);await Task.Delay(100);RoutingNative.Click(true);await Task.Delay(100);}
                attempts.Add(new{result,effect});
            }
            File.WriteAllText(Path.Combine(root,$"routing-done-{mode}"),JsonSerializer.Serialize(new{attempts,hit=hit.ToInt64(),pid,underlay,sourcePid=Environment.ProcessId,syntheticNativeInput=true}));
        }
        catch(Exception e){File.WriteAllText(Path.Combine(root,$"routing-done-{mode}"),e.ToString());}
        finally{if(initialized)RoutingNative.OleUninitialize();if(new NativeDesktopPointerPlatform().TryReadCursor(out _))Cursor.Position=old;}
    }
}
[ComVisible(true),ClassInterface(ClassInterfaceType.None)]
public sealed class TimedRoutingSource(Stopwatch clock,Action<string,object?> record,bool cancel=false,Func<bool>? interrupted=null):IProbeDropSource
{
    private nint last=-1;
    public int QueryContinueDrag(bool escape,uint keys)=>escape||interrupted?.Invoke()==true?0x40101:clock.ElapsedMilliseconds>=550?cancel?0x40101:0x40100:0;
    public int GiveFeedback(uint effect)
    {var capture=RoutingNative.GetCapture();if(capture!=last){last=capture;var text=new System.Text.StringBuilder(256);RoutingNative.GetClassName(capture,text,256);record("ole-capture",new{capture=capture.ToInt64(),windowClass=text.ToString()});}return 0x40102;}
}
internal static class RoutingNative
{
    [DllImport("user32.dll")]internal static extern bool SetWindowPos(nint h,nint after,int x,int y,int w,int height,uint flags);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]internal static extern nint SetWindowLongPtr(nint h,int index,nint value);
    [DllImport("user32.dll")]internal static extern uint GetWindowThreadProcessId(nint h,out uint pid);
    [DllImport("user32.dll")]internal static extern nint GetCapture();
    [DllImport("user32.dll")]internal static extern nint SetCapture(nint h);
    [DllImport("user32.dll")]internal static extern bool ReleaseCapture();
    [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="GetClassNameW")]internal static extern int GetClassName(nint h,System.Text.StringBuilder text,int count);
    [DllImport("ole32.dll")]internal static extern int OleInitialize(nint p);
    [DllImport("ole32.dll")]internal static extern void OleUninitialize();
    [StructLayout(LayoutKind.Sequential)]private struct Input{public uint Type;public Mouse Mouse;}
    [StructLayout(LayoutKind.Sequential)]private struct Mouse{public int X,Y;public uint Data,Flags,Time;public nuint Extra;}
    [DllImport("user32.dll",SetLastError=true)]private static extern uint SendInput(uint count,Input[] inputs,int size);
    internal static void Click(bool right)
    {if(!new NativeDesktopPointerPlatform().TryReadCursor(out _))throw new IOException("Interactive desktop unavailable; no click sent.");var inputs=new[]{new Input{Mouse=new(){Flags=right?8u:2u}},new Input{Mouse=new(){Flags=right?16u:4u}}};if(SendInput(2,inputs,Marshal.SizeOf<Input>())!=2)throw new IOException("Native test input rejected.");}
}
