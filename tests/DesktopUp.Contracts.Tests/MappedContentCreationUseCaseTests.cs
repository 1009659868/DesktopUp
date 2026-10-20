using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

// No SQLite, filesystem or window: ordering and ambiguous acknowledgement policy live here.
public sealed class MappedContentCreationUseCaseTests
{
    [Theory]
    [InlineData("success", false)]
    [InlineData("cancel", true)]
    [InlineData("changed", true)]
    [InlineData("commit-failed", true)]
    [InlineData("acknowledgement-lost", false)]
    [InlineData("read-unavailable", false)]
    public void CreatesOnceAndOnlyCleansAProvenUnindexedResult(string fault, bool cleanupRequested)
    {
        var ports = new Ports(fault);
        using var stop = new CancellationTokenSource();
        var useCase = new MappedContentCreationUseCase(ports, ports, ports,
            () => { if (fault == "cancel") stop.Cancel(); });
        if (fault == "success")
            Assert.Equal(Ports.Path, useCase.Create(ports.PosterId, ports.Location, NewContentKind.TextFile, "new.txt", [], stop.Token));
        else
            Assert.ThrowsAny<Exception>(() => useCase.Create(ports.PosterId, ports.Location, NewContentKind.TextFile, "new.txt", [], stop.Token));

        Assert.Equal(1, ports.Creates);
        Assert.Equal(cleanupRequested, ports.CleanupRequested);
        Assert.Equal(0, ports.Leases);
        Assert.Equal(fault is "success" or "acknowledgement-lost", ports.Indexed);
        Assert.Equal(fault is "cancel" or "changed" ? 0 : 1, ports.Commits);
        if (fault == "success")
            Assert.Equal(new[] { "create", "scan", "pin", "scan", "commit", "unpin" }, ports.Order);
    }

    private sealed class Lease(Action release) : IDisposable
    {
        public void Dispose() => release();
    }

    private sealed class Ports : IMappedDirectoryStorage, IContentObservationFiles,
        IOwnedContentCreationFiles, IOwnedContentCreation
    {
        public MappedObservationRead ReadObservation(CancellationToken token = default, IReadOnlySet<string>? directories = null) => throw new NotSupportedException();
        public const string Root = @"C:\r5-mapping";
        public const string Path = Root + @"\new.txt";
        private readonly string _fault;
        private DesktopStateSnapshot _state;
        private readonly MappedDirectoryScan _scan;
        private bool _readUnavailable;
        public string PosterId { get; }
        public NewContentLocation Location { get; }
        public int Creates, Commits, Leases;
        public bool CleanupRequested;
        public bool Indexed => _state.ContentCatalog.PosterItems.Count == 1;
        public List<string> Order { get; } = [];

        public Ports(string fault)
        {
            _fault = fault;
            var collection = new Collection(Guid.NewGuid(), "mapped", ContentCollectionKind.Mapped, Root, ContentStorageOwnership.Borrowed);
            var poster = PosterConfigurationFactory.NewBasicBox("mapped", "DISPLAY1") with { CollectionId = collection.Id.ToString() };
            PosterId = poster.PosterId;
            _state = new(new(1, 1, true, [new(1, 1, poster, DateTimeOffset.UtcNow)]),
                new(1, 1, new(false, DesktopUpPerformanceMode.Automatic, false), DateTimeOffset.UtcNow))
            {
                ContentVersion = 1,
                ContentCatalog = ContentCatalogSnapshot.Empty with
                { Collections = [collection], Bindings = [new(PosterId, collection.Id)] }
            };
            Location = new(null, 1, 1) { Mapping = new(collection.Id, Root, "root-id") };
            var item = new ContentItem(Guid.NewGuid(), ContentKind.File, new("file", Path),
                new(ContentTargetKind.FileSystem, Path), "new.txt", ContentCapability.Reference | ContentCapability.Open);
            _scan = new(Root, "root-id", [new(item, "file-id", "unchanged")]);
        }

        private IDisposable Hold()
        {
            Leases++;
            return new Lease(() => Leases--);
        }
        public IDisposable AcquireCreation() => Hold();
        public void RequireIdle() { }
        public DesktopStateSnapshot ReadState() => _readUnavailable ? throw new IOException("read unavailable") : _state;
        public void ValidateRoot(DesktopStateSnapshot state, string path, Guid? own = null) { }
        public MappedDirectoryEvidence Evidence(Guid collection) => new("root-id", []);
        public StateTransactionResult Commit(MappedObservationCommit commit)
        {
            Commits++;
            Order.Add("commit");
            if (_fault == "read-unavailable") _readUnavailable = true;
            if (_fault is "commit-failed" or "read-unavailable") throw new IOException("commit failed");
            _state = _state with { ContentCatalog = commit.Mutation.ContentCatalog! };
            if (_fault == "acknowledgement-lost") throw new IOException("acknowledgement lost");
            return new(StateCommandStatus.Committed, _state, 2, null, [], null);
        }
        public string Identity(string path) => "root-id";
        public IDisposable LockDirectory(string path) => Hold();
        public MappedDirectoryScan ScanMapping(string path, CancellationToken token = default)
        { Order.Add("scan"); return _scan; }
        public IOwnedContentCreation Create(string path, NewContentKind kind, byte[] bytes)
        { Creates++; Order.Add("create"); return this; }
        public IDisposable PinUnchanged()
        {
            Order.Add("pin");
            if (_fault == "changed") throw new IOException("changed");
            var held = Hold();
            return new Lease(() => { Order.Add("unpin"); held.Dispose(); });
        }
        public void RemoveIfUnchanged() => CleanupRequested = true;
        public IDisposable AcquireObservation() => throw new NotSupportedException();
        public StateTransactionResult Create(CreatePosterCommand command, MappedDirectoryScan scan) => throw new NotSupportedException();
        public string Normalize(string path) => path;
        public string MappedItemIdentity(string path) => throw new NotSupportedException();
        public IEnumerable<string> Enumerate(string directory) => throw new NotSupportedException();
        public bool Contains(string parent, string child) => throw new NotSupportedException();
        public IReadOnlyDictionary<string, string> Inventory(string path, CancellationToken token = default) => throw new NotSupportedException();
    }
}
