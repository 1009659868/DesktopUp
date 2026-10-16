namespace DesktopUp.Application;

// Shell alone moves/copies files. Persist each observed result and synchronize after
// releasing the executor; cancellation does not undo or replay completed Shell work.
public sealed class MappedIngressUseCase(IMappedIngressStorage storage, IShellDirectoryTransfer shell,
    Action<string>? checkpoint = null)
{
    public async Task<MappedIngressResult> Execute(string posterId, IReadOnlyList<string> paths, bool copy,
        IncomingContentPlacement? landing, nint owner, Func<bool> current,
        CancellationToken token = default, long? expectedPosterVersion = null)
    {
        var attempt = storage.BeginAttempt();
        var completed = new List<string>(); var errors = new List<string>(); int skipped = 0;
        try
        {
            using (attempt.AcquireExecution())
            {
                var prepared = attempt.Prepare(posterId, paths, copy, current, token, expectedPosterVersion);
                skipped = prepared.Skipped;
                checkpoint?.Invoke("Prepared");
                foreach (var source in prepared.Sources)
                {
                    token.ThrowIfCancellationRequested();
                    bool Valid() => !token.IsCancellationRequested && current() && attempt.IsCurrent(source);
                    if (!Valid()) { errors.Add("来源或海报状态改变，剩余项未执行。"); break; }
                    try
                    {
                        var result = await shell.Run(source.Path, prepared.Target.Path, copy, owner, Valid, token).ConfigureAwait(false);
                        if (result.Destination is { } path && attempt.AcceptDestination(path))
                        {
                            completed.Add(path);
                            attempt.RecordCompleted(completed);
                        }
                        else
                        {
                            skipped++;
                            if (result.HResult < 0 && !result.Cancelled) errors.Add(Path.GetFileName(source.Path) + "：Windows未完成该项。");
                        }
                        if (result.Cancelled) break;
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
                    { errors.Add(Path.GetFileName(source.Path) + "：" + e.Message); }
                }
            }
        }
        finally
        {
            attempt.ObserveDepartures(errors);
            if (attempt.HasPreparedOperation)
                attempt.PublishAndClose(completed, current() ? landing : null, errors);
        }
        return new(completed.Count, skipped, errors);
    }
}
