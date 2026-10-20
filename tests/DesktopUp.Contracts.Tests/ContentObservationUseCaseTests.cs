using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

// New boundaries only. Existing mapping/sync integration tests exercise the same use cases.
public sealed class ContentObservationUseCaseTests
{
    [Fact]
    public void StartupKeepsSettlementOrderAndReportsCountsAndNotices()
    {
        var calls = new List<string>();
        var result = new ContentStartupUseCase(new Startup(calls), new Log(calls)).Run();
        Assert.Equal(new[] { "storage", "transfers", "owner", "rename", "return",
            "content.return_preserved", "cross", "transfer.cross_poster_preserved", "mapped", "exit" }, calls);
        Assert.Equal(2, result.Pending); Assert.Equal(1, result.Preserved); Assert.Equal(3, result.Relocations);
        Assert.Equal(new[] { "kept" }, result.ReturnNotices);
    }

    [Fact]
    public void StartupFailureDoesNotRunLaterStepsOrLoseAlreadyLoggedNotices()
    {
        var calls = new List<string>();
        Assert.Throws<IOException>(() => new ContentStartupUseCase(new Startup(calls, true), new Log(calls)).Run());
        Assert.Equal(new[] { "storage", "transfers", "owner", "rename", "return", "content.return_preserved", "cross" }, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MappingChecksCancellationAfterScanAndAlwaysReleasesLeases(bool cancel)
    {
        var calls = new List<string>();
        using var stop = new CancellationTokenSource();
        var useCase = new MappedDirectoryUseCase(new Mapping(calls), new Files(calls, () => { if (cancel) stop.Cancel(); }));
        if (cancel) Assert.Throws<OperationCanceledException>(() => useCase.Create(null!, "root", stop.Token));
        else useCase.Create(null!, "root", stop.Token);
        Assert.Equal(cancel
            ? new[] { "executor", "idle", "read", "normalize", "validate", "root-lock", "scan", "root-release", "executor-release" }
            : new[] { "executor", "idle", "read", "normalize", "validate", "root-lock", "scan", "commit", "root-release", "executor-release" }, calls);
    }

    private sealed class Startup(List<string> calls, bool fail = false) : IContentStartupSteps
    {
        public int SettleStorageLocation() { calls.Add("storage"); return 3; }
        public StartupTransferSettlement SettleTransfers() { calls.Add("transfers"); return new(2, 1); }
        public void EnsureSingleOwnership() => calls.Add("owner");
        public void ReconcileRenames() => calls.Add("rename");
        public IReadOnlyList<string> ReconcileReturns() { calls.Add("return"); return ["kept"]; }
        public IReadOnlyList<string> ReconcileCrossPoster()
        { calls.Add("cross"); if (fail) throw new IOException("owned failure"); return ["cross kept"]; }
        public void SettleMappedIngress() => calls.Add("mapped");
        public void ReconcileReleasedExits() => calls.Add("exit");
    }
    private sealed class Log(List<string> calls) : IStructuredLog
    { public void Write(string level, string eventName, object? data = null) => calls.Add(eventName); }
    private sealed class Lease(Action release) : IDisposable
    { public void Dispose() => release(); }
    private sealed class Mapping(List<string> calls) : IMappedDirectoryStorage
    {
        public IDisposable AcquireCreation() { calls.Add("executor"); return new Lease(() => calls.Add("executor-release")); }
        public void RequireIdle() => calls.Add("idle");
        public DesktopStateSnapshot ReadState() { calls.Add("read"); return null!; }
        public MappedObservationRead ReadObservation(CancellationToken token = default, IReadOnlySet<string>? directories = null) => throw new NotSupportedException();
        public void ValidateRoot(DesktopStateSnapshot state, string path, Guid? own = null) => calls.Add("validate");
        public StateTransactionResult Create(CreatePosterCommand command, MappedDirectoryScan scan)
        { calls.Add("commit"); return null!; }
        public IDisposable AcquireObservation() => throw new NotSupportedException();
        public MappedDirectoryEvidence Evidence(Guid collection) => throw new NotSupportedException();
        public StateTransactionResult Commit(MappedObservationCommit commit) => throw new NotSupportedException();
    }
    private sealed class Files(List<string> calls, Action scanned) : IContentObservationFiles
    {
        public string Identity(string path) => throw new NotSupportedException();
        public string MappedItemIdentity(string path) => throw new NotSupportedException();
        public IEnumerable<string> Enumerate(string directory) => throw new NotSupportedException();
        public string Normalize(string path) { calls.Add("normalize"); return path; }
        public IDisposable LockDirectory(string path) { calls.Add("root-lock"); return new Lease(() => calls.Add("root-release")); }
        public MappedDirectoryScan ScanMapping(string path, CancellationToken token = default)
        { calls.Add("scan"); scanned(); return new(path, "root-id", []); }
        public bool Contains(string parent, string child) => throw new NotSupportedException();
        public IReadOnlyDictionary<string, string> Inventory(string path, CancellationToken token = default) => throw new NotSupportedException();
    }
}
