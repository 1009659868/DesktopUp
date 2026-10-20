using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

public sealed class ShellUiClient(string executable, IResourceLedger ledger) : IShellUiClient
{
    // Independent interactive lifetime: a property dialog must not consume icon extraction slots.
    public async Task<ShellUiResult> RunAsync(ShellUiRequest request, CancellationToken stopping)
    {
        if (!ShellUiProtocol.Valid(request)) return new(request.Id, false, false, "不支持此Shell目标。");
        var start = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true,RedirectStandardError=true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(executable))! };
        start.ArgumentList.Add("--shell-ui-v1");
        using var process = new Process { StartInfo = start };
        IDisposable? lease = null;
        bool started = false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        try
        {
            stopping.ThrowIfCancellationRequested();
            if (!process.Start()) return new(request.Id, false, false, "无法启动系统菜单辅助进程。");
            started = true;
            lease = ledger.Acquire("shell.ui:" + process.Id, request.Action.ToString());
            AllowSetForegroundWindow((uint)process.Id);
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(request));
            process.StandardInput.Close();
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            if (await ReadLine(process.StandardOutput, timeout.Token) != "ready")
            {
                string? diagnostic=await ReadLine(process.StandardError,timeout.Token);
                return new(request.Id,false,false,"Windows未能准备此菜单或属性。"+
                    (string.IsNullOrWhiteSpace(diagnostic)?"": " "+diagnostic[..Math.Min(700,diagnostic.Length)]));
            }
            timeout.CancelAfter(TimeSpan.FromMinutes(10));
            string? json = await ReadLine(process.StandardOutput, timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var result = json is null ? null : JsonSerializer.Deserialize<ShellUiResult>(json);
            if (process.ExitCode == 0 && result is not null && ShellUiProtocol.Valid(request, result)) return result;
            string? detail = await ReadLine(process.StandardError, timeout.Token);
            return new(request.Id, false, false, "系统操作结果未知；请核对文件，不会自动重试。" +
                (string.IsNullOrWhiteSpace(detail) ? "" : "\n" + detail[..Math.Min(700,detail.Length)]));
        }
        catch (OperationCanceledException)
        { return new(request.Id, false, true, "系统界面已结束；已执行的文件操作不会被自动撤销。"); }
        catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or JsonException)
        { return new(request.Id, false, false, "系统菜单辅助进程失败；文件结果以实际状态为准。"); }
        finally
        {
            // Only this invocation's helper is owned here. Never kill Explorer, child applications,
            // or replay/roll back a third-party command whose outcome is unknown.
            if (started)
            {
                bool released = false;
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: false);
                    using var termination = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await process.WaitForExitAsync(termination.Token).ConfigureAwait(false);
                    released = true;
                }
                catch (InvalidOperationException) { released = true; }
                catch (Exception e) when (e is System.ComponentModel.Win32Exception or OperationCanceledException) { }
                if (released) lease?.Dispose(); // A stuck helper stays visible in the resource ledger.
            }
        }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);
    private static async Task<string?> ReadLine(StreamReader reader, CancellationToken token)
    {
        var text = new StringBuilder(); var character = new char[1];
        while (await reader.ReadAsync(character.AsMemory(), token) != 0)
        {
            if (character[0] == '\n') return text.ToString().TrimEnd('\r');
            if (text.Length >= ShellUiProtocol.MaximumCharacters) throw new IOException("Shell result limit exceeded.");
            text.Append(character[0]);
        }
        return text.Length == 0 ? null : text.ToString();
    }
}
