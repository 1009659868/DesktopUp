using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

// Headless orchestration checks. No SQLite, Shell, windows or physical files.
public sealed class ContentTransferUseCaseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManagedIngressChecksLateAdmissionAndPublishesCompletedFactsAfterCancellation(bool commit)
    {
        using var stop = new CancellationTokenSource();
        var calls = new List<string>(); var view = new IncomingView(calls, stop);
        var storage = new ManagedStorage(calls, () => { if (!commit) view.Current = false; },
            () => { if (commit) stop.Cancel(); });
        var result = await new ManagedIngressUseCase(storage, new Files()).Execute("p",
            [@"C:\owned\one.txt", @"C:\owned\two.txt"], null!, null, view, stop.Token);
        Assert.Equal(commit ? 1 : 0, result.Completed);
        Assert.Equal(0, result.Failed);
        Assert.Equal(commit
            ? new[] { "progress", "read", "prepare", "intent", "complete", "publish" }
            : new[] { "progress", "read", "prepare" }, calls);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task NonExternalCompletionNeverObservesOrPrompts(bool released, bool origin, bool handedOff)
    {
        var calls = new List<string>(); var store = new ExitStore(calls);
        await new NativeShellExitUseCase(store).Complete(Intent(), new(released, origin, 2, handedOff),
            new ExitView(calls), CancellationToken.None);
        Assert.Equal(new[] { "end:False" }, calls);
    }

    [Fact]
    public async Task NativeExitObservesEvenNoneEffectAndConfirmsOnlyOriginalMissingCards()
    {
        var calls = new List<string>(); var store = new ExitStore(calls); var view = new ExitView(calls);
        var intent = Intent();
        await new NativeShellExitUseCase(store).Complete(intent, new(true, false, 0, false), view, CancellationToken.None);
        Assert.Equal(new[] { intent.Items[1].Path }, view.Missing);
        Assert.Equal(new[] { "end:True", "observe", "publish", "remaining", "confirm", "remove-stale", "reconcile" }, calls);
    }

    [Fact]
    public async Task MappedPartialCancellationRecordsResultThenReleasesBeforeObservation()
    {
        var calls = new List<string>(); var storage = new Mapping(calls); var shell = new Shell(calls);
        var result = await new MappedIngressUseCase(storage, shell).Execute("p", [@"C:\owned\one.txt"], false,
            null, 0, () => true);
        Assert.Equal(1, result.Completed); Assert.Empty(result.Errors);
        Assert.Equal(new[] { "acquire", "prepare", "shell", "record", "release", "observe", "publish" }, calls);
    }

    private static ShellExitIntent Intent() => new(Guid.NewGuid(), "p", DateTimeOffset.UtcNow,
        [new(Guid.NewGuid(), Guid.NewGuid(), 1, @"C:\owned\one.txt", "parent", "file", false),
         new(Guid.NewGuid(), Guid.NewGuid(), 1, @"C:\owned\missing.txt", "parent", null, true)]);
    private static DesktopStateSnapshot Empty => new(new(0, 0, true, []),
        new(0, 0, new(false, default, false), DateTimeOffset.UnixEpoch));
    private sealed class Lease(Action? release = null) : IDisposable { public void Dispose() => release?.Invoke(); }
    private sealed class IncomingView(List<string> calls, CancellationTokenSource stop) : IIncomingTransferPresentation
    {
        public bool Current = true;
        public bool IsCurrent => Current && !stop.IsCancellationRequested;
        public void ReportProgress(string path, int completed) => calls.Add("progress");
        public void PublishCommittedChanges() => calls.Add("publish");
    }
    private sealed class ManagedStorage(List<string> calls, Action prepared, Action completed) : IManagedIngressStorage, IManagedIngressItem
    {
        public DesktopStateSnapshot ReadState() { calls.Add("read"); return Empty; }
        public IManagedIngressItem Prepare(DesktopStateSnapshot state, string posterId, string path,
            RegisteredManagedStorage storage, IncomingContentPlacement? landing)
        { calls.Add("prepare"); prepared(); return this; }
        public Guid ContentId => Guid.Empty;
        public string Destination => @"C:\target\one.txt";
        public void RecordIntent() => calls.Add("intent");
        public TransferFinishKind Complete(CancellationToken token)
        { calls.Add("complete"); completed(); return TransferFinishKind.Completed; }
    }
    private sealed class Files : IContentActionFiles
    {
        public string Normalize(string path) => path;
        public string Identity(string path) => "id";
        public bool Exists(string path) => false;
        public bool Contains(string parent, string path) => false;
        public IDisposable LockDirectory(string path) => new Lease();
        public IEnumerable<string> Enumerate(string directory) => [];
        public void RenameKnown(string source, string destination, string identity, string parentIdentity) => throw new NotSupportedException();
        public byte[] EncodeShortcut(string target) => throw new NotSupportedException();
    }
    private sealed class ExitStore(List<string> calls) : INativeShellExitStore
    {
        public ShellExitIntent Begin(string posterId, IReadOnlyList<string> layoutIds, bool explicitDelete = false) => Intent();
        public void End(ShellExitIntent intent, bool released) => calls.Add("end:" + released);
        public int ObserveReleasedMove(ShellExitIntent intent) { calls.Add("observe"); return 1; }
        public bool HasRemainingPresentCards(ShellExitIntent intent) { calls.Add("remaining"); return false; }
        public int RemoveStaleReferencesAfterUserConfirmation(ShellExitIntent intent) { calls.Add("remove-stale"); return 1; }
        public int Reconcile() { calls.Add("reconcile"); return 0; }
    }
    private sealed class ExitView(List<string> calls) : INativeExitPresentation
    {
        public IReadOnlyList<string>? Missing;
        public void PublishObservedChanges() => calls.Add("publish");
        public Task<bool> ConfirmMissingReferences(IReadOnlyList<string> paths, CancellationToken token)
        { Missing = paths; calls.Add("confirm"); return Task.FromResult(true); }
    }
    private sealed class Shell(List<string> calls) : IShellDirectoryTransfer
    {
        public Task<ShellDirectoryResult> Run(string source, string destination, bool copy, nint owner, Func<bool> current, CancellationToken token)
        { Assert.True(current()); calls.Add("shell"); return Task.FromResult(new ShellDirectoryResult(@"C:\target\one.txt", true, 0)); }
    }
    private sealed class Mapping(List<string> calls) : IMappedIngressStorage, IMappedIngressAttempt
    {
        public IMappedIngressAttempt BeginAttempt() => this;
        public IDisposable AcquireExecution() { calls.Add("acquire"); return new Lease(() => calls.Add("release")); }
        public MappedIngressPreparation Prepare(string posterId, IReadOnlyList<string> paths, bool copy, Func<bool> current,
            CancellationToken token, long? expectedPosterVersion)
        { calls.Add("prepare"); return new(new(Guid.NewGuid(), @"C:\target", "identity"), [new(paths[0], "file")], 0); }
        public bool IsCurrent(MappedIngressSource source) => true;
        public bool AcceptDestination(string path) => true;
        public void RecordCompleted(IReadOnlyList<string> paths) { Assert.Single(paths); calls.Add("record"); }
        public void ObserveDepartures(ICollection<string> errors) => calls.Add("observe");
        public bool HasPreparedOperation => true;
        public void PublishAndClose(IReadOnlyList<string> completed, IncomingContentPlacement? landing, ICollection<string> errors)
        { Assert.Single(completed); calls.Add("publish"); }
    }
}
