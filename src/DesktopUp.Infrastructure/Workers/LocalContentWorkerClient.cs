using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

// Per-application bounded process pool. Only this instance's disposable helpers may be terminated.
public sealed class LocalContentWorkerClient(string executable, IResourceLedger? ledger = null) : ILocalContentWorker, IDisposable
{
    private readonly SemaphoreSlim slots = new(4, 4);
    private readonly CancellationTokenSource stopping = new();
    private int outstanding;
    public async ValueTask<LocalContentResponse> RunAsync(LocalContentRequest request, CancellationToken cancellationToken)
    {
        LocalContentResponse Failure(string message, bool cancelled = false, ContentOpenOutcome outcome = ContentOpenOutcome.Failed) =>
            new(request.RequestId, ContentAvailability.Unavailable, message, Cancelled: cancelled) { Outcome = outcome };
        if (!LocalContentProtocol.IsSupported(request.Item) || !Enum.IsDefined(request.Action)) return Failure("此来源尚未接入本机提供方。");
        if (stopping.IsCancellationRequested) return Failure("内容服务正在退出。", true, ContentOpenOutcome.Cancelled);
        if (Interlocked.Increment(ref outstanding) > 64)
        { Interlocked.Decrement(ref outstanding); return Failure("内容服务忙，请稍后重试。", outcome: ContentOpenOutcome.Busy); }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stopping.Token);
        bool entered = false;
        Process? process = null;
        IDisposable? lease = null;
        Task<string>? output = null, errors = null;
        try
        {
            // No unbounded waiting queue. The visible cache/open coordinator supplies backpressure.
            linked.CancelAfter(request.Action is LocalContentAction.Open or LocalContentAction.Reveal ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(5));
            await slots.WaitAsync(linked.Token).ConfigureAwait(false);
            entered = true;
            var start = new ProcessStartInfo(Path.GetFullPath(executable))
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardInputEncoding = new UTF8Encoding(false),
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(executable))!
            };
            start.ArgumentList.Add("--stdio-v1");
            process = new Process { StartInfo = start };
            if (!process.Start()) return Failure("无法启动内容辅助进程。");
            lease = ledger?.Acquire("content.worker:" + process.Id, request.Action.ToString());
            output = ReadBounded(process.StandardOutput, linked.Token);
            errors = ReadBounded(process.StandardError, linked.Token);
            string body = JsonSerializer.Serialize(request);
            if (body.Length > LocalContentProtocol.MaximumRequestCharacters) return Failure("内容请求超出限制。");
            await process.StandardInput.WriteAsync(body.AsMemory(), linked.Token).ConfigureAwait(false);
            process.StandardInput.Close();
            string json = await output.ConfigureAwait(false);
            await errors.ConfigureAwait(false);
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            if (process.ExitCode != 0) return Failure("内容辅助进程失败；未自动重试。");
            var result = JsonSerializer.Deserialize<LocalContentResponse>(json);
            return result is not null && LocalContentProtocol.Validate(request, result) ? result : Failure("内容提供方返回无效结果。");
        }
        catch (OperationCanceledException)
        {
            // Cancellation can race the helper's final exit after its complete handoff receipt
            // has arrived. Preserve that known fact; do not turn proven success into unknown.
            if (request.Action is LocalContentAction.Open or LocalContentAction.Reveal && output?.IsCompletedSuccessfully == true)
            {
                try
                {
                    var accepted = JsonSerializer.Deserialize<LocalContentResponse>(output.Result);
                    if (accepted is not null && LocalContentProtocol.Validate(request, accepted) && accepted.Evidence != ContentOpenEvidence.None)
                        return accepted;
                }
                catch (JsonException) { }
            }
            return Failure(cancellationToken.IsCancellationRequested || stopping.IsCancellationRequested
                ? "操作已取消；已交给外部程序的操作不会被撤销。" : "等待超时，是否已交接未知；未自动重试。", cancelled: true,
                outcome: cancellationToken.IsCancellationRequested || stopping.IsCancellationRequested ? ContentOpenOutcome.Cancelled : ContentOpenOutcome.TimedOut);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or JsonException or ArgumentException)
        { return Failure("内容服务暂不可用；原文件和关系保留。"); }
        finally
        {
            linked.Cancel();
            Exception? terminationFailure = null;
            if (process is not null)
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: false);
                    using var termination = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await process.WaitForExitAsync(termination.Token).ConfigureAwait(false);
                }
                catch (InvalidOperationException) { }
                catch (Exception error) when (error is System.ComponentModel.Win32Exception or OperationCanceledException)
                { terminationFailure = error; }
                process.Dispose();
            }
            // Observe both pipes even when one failed, timed out or exceeded its budget.
            foreach (var read in new[] { output, errors })
                if (read is not null) { try { await read.ConfigureAwait(false); } catch (Exception) { } }
            // A failed termination stays visible in the resource ledger, but cannot strand the
            // host's exit await forever. Never claim this helper was released in that case.
            if (terminationFailure is null) lease?.Dispose();
            if (entered) slots.Release(); Interlocked.Decrement(ref outstanding);
            if (terminationFailure is not null) throw new IOException("Owned content helper did not terminate.", terminationFailure);
        }
    }
    private static async Task<string> ReadBounded(StreamReader reader, CancellationToken cancellation)
    {
        var text = new StringBuilder(); var buffer = new char[4096];
        while (true)
        {
            int count = await reader.ReadAsync(buffer, cancellation).ConfigureAwait(false);
            if (count == 0) return text.ToString();
            if (text.Length + count > LocalContentProtocol.MaximumResponseCharacters) throw new IOException("Worker output budget exceeded.");
            text.Append(buffer, 0, count);
        }
    }
    public void Dispose() => stopping.Cancel();
    public async Task StopAsync()
    {
        stopping.Cancel();
        while (Volatile.Read(ref outstanding) != 0) await Task.Delay(20).ConfigureAwait(false);
    }
}
