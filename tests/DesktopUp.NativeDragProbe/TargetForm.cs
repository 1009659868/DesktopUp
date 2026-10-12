using System.Diagnostics;
using System.Runtime.InteropServices;
using DesktopUp.Windows;

namespace DesktopUp.NativeDragProbe;

internal sealed class TargetForm : Form
{
    private readonly ProbeLog log;
    private readonly Panel underlay = new() { BackColor = Color.FromArgb(29, 52, 59) };
    private readonly Label status = new() { AutoSize = false };
    private readonly ComboBox mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 380 };
    private readonly CheckBox helper = new() { Text = "Shell 跟手图像", Checked = true, AutoSize = true };
    private readonly CheckBox enabled = new() { Text = "允许探针落下（不执行）", Checked = true, AutoSize = true };
    private readonly CheckBox collapsed = new() { Text = "收起态", AutoSize = true };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 250 };
    private TargetSurface? surface;
    private Form? blocker;
    private int clicks, overs, enters, drops, ticks;
    private string latest = "尚无 OLE 回调";
    private bool closing;

    public TargetForm(ProbeLog log, string root, bool smoke)
    {
        this.log = log;
        Text = "DesktopUp D1 · 全区域命中与图像探针（不移动文件）";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new(900, 680); MinimumSize = Size;
        var header = new Label { Text = "① 普通点击蓝色空白，看穿透计数  ② 从独立源窗口直接拖入空白  ③ 拖出、再拖回、Esc", AutoSize = true, Location = new(20, 18) };
        var controls = new FlowLayoutPanel { Location = new(20, 55), Size = new(850, 90), WrapContents = true };
        mode.Items.AddRange(["候选 A · 全矩形 + HTTRANSPARENT", "候选 B · 全矩形 + WS_EX_TRANSPARENT", "对照 C · 稀疏 HRGN（旧输入岛）", "反例 D · 全矩形不穿透"]);
        mode.SelectedIndex = 0;
        var source = new Button { Text = "打开独立源窗口", AutoSize = true };
        source.Click += (_, _) =>
        {
            var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
            info.ArgumentList.Add("--source"); info.ArgumentList.Add(root);
            using var process = Process.Start(info);
            log.Write("source-launched", new { pid = process?.Id });
        };
        var cover = new Button { Text = "覆盖目标（前台反例）", AutoSize = true };
        cover.Click += (_, _) =>
        {
            if (blocker is { IsDisposed: false }) { blocker.Close(); return; }
            blocker = new Form { Text = "前台覆盖：海报不得抢接收", StartPosition = FormStartPosition.Manual, Bounds = new Rectangle(underlay.PointToScreen(new(180, 110)), new(260, 190)), BackColor = Color.DarkSlateGray };
            blocker.Controls.Add(new Label { Text = "拖到本窗口时，目标海报应 Leave。\n关闭后再继续。", Dock = DockStyle.Fill, ForeColor = Color.White });
            blocker.Show();
        };
        controls.Controls.AddRange([mode, helper, enabled, collapsed, source, cover]);
        underlay.SetBounds(20, 158, 850, 420);
        underlay.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(110, 179, 179));
            e.Graphics.DrawRectangle(pen, 4, 4, underlay.Width - 9, underlay.Height - 9);
            e.Graphics.DrawString("透明内容留白 · 普通点击应到达底层；文件拖入应到达 OLE 目标", Font, Brushes.White, 22, 80);
            e.Graphics.FillRectangle(Brushes.Teal, 24, 130, 110, 100);
            e.Graphics.DrawString("模拟 Item", Font, Brushes.White, 40, 169);
            e.Graphics.DrawString("模拟标题 / 云标题（仅此处常驻命中）", Font, Brushes.LightBlue, 22, 16);
        };
        underlay.MouseDown += (_, e) => { clicks++; log.Write("underlay-click", new { e.X, e.Y, button = e.Button.ToString(), mode = mode.SelectedIndex }); };
        status.SetBounds(20, 590, 850, 74);
        Controls.AddRange([header, controls, underlay, status]);
        Shown += (_, _) => { Rebuild(); timer.Start(); };
        LocationChanged += (_, _) => Reposition();
        SizeChanged += (_, _) => Reposition();
        mode.SelectedIndexChanged += (_, _) => Rebuild();
        helper.CheckedChanged += (_, _) => Rebuild();
        collapsed.CheckedChanged += (_, _) => Rebuild();
        timer.Tick += (_, _) =>
        {
            status.Text = $"穿透点击 {clicks} · Enter {enters} · Over {overs} · 记录松手 {drops}\n{latest}\n本探针没有文件执行器；计数不是人工视觉通过。目标和源是不同进程。";
            if (smoke && ++ticks % 2 == 0)
            {
                var blank = underlay.PointToScreen(new(350, 300));
                var title = underlay.PointToScreen(new(70, 20));
                log.Write("smoke", new { mode = mode.SelectedIndex, registered = surface is not null, surface = surface?.Handle.ToString(),
                    blankWindow = TargetSurface.WindowAt(blank).ToString(), titleWindow = TargetSurface.WindowAt(title).ToString(), underlayWindow = underlay.Handle.ToString(),
                    scope = "create/register/WindowFromPoint/dispose only; NOT native OLE mouse/visual PASS" });
                if (mode.SelectedIndex == 3) Close(); else mode.SelectedIndex++;
            }
        };
        FormClosing += (_, _) => { closing = true; timer.Stop(); surface?.Close(); blocker?.Close(); };
        FormClosed += (_, _) => timer.Dispose();
    }
    private void Record(string name, object? data)
    {
        if (name == "enter") enters++;
        if (name == "over") overs++;
        if (name == "drop") drops++;
        if (name != "over") latest = name;
        log.Write(name, new { mode = mode.SelectedIndex, helper = helper.Checked, collapsed = collapsed.Checked, detail = data });
    }
    private void Rebuild()
    {
        if (!Visible || closing) return;
        surface?.Close();
        surface = new TargetSurface(mode.SelectedIndex, helper.Checked, () => enabled.Checked, Record);
        Reposition(); surface.Show(this);
        Record("target-created", new { hwnd = $"0x{surface.Handle:X}", dpi = surface.DeviceDpi });
    }
    private void Reposition()
    {
        if (surface is null || surface.IsDisposed || closing) return;
        var bounds = underlay.RectangleToScreen(underlay.ClientRectangle);
        if (collapsed.Checked) bounds.Height = 52;
        surface.Bounds = bounds;
        surface.ApplyShape();
    }
}

internal sealed class TargetSurface : Form
{
    private readonly int mode;
    private readonly bool helper;
    private readonly Func<bool> enabled;
    private readonly Action<string, object?> record;
    private ProbeDropTarget? target;
    private bool registered;
    private bool oleInitialized;
    private NativeOleDragMonitor? monitor;
    private bool oleActive;
    private PosterDropReception? component;
    private readonly RoutingProbeLedger componentLedger=new();
    public TargetSurface(int mode, bool helper, Func<bool> enabled, Action<string, object?> record)
    {
        this.mode = mode; this.helper = helper; this.enabled = enabled; this.record = record;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black; Opacity = 1d / 255d;
        if (mode == 4) TransparencyKey = Color.Black;
        // No topmost, no full-screen overlay, no mouse hook and no button-state drag detector.
    }
    protected override bool ShowWithoutActivation => true;
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (mode == 4) e.Graphics.FillRectangle(Brushes.White, 0, 0, Width, Math.Min(40, Height));
    }
    protected override CreateParams CreateParams
    {
        get { var value = base.CreateParams; value.ExStyle |= 0x08000000 | 0x80 | 0x80000; if (mode == 1) value.ExStyle |= 0x20; return value; }
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Marshal.ThrowExceptionForHR(OleInitialize(0)); oleInitialized = true;
        try
        {
            if(mode>=7)
            {
                var attachment=new RoutingProbeAttachment(Handle);
                var host=new DesktopUp.Application.DesktopShellHostSnapshot(new(Handle,Handle,(uint)Environment.ProcessId),1,DateTimeOffset.UtcNow);
                var frame=new DesktopUp.Application.DesktopPixelRect(Left,Top,Width,Height);
                var view=new DesktopUp.Application.IncomingDropSurface(Guid.NewGuid(),"isolated",1,1,1,frame,1,1,new(0,0,Width,Height),DesktopUp.Domain.ContentFlowAxis.Vertical);
                component=new PosterDropReception(attachment,host,frame,()=>view,
                    ticket=>record("drop",new{ticket.Paths.Count,filesMoved=0}),componentLedger,new RoutingProbeLog(record),
                    preview=>{if(preview is not null){record("enter",new{preview.Count});if(mode==9)BeginInvoke(()=>component?.Suspend());}},
                    ()=>component is null||attachment.ReassertDesktopBottom(component.WindowHandle,host));
                ApplyShape();return;
            }
            target = new ProbeDropTarget(Handle, helper ? new ShellProbeImages() : new NullProbeImages(), record, enabled);
            Marshal.ThrowExceptionForHR(RegisterDragDrop(Handle, target)); registered = true; ApplyShape();
            if(mode==6)monitor=new NativeOleDragMonitor(active=>{oleActive=active;ApplyShape();record("ole-surface-state",new{active});});
        }
        catch { target?.Dispose(); target = null; OleUninitialize(); oleInitialized = false; throw; }
    }
    public void ApplyShape()
    {
        if (!IsHandleCreated) return;
        var previous = Region;
        if(mode>=7||mode==6&&!oleActive)Region=new Region(Rectangle.Empty);
        else if (mode == 2)
        {
            var region = new Region(new Rectangle(0, 0, Width, Math.Min(52, Height)));
            if (Height > 130) region.Union(new Rectangle(24, 130, 110, Math.Min(100, Height - 130)));
            Region = region;
        }
        else Region = null;
        previous?.Dispose();
    }
    public void ApplyDiagnosticAlpha()
    { if(mode==5) SetLayeredWindowAttributes(Handle,0,0,2); }
    [DllImport("user32.dll")]private static extern bool SetLayeredWindowAttributes(nint window,uint key,byte alpha,uint flags);
    protected override void OnHandleDestroyed(EventArgs e)
    {
        component?.Dispose();component=null;
        if(mode>=7)record("component-resources",new{active=componentLedger.ActiveResources.Count});
        monitor?.Dispose();monitor=null;
        if (registered) { RevokeDragDrop(Handle); registered = false; }
        target?.Dispose(); target = null;
        if (oleInitialized) { OleUninitialize(); oleInitialized = false; }
        record("target-disposed", new { registered = false });
        base.OnHandleDestroyed(e);
    }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x84 && mode == 0) { message.Result = -1; return; }
        if (message.Msg == 0x21) { message.Result = 3; return; }
        if (message.Msg == 0x201) record("surface-click-swallowed", new { mode });
        base.WndProc(ref message);
    }
    [DllImport("ole32.dll")] private static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("ole32.dll")] private static extern int RegisterDragDrop(nint window, [MarshalAs(UnmanagedType.Interface)] IPhysicalFileDropTarget target);
    [DllImport("ole32.dll")] private static extern int RevokeDragDrop(nint window);
    public static nint WindowAt(Point point) => WindowFromPoint(new() { X = point.X, Y = point.Y });
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(FileDropPoint point);
}
