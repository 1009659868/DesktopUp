using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DesktopUp.NativeDragProbe;

internal sealed class SourceForm : Form
{
    private readonly ProbeLog log;
    private readonly Panel icon = new() { BackColor = Color.FromArgb(230, 235, 239), Size = new(104, 104), Location = new(30, 98) };
    private readonly CheckBox multiple = new() { Text = "三个图标叠影", AutoSize = true, Location = new(30, 220) };
    private Point? press;
    private bool dragging, closing;
    public SourceForm(ProbeLog log)
    {
        this.log = log;
        Text = "D1 独立源 · 只有图像，不包含文件";
        StartPosition = FormStartPosition.Manual; Location = new(30, 100); ClientSize = new(420, 285);
        var label = new Label { Text = "按住下方图标并移动，不额外等待。\n拖入目标空白、拖出、拖回，Esc 取消。\n此图标不能被 Explorer 收纳；不提供文件载荷。", AutoSize = true, Location = new(24, 20) };
        icon.Paint += (_, e) => { using var image = MakeImage(multiple.Checked); e.Graphics.DrawImageUnscaled(image, 0, 0); };
        multiple.CheckedChanged += (_, _) => icon.Invalidate();
        icon.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) press = e.Location; };
        icon.MouseUp += (_, _) => press = null;
        icon.MouseMove += (_, e) =>
        {
            if (dragging || press is not { } anchor || (e.Button & MouseButtons.Left) == 0) return;
            var area = new Rectangle(anchor.X - SystemInformation.DragSize.Width / 2, anchor.Y - SystemInformation.DragSize.Height / 2, SystemInformation.DragSize.Width, SystemInformation.DragSize.Height);
            if (area.Contains(e.Location)) return;
            StartDrag(anchor);
        };
        FormClosing += (_, _) => closing = true;
        Controls.AddRange([label, icon, multiple]);
    }
    private void StartDrag(Point anchor)
    {
        dragging = true; press = null;
        bool initialized = false;
        try
        {
            Marshal.ThrowExceptionForHR(OleInitialize(0)); initialized = true;
            using var data = new ProbeDataObject(Guid.NewGuid().ToByteArray());
            data.FormatWritten = (format, bytes) => log.Write("source-set-private-format", new { format, bytes });
            using var image = MakeImage(multiple.Checked);
            nint bitmap = image.GetHbitmap(Color.FromArgb(0));
            using var helper = new ShellProbeImages();
            try { helper.InitializeSource(bitmap, image.Width, image.Height, anchor.X, anchor.Y, data); }
            catch { DeleteObject(bitmap); throw; }
            // Successful initialization transfers the bitmap to the Shell image manager.
            log.Write("source-drag-start", new { anchor.X, anchor.Y, dpi = DeviceDpi, multiple = multiple.Checked, imageOwner = "Shell", nativeFilePayload = false });
            var source = new ProbeSource(log.Write, () => closing);
            int result = ProbeSource.DoDragDrop(data, source, 2, out var effect);
            uint? performed = null;
            short format = unchecked((short)ProbeDataObject.RegisterClipboardFormat("Performed DropEffect"));
            if (ProbeDataObject.TryReadBytes(data, format, out var bytes) && bytes.Length >= 4) performed = BitConverter.ToUInt32(bytes);
            log.Write("source-drag-ended", new { result, effect, performed, verdict = ProbeOutgoingVerdict.Classify(result, effect, performed), filesMoved = 0 });
        }
        catch (Exception error) { log.Write("source-error", error.ToString()); MessageBox.Show(error.Message, "D1 图像初始化失败，不启动无图像拖动"); }
        finally { if (initialized) OleUninitialize(); dragging = false; if (!icon.IsDisposed) icon.Invalidate(); }
    }
    private static Bitmap MakeImage(bool multiple)
    {
        var bitmap = new Bitmap(104, 104, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        using var paper = new SolidBrush(Color.FromArgb(242, 247, 251));
        using var blue = new SolidBrush(Color.FromArgb(34, 118, 182));
        if (multiple) { graphics.FillRectangle(Brushes.SlateGray, 26, 19, 62, 74); graphics.FillRectangle(Brushes.LightSteelBlue, 20, 13, 62, 74); }
        graphics.FillRectangle(blue, 12, 6, 64, 78);
        graphics.FillRectangle(paper, 17, 11, 54, 55);
        using var font = new Font("Segoe UI", 18, FontStyle.Bold, GraphicsUnit.Pixel);
        graphics.DrawString("D1", font, blue, 27, 28);
        if (multiple) { graphics.FillEllipse(Brushes.Orange, 64, 67, 32, 32); graphics.DrawString("3", font, Brushes.Black, 72, 71); }
        return bitmap;
    }
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint handle);
    [DllImport("ole32.dll")] private static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
}
