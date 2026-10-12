using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DesktopUp.NativeDragProbe;

// Native routing investigation only. Never opens a data object path or executes a file move.
// The bottom receiver lives in another process; the original D1 blue panel did not.
internal sealed class CrossProcessClickProbe : Form
{
    private readonly Label status=new(){AutoSize=false,Bounds=new(20,470,900,75)};
    private readonly System.Windows.Forms.Timer timer=new(){Interval=300};
    private readonly List<TargetSurface> surfaces=[];
    private readonly string root;
    private readonly ProbeLog log;
    private Process? bottom;
    private int aDrops,bDrops,aEnter,bEnter,starts,ends,captures;
    private WinEvent? callback;
    private nint hook;
    private bool closed;
    private int ticks;
    public CrossProcessClickProbe(ProbeLog log,string root,bool smoke=false)
    {
        this.root=root;this.log=log;
        Text="D3 留白穿透／拖入对照 · 不移动文件";ClientSize=new(940,560);StartPosition=FormStartPosition.CenterScreen;
        Controls.Add(new Label{Text="只测同一块留白：①左／右键点击 ②从 Explorer 拖一个测试文件进入并松手（仅计数，不移动）",AutoSize=true,Location=new(20,15)});
        var source=new Button{Text="打开独立测试源",Bounds=new(20,40,180,30)};
        source.Click+=(_,_)=>{using var child=Child("--source");};Controls.Add(source);Controls.Add(status);
        Shown+=(_,_)=>
        {
            PublishBounds();bottom=Child("--cross-underlay");
            callback=(_,ev,hwnd,_,_,thread,_)=>
            {
                if(ev==0xE)starts++;if(ev==0xF)ends++;if(ev==8)captures++;
                // Accessibility drag events are not assumed to be provided by every OLE source.
                log.Write("external-win-event",new{ev,hwnd=$"0x{hwnd:X}",thread});
            };
            hook=SetWinEventHook(8,15,0,callback,0,0,2); // OUTOFCONTEXT | SKIPOWNPROCESS, no injection.
            timer.Start();
        };
        LocationChanged+=(_,_)=>{if(Visible&&!closed){PublishBounds();PlaceSurfaces();}};
        timer.Tick+=(_,_)=>
        {
            if(File.Exists(Path.Combine(root,"cross-ready.json"))&&surfaces.Count==0)
            {
                for(int i=0;i<2;i++)
                {
                    int side=i;var s=new TargetSurface(i==0?0:4,true,()=>true,(name,data)=>
                    {if(name=="enter"){if(side==0)aEnter++;else bEnter++;}if(name=="drop"){if(side==0)aDrops++;else bDrops++;}log.Write("cross-target",new{side,name,data});});
                    surfaces.Add(s);s.Show(this);
                }
                PlaceSurfaces();
            }
            string clicks="等待独立进程底板";
            try{clicks=File.ReadAllText(Path.Combine(root,"cross-clicks.txt"));}catch(IOException){}
            status.Text=$"{clicks}\nA Enter {aEnter} / Drop {aDrops}；E Enter {bEnter} / Drop {bDrops}；外部拖放事件 {starts}/{ends}，捕获开始 {captures}\nA=当前全矩形+HTTRANSPARENT；E=全矩形+留白颜色键透明（待验证候选）。两组都必须点击和拖入通过。";
            if(smoke && ++ticks>=8)
            {
                bool ready=surfaces.Count==2&&bottom is {HasExited:false}&&bottom.Id!=Environment.ProcessId;
                Environment.ExitCode=ready?0:1;log.Write("cross-lifecycle",new{ready,pid=Environment.ProcessId,bottom=bottom?.Id,surfaces=surfaces.Count,nativeMouse=false});Close();
            }
        };
        FormClosing+=(_,_)=>
        {
            closed=true;timer.Stop();foreach(var s in surfaces)s.Close();
            File.WriteAllText(Path.Combine(root,"cross-stop"),"close owned helper normally");
            if(hook!=0){UnhookWinEvent(hook);hook=0;}GC.KeepAlive(callback);
        };
        FormClosed+=(_,_)=>{timer.Dispose();bottom?.Dispose();};
    }
    private Process Child(string mode)
    {
        var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false};start.ArgumentList.Add(mode);start.ArgumentList.Add(root);
        return Process.Start(start)??throw new IOException("Probe child could not start.");
    }
    private Rectangle Area=>RectangleToScreen(new Rectangle(20,90,900,350));
    private void PublishBounds()=>File.WriteAllText(Path.Combine(root,"cross-bounds.json"),JsonSerializer.Serialize(new[]{Area.X,Area.Y,Area.Width,Area.Height}));
    private void PlaceSurfaces()
    {
        var area=Area;
        for(int i=0;i<surfaces.Count;i++)
        {
            surfaces[i].Bounds=new(area.X+i*450,area.Y,440,350);
            SetWindowPos(surfaces[i].Handle,0,0,0,0,0,0x13); // Only our probe: TOP (not TOPMOST), no activation/geometry.
        }
    }
    private delegate void WinEvent(nint hook,uint ev,nint hwnd,int objectId,int childId,uint thread,uint time);
    [DllImport("user32.dll")]private static extern nint SetWinEventHook(uint min,uint max,nint module,WinEvent proc,uint pid,uint thread,uint flags);
    [DllImport("user32.dll")]private static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")]private static extern bool SetWindowPos(nint window,nint after,int x,int y,int cx,int cy,uint flags);
}

internal sealed class CrossProcessUnderlay : Form
{
    private readonly System.Windows.Forms.Timer timer=new(){Interval=200};
    private readonly string root;
    private readonly int[] clicks=new int[4];
    public CrossProcessUnderlay(ProbeLog log,string root)
    {
        this.root=root;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;BackColor=Color.Teal;
        var initial=JsonSerializer.Deserialize<int[]>(File.ReadAllText(Path.Combine(root,"cross-bounds.json")))!;
        Bounds=new(initial[0],initial[1],initial[2],initial[3]);
        timer.Tick+=(_,_)=>
        {
            if(File.Exists(Path.Combine(root,"cross-stop"))){Close();return;}
            try{var b=JsonSerializer.Deserialize<int[]>(File.ReadAllText(Path.Combine(root,"cross-bounds.json")))!;Bounds=new(b[0],b[1],b[2],b[3]);}catch(IOException){}catch(JsonException){}
        };
        Shown+=(_,_)=>{File.WriteAllText(Path.Combine(root,"cross-ready.json"),JsonSerializer.Serialize(new{pid=Environment.ProcessId,hwnd=$"0x{Handle:X}"}));Save();timer.Start();};
        MouseDown+=(_,e)=>{int side=e.X<450?0:1;int button=e.Button==MouseButtons.Right?1:0;clicks[side*2+button]++;log.Write("cross-underlay-click",new{side,e.X,e.Y,e.Button});Save();Invalidate();};
        FormClosed+=(_,_)=>{timer.Stop();timer.Dispose();};
    }
    protected override bool ShowWithoutActivation=>true;
    protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000080;return p;}}
    protected override void WndProc(ref Message message){if(message.Msg==0x21){message.Result=3;return;}base.WndProc(ref message);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);e.Graphics.DrawLine(Pens.White,445,0,445,350);
        e.Graphics.DrawString("A 当前机制\n\n在此留白点击／拖入",Font,Brushes.White,45,85);
        e.Graphics.DrawString("E 颜色键透明候选\n\n在此留白点击／拖入",Font,Brushes.White,495,85);
        e.Graphics.DrawString($"独立底板进程 {Environment.ProcessId}；不移动文件",Font,Brushes.White,45,305);
    }
    private void Save()=>File.WriteAllText(Path.Combine(root,"cross-clicks.txt"),$"独立底板点击：A 左 {clicks[0]} / 右 {clicks[1]}；E 左 {clicks[2]} / 右 {clicks[3]}");
}
