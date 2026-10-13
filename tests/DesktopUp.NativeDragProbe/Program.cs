using System.Diagnostics;
using System.Text.Json;
using Forms = System.Windows.Forms.Application;

namespace DesktopUp.NativeDragProbe;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && (args[0].StartsWith("--routing-", StringComparison.Ordinal) ||
            args[0].StartsWith("--cross-", StringComparison.Ordinal)))
        {
            Console.Error.WriteLine("DEC-043: click-through experiments retired; use start-incoming-review.ps1 -FullFrameRevision.");
            return 2;
        }
        if (args.Length != 2 || args[0] is not ("--target" or "--source" or "--smoke" or "--check-environment" or "--cross-click" or "--cross-smoke" or "--cross-underlay" or "--routing-auto" or "--routing-repeat" or "--routing-bottom" or "--routing-source")) return 2;
        var root = Path.GetFullPath(args[1]);
        if (!Directory.Exists(root) || !File.Exists(Path.Combine(root, "d1-probe-owned.marker"))) return 3;
        using var log = new ProbeLog(root, args[0][2..]);
        try
        {
            // Each source/target process rechecks its own token, including after a launcher preflight.
            var environment = ProbeEnvironment.Inspect();
            log.Write("environment", environment);
            if (args[0] == "--check-environment")
            {
                using var report = new FileStream(Path.Combine(root, "environment.json"), FileMode.CreateNew, FileAccess.Write);
                JsonSerializer.Serialize(report, environment, new JsonSerializerOptions { WriteIndented = true });
                return environment.Allowed ? 0 : 4;
            }
            if (!environment.Allowed)
            {
                if (args[0] != "--smoke")
                    MessageBox.Show("D1 探针与桌面 Explorer 的运行环境不兼容或无法核实。\n" + environment.Reason +
                        "\n需同一用户、会话和完整性级别；不要关闭 UAC 或绕过 UIPI。详见本次 review 日志。", "D1 环境检查");
                return 4;
            }
            Forms.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Forms.EnableVisualStyles();
            Forms.SetCompatibleTextRenderingDefault(false);
            log.Write("start", new { environment.Kind, os = Environment.OSVersion.ToString(), architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString() });
            using Form window = args[0] switch
            {
                "--source" => new SourceForm(log),
                "--cross-click" => new CrossProcessClickProbe(log, root),
                "--cross-smoke" => new CrossProcessClickProbe(log, root, smoke:true),
                "--cross-underlay" => new CrossProcessUnderlay(log, root),
                "--routing-auto" => new RoutingMechanismProbe(log, root),
                "--routing-repeat" => new RoutingMechanismProbe(log, root, repeatOnly:true),
                "--routing-bottom" => new RoutingMechanismWorker(log, root, true),
                "--routing-source" => new RoutingMechanismWorker(log, root, false),
                _ => new TargetForm(log, root, args[0] == "--smoke")
            };
            window.Text += environment.Kind == "HighIntegrityMatched" ? " [高完整性匹配 · 仅本机探针]" : " [普通完整性匹配]";
            Forms.Run(window);
            log.Write("exit", new { code = Environment.ExitCode, nativeMouseVerified = false, filesMoved = 0 });
            return Environment.ExitCode;
        }
        catch (Exception error) { log.Write("fatal", error.ToString()); return 1; }
    }
}

internal sealed class ProbeLog : IDisposable
{
    private readonly StreamWriter writer;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    public ProbeLog(string root, string role)
    {
        writer = new StreamWriter(new FileStream(Path.Combine(root, $"{role}-{Environment.ProcessId}-{Guid.NewGuid():N}.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
    }
    public void Write(string name, object? data) => writer.WriteLine(JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, ms = clock.ElapsedMilliseconds, pid = Environment.ProcessId, name, data }));
    public void Dispose() => writer.Dispose();
}
