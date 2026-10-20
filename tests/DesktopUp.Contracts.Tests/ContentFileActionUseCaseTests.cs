using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

// In-memory ports only: no window, SQLite, file IO, native helper or user data.
public sealed class ContentFileActionUseCaseTests
{
    [Theory]
    [InlineData(ContentCreationFinish.Completed)]
    [InlineData(ContentCreationFinish.NotMoved)]
    [InlineData(ContentCreationFinish.Preserved)]
    public void CreationOrdersPreparationCompletionAndCleanupWithoutRetry(ContentCreationFinish finish)
    {
        var calls = new List<string>();
        var storage = new Creation(calls, finish);
        var useCase = new ContentCreationUseCase(storage, new Files(), null!, (phase, _) => calls.Add(phase));
        if (finish == ContentCreationFinish.Completed)
            Assert.Equal(@"C:\owned\new.txt", useCase.Create("poster", new(null, 1, 2), NewContentKind.TextFile, "new.txt"));
        else
            Assert.Throws<IOException>(() => useCase.Create("poster", new(null, 1, 2), NewContentKind.TextFile, "new.txt"));
        Assert.Equal(new[] { "prepare", "Journaled", "complete", "dispose" }, calls);
    }

    [Fact]
    public async Task PartialDeletionObservesOnceAfterReleasingExecutorAndDoesNotRetry()
    {
        var calls = new List<string>();
        var storage = new Deletion(calls);
        var shell = new DeleteShell();
        var result = await new ContentDeletionUseCase(storage, new Files(), shell).Delete("p", ["1", "2"], 0);
        Assert.Equal(1, result.Removed);
        Assert.True(result.Cancelled);
        Assert.Equal(2, shell.Calls);
        Assert.Equal(new[] { "acquire", "prepare", "authorize", "release", "observe" }, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptedShellCreationPublishesAfterViewCancellationWithoutReplaying(bool publicationFails)
    {
        var context = new Context { FailPublication = publicationFails };
        var shell = new UiShell();
        var service = new ContentFileActionService(context, new Files(), null!, null!, null!, shell);
        using var stop = new CancellationTokenSource();
        var pending = service.InvokeShell("p", null, null, new(null, 1, 2),
            @"C:\owned", ShellUiAction.DirectoryMenu, 10, 20, stop.Token);
        stop.Cancel(); // A closed view cannot erase a file already created by Windows.
        shell.Done.SetResult(new(shell.Request!.Id, true, false)
            { CreatedItem = new(@"C:\owned\new.txt", new string('A', 48), true) });
        var result = await pending;
        Assert.Equal(1, shell.Calls);
        Assert.Equal(1, context.Publications);
        Assert.Equal(!publicationFails, result.Registered);
        Assert.Equal(publicationFails, result.PublicationError is not null);
    }

    [Fact]
    public async Task CancellationBeforeShellAdmissionDoesNotDispatchOrPublish()
    {
        var context = new Context(); var shell = new UiShell();
        var service = new ContentFileActionService(context, new Files(), null!, null!, null!, shell);
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.InvokeShell("p", null, null, null,
            @"C:\owned", ShellUiAction.Properties, 0, 0, stop.Token));
        Assert.Equal(0, shell.Calls); Assert.Equal(0, context.Publications);
    }

    private sealed class Lease(Action? release = null) : IDisposable
    { public void Dispose() => release?.Invoke(); }
    private sealed class Files : IContentActionFiles
    {
        public string Normalize(string path) => path;
        public string Identity(string path) => new('A', 48);
        public bool Exists(string path) => false;
        public bool Contains(string parent, string path) => false;
        public IDisposable LockDirectory(string path) => new Lease();
        public IEnumerable<string> Enumerate(string directory) => [];
        public void RenameKnown(string source, string destination, string identity, string parentIdentity) => throw new NotSupportedException();
        public byte[] EncodeShortcut(string target) => throw new NotSupportedException();
    }
    private sealed class Creation(List<string> calls, ContentCreationFinish finish) : IContentCreationStorage, IContentCreationAttempt
    {
        public IContentCreationAttempt BeginAttempt(Action<string, string>? checkpoint) => this;
        public PreparedContentCreation Prepare(string posterId, NewContentLocation location, NewContentKind kind, string name, byte[] bytes, CancellationToken token)
        { calls.Add("prepare"); return new(Guid.NewGuid(), @"C:\stage\new.txt", @"C:\owned\new.txt"); }
        public ContentCreationFinish Complete(Guid operationId, CancellationToken token) { calls.Add("complete"); return finish; }
        public void Dispose() => calls.Add("dispose");
    }
    private sealed class Deletion(List<string> calls) : IContentDeletionStorage, IContentDeletionAttempt
    {
        public IContentDeletionAttempt BeginAttempt() => this;
        public IDisposable AcquireExecutor() { calls.Add("acquire"); return new Lease(() => calls.Add("release")); }
        public IReadOnlyList<string> Prepare(string posterId, IReadOnlyList<string> itemIds)
        { calls.Add("prepare"); return [@"C:\owned\one.txt", @"C:\owned\two.txt"]; }
        public void AuthorizeDelete() => calls.Add("authorize");
        public bool IsCurrent(string path) => true;
        public int ObserveAndClose() { calls.Add("observe"); return 1; }
    }
    private sealed class DeleteShell : IShellFileDeletion
    {
        public int Calls;
        public Task<ShellDeleteResult> Recycle(string path, nint owner, Func<bool> current, CancellationToken token)
        { Assert.True(current()); return Task.FromResult(new ShellDeleteResult(++Calls == 2, 0)); }
    }
    private sealed class Context : IContentFileActionContext
    {
        public bool FailPublication; public int Publications;
        public DesktopStateSnapshot ReadState() => throw new NotSupportedException();
        public MappedNewContent ResolveMappedLocation(string posterId) => throw new NotSupportedException();
        public void ValidateMappedAction(string posterId, string? itemId) { }
        public void RegisterShellCreated(string posterId, NewContentLocation location, string directoryIdentity, ShellCreatedItem created)
        { Publications++; if (FailPublication) throw new IOException("injected"); }
    }
    private sealed class UiShell : IShellUiClient
    {
        public int Calls; public ShellUiRequest? Request;
        public readonly TaskCompletionSource<ShellUiResult> Done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<ShellUiResult> RunAsync(ShellUiRequest request, CancellationToken stopping)
        { Calls++; Request = request; return Done.Task; }
    }
}
