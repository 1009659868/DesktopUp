using DesktopUp.Domain;

namespace DesktopUp.Application;

// Owns the existing handoff sequence; storage performs pinned checks and atomic writes.
public sealed class CrossPosterTransferUseCase(ICrossPosterStorage storagePort, IShellDirectoryTransfer shell,
    Action<string>? checkpoint = null)
{
    public async Task<MappedIngressResult> Execute(ShellExitIntent source, string targetId, IReadOnlyList<string> paths,
        RegisteredManagedStorage? storage, IncomingContentPlacement? landing, nint owner, Func<bool> current,
        CancellationToken token = default, long? expectedTargetVersion = null)
    {
        // A prior acknowledgement failure must not force a separate recovery UI.
        // Only recorded results are reconciled; this never repeats a file operation.
        var priorErrors = storagePort.Reconcile();
        if (priorErrors.Count > 0) throw new IOException(string.Join("\n", priorErrors));
        using var attempt = storagePort.BeginAttempt();
        var prepared = attempt.Prepare(source, targetId, paths, storage, landing, current, token, expectedTargetVersion);
        var sources = prepared.Sources; var target = prepared.Target; long version = prepared.Version;
        checkpoint?.Invoke("Prepared");
        var results = new List<CrossPosterResult>(); var errors = new List<string>(); int completed = 0, skipped = 0;
        bool indexFailed = false;
        try
        {
            foreach (var item in sources)
            {
                if (token.IsCancellationRequested || !current()) break;
                try
                {
                    bool Valid() => !token.IsCancellationRequested && current() && attempt.IsCurrent(item, version);
                    if (!Valid()) throw new IOException("来源或目标状态已变化，未移动此项。");
                    // Ordinary posters share one managed collection. Never copy/move a file onto itself.
                    bool same = string.Equals(Path.GetDirectoryName(item.Path), target.Path, StringComparison.OrdinalIgnoreCase);
                    ShellDirectoryResult result = same ? new(item.Path, false, 0) :
                        await shell.Run(item.Path, target.Path, false, owner, Valid, token).ConfigureAwait(false);
                    if (result.Destination is not { } destination)
                    {
                        skipped++;
                        if (attempt.IsStillMissing(item))
                            errors.Add($"Windows未返回可核实的位置；文件未由本程序再次处理。请核对来源 {item.Path} 和目标目录 {target.Path}。");
                        else if (result.HResult < 0 && !result.Cancelled) errors.Add(Path.GetFileName(item.Path) + "：Windows未完成该项。");
                        if (result.Cancelled) break;
                        continue;
                    }
                    var receipt = attempt.CaptureDestination(item, destination);
                    indexFailed = true;
                    results.Add(receipt); attempt.RecordResults(results); checkpoint?.Invoke("FileRecorded");
                    try { attempt.Commit(receipt, landing, version); }
                    catch { indexFailed = true; throw; }
                    indexFailed = false;
                    completed++; checkpoint?.Invoke("IndexCommitted");
                    var after = attempt.ReadState();
                    version = after.Catalog.Posters.Single(p => p.Poster.PosterId == targetId).EntityVersion;
                    if (landing is not null)
                    {
                        var card = after.ContentCatalog.PosterItems.Single(p => p.PosterId == targetId &&
                            after.ContentCatalog.Memberships.Any(m => m.Id == p.MembershipId && m.ContentItemId == item.ContentId));
                        ContentLayoutPolicy.TryRead(after.Catalog.Posters.Single(p => p.Poster.PosterId == targetId).Poster.ContentLayoutJson, targetId, out var layout, out _);
                        landing = landing.Advance(layout!, card.LayoutItemId);
                    }
                    if (result.Cancelled) break;
                }
                catch (OperationCanceledException) { break; }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException)
                { errors.Add(Path.GetFileName(item.Path) + "：" + e.Message); if (indexFailed) break; }
            }
        }
        finally
        {
            // A completed physical result remains durable until its index can be observed.
            // Ordinary cancel/failure has no pending user workflow and can be dragged again.
            if (!indexFailed) attempt.Close();
        }
        return new(completed, sources.Count - completed, errors);
    }

}
