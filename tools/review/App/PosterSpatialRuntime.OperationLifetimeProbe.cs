using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    // Only compiled into Review. Uses real creation services with a failing projection
    // port; no test hook or synthetic failure exists in the normal runtime.
    internal async Task VerifyOperationLifetimeForProbe(string posterId)
    {
        if (!state.TryGetCurrent(out var before) || before is null) throw new IOException("No current projection.");
        var card = before.ContentCatalog.PosterItems.First(p => p.PosterId == posterId);
        var source = _windows[posterId];
        var intent = BeginNativeExit(source, [card.LayoutItemId]) ?? throw new IOException("Native cancellation admission failed.");
        var nativeCompletion = _contentOperations.FileOperationCompletion;
        CompleteNativeExit(source, intent, null);
        CompleteNativeExit(source, intent, null); // Duplicate completion must not end another operation.
        await nativeCompletion.WaitAsync(TimeSpan.FromSeconds(10));
        if (!TransfersAvailable || ContentActionRelation(posterId, card.LayoutItemId) is null)
            throw new IOException("Cancelled native exit retained busy state or removed its card.");

        var projection = new FailingRefresh(state);
        var runtime = new PosterSpatialRuntime(projection, commands, log, animationOptions);
        runtime.ConfigureTransfers(Task.CompletedTask, _chooseTransferStorage!, () => false, _ => { });
        runtime.ConfigureFileActions(FileActions);
        runtime.Resume();
        try
        {
            var location = await runtime.PrepareNewContent(posterId) ?? throw new IOException("No owned creation location.");
            string name = "r6-publication-failure.txt";
            projection.FailNext = true;
            bool failed = false;
            try { await runtime.CreateContent(posterId, location, NewContentKind.TextFile, name, null, CancellationToken.None); }
            catch (IOException error) when (error.Message == "R6 injected refresh failure") { failed = true; }
            if (!failed || !runtime.TransfersAvailable || !File.Exists(Path.Combine(location.Directory, name)))
                throw new IOException("Publication failure retained busy state or lost the committed file.");
            state.Refresh();
            if (runtime.ContentItemAtPath(posterId, Path.Combine(location.Directory, name)) is null)
                throw new IOException("Committed creation was not recoverable by a read-only refresh.");

            // Admission notifications can fail before IO. There must be no file and no
            // active scope; removing the bad subscriber permits a fresh user operation.
            location = await runtime.PrepareNewContent(posterId) ?? throw new IOException("No retry location.");
            void FailActivity() => throw new IOException("R6 injected activity failure");
            runtime.ActivityChanged += FailActivity;
            try
            {
                try { await runtime.CreateContent(posterId, location, NewContentKind.TextFile, "r6-not-created.txt", null, CancellationToken.None); }
                catch (IOException error) when (error.Message == "R6 injected activity failure") { }
            }
            finally { runtime.ActivityChanged -= FailActivity; }
            if (!runtime.TransfersAvailable || File.Exists(Path.Combine(location.Directory, "r6-not-created.txt")))
                throw new IOException("Failed admission performed IO or retained its scope.");

            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            try { await runtime.CreateContent(posterId, location, NewContentKind.TextFile, "r6-cancelled.txt", null, cancelled.Token); }
            catch (OperationCanceledException) { }
            if (!runtime.TransfersAvailable || File.Exists(Path.Combine(location.Directory, "r6-cancelled.txt")))
                throw new IOException("Cancelled creation retained its scope or created a file.");

            location = await runtime.PrepareNewContent(posterId) ?? throw new IOException("No retry location.");
            await runtime.CreateContent(posterId, location, NewContentKind.TextFile, "r6-fresh-operation.txt", null, CancellationToken.None);
            if (!runtime.TransfersAvailable || !File.Exists(Path.Combine(location.Directory, "r6-fresh-operation.txt")))
                throw new IOException("Fresh creation did not recover after failure.");
            log.Write("info", "contract.r6_operation_lifetime_passed",
                new { publicationFailure = true, admissionFailure = true, cancellation = true, nativeCancellation = true,
                    duplicateCompletion = true, replay = false, nativeMouse = false });
        }
        finally
        {
            await runtime.StopTransfersAsync();
            await runtime.WaitForContentOperationsAsync();
        }
    }

    private sealed class FailingRefresh(IStateProjectionCoordinator inner) : IStateProjectionCoordinator
    {
        public bool FailNext;
        public event EventHandler<StateProjectionChangedEventArgs>? ProjectionChanged
        { add => inner.ProjectionChanged += value; remove => inner.ProjectionChanged -= value; }
        public DesktopUpProjection EnsureInitialized(PosterDefinition? defaultPoster = null) => inner.EnsureInitialized(defaultPoster);
        public bool TryGetCurrent(out DesktopUpProjection? projection) => inner.TryGetCurrent(out projection);
        public PosterRemovalPreview? PrepareRemoval(string id) => inner.PrepareRemoval(id);
        public DesktopUpProjection Refresh()
        {
            if (FailNext) { FailNext = false; throw new IOException("R6 injected refresh failure"); }
            return inner.Refresh();
        }
    }
}
