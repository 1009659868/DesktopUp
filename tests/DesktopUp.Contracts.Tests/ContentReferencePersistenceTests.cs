using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentReferencePersistenceTests
{
    [Fact] public void FreshSchemaIsEmptyAndDoesNotConvertDemo()
    {
        using var f = new Fixture();
        Assert.Equal(18L, f.Sql("PRAGMA user_version"));
        Assert.Empty(f.Current.ContentCatalog.Items); Assert.Equal(0, f.Current.ContentVersion);
        Assert.Null(f.Current.PostersById[f.Main].Poster.ContentLayoutJson);
        Assert.Equal(0L, f.Sql("SELECT count(*) FROM content_demo_archive"));
    }

    [Fact] public void AddIsOneRevisionAndRestartKeepsAllRelations()
    {
        using var f = new Fixture(); var original = f.Current;
        var command = f.AddCommand(f.Main, Entry("a.txt"), Entry("b.lnk"));
        var events = new List<StateProjectionChangedEventArgs>();
        f.Coordinator.ProjectionChanged += (_, e) => events.Add(e);
        var result = f.Coordinator.Execute(command);
        Assert.True(result.Succeeded, result.Error); Assert.Equal(original.Revision + 1, result.CommittedRevision);
        Assert.Single(events); Assert.Equal(1, f.Current.ContentVersion);
        Assert.Equal(2, f.Current.ContentCatalog.Items.Count); Assert.Equal(2, f.Current.ContentCatalog.PosterItems.Count);
        Assert.Equal(1L, f.Sql("SELECT count(*) FROM content_demo_archive"));
        Assert.Equal(DBNull.Value, f.Sql("SELECT original_layout_json FROM content_demo_archive"));
        var read = new SqliteStateCommandRepository(f.Store).Read();
        Assert.Equal(ContentCatalogCodec.Write(f.Current.ContentCatalog), ContentCatalogCodec.Write(read.ContentCatalog));
        Assert.True(ContentLayoutPolicy.TryRead(read.Catalog.Posters.Single().Poster.ContentLayoutJson, f.Main, out var doc, out _));
        Assert.Equal(f.Current.ContentCatalog.PosterItems.Select(p => p.LayoutItemId).Order(), doc!.Placements.Select(p => p.ItemId).Order());
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(command).Status);
        Assert.Equal(result.CommittedRevision, f.Current.Revision);
    }

    [Fact] public void FirstAddRequiresExplicitDemoConversion()
    {
        using var f = new Fixture();
        var result = f.Coordinator.Execute(f.AddCommand(f.Main, Entry()) with { ConfirmDemoReplacement = false });
        Assert.Equal(StateCommandStatus.ConfirmationRequired, result.Status); Assert.Empty(f.Current.ContentCatalog.Items);
    }

    [Fact] public void SkipsDoNotAllocateCollectionOrRevision()
    {
        using var f = new Fixture(); long revision = f.Current.Revision;
        Assert.True(f.Coordinator.Execute(f.AddCommand(f.Main, Entry() with { Choice = ContentReferenceChoice.Skip })).Succeeded);
        Assert.Equal(revision, f.Current.Revision); Assert.Empty(f.Current.ContentCatalog.Bindings);
    }

    [Fact] public void ExplicitReuseSharesEntityButNotPosterPlacement()
    {
        using var f = new Fixture(); var entry = Entry();
        f.Commit(f.AddCommand(f.Main, entry)); string b = f.NewPoster();
        f.Commit(f.AddCommand(b, entry with { Choice = ContentReferenceChoice.Reuse, ReuseItemId = entry.Item.Id }));
        Assert.Single(f.Current.ContentCatalog.Items); Assert.Equal(2, f.Current.ContentCatalog.PosterItems.Count);
        Assert.Equal(2, f.Current.ContentCatalog.Memberships.Count);
        long rev = f.Current.Revision;
        f.Commit(f.AddCommand(b, entry with { Choice = ContentReferenceChoice.Reuse, ReuseItemId = entry.Item.Id }));
        Assert.Equal(rev, f.Current.Revision); Assert.Equal(2, f.Current.ContentCatalog.PosterItems.Count);
    }

    [Fact] public void IndependentDuplicateDoesNotAutoMerge()
    {
        using var f = new Fixture(); f.Commit(f.AddCommand(f.Main, Entry(), Entry()));
        Assert.Equal(2, f.Current.ContentCatalog.Items.Count);
    }

    [Fact] public void SharedRepairChecksAllPostersAndKeepsLayoutIdentity()
    {
        using var f = new Fixture(); var entry = Entry(); f.Commit(f.AddCommand(f.Main, entry));
        string b = f.NewPoster(); f.Commit(f.AddCommand(b, entry with { Choice = ContentReferenceChoice.Reuse, ReuseItemId = entry.Item.Id }));
        string? layout = f.Current.PostersById[f.Main].Poster.ContentLayoutJson;
        var incomplete = f.AddCommand(f.Main, Entry("new.txt")) with { RepairContentId = entry.Item.Id };
        Assert.Equal(StateCommandStatus.ValidationFailed, f.Coordinator.Execute(incomplete).Status);
        var repair = incomplete with { RequestId = Guid.NewGuid().ToString(), ReadSet = incomplete.ReadSet with
            { Posters = f.Current.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion) } };
        long rev = f.Current.Revision; var before = f.Current;
        f.Commit(repair);
        Assert.Equal(rev + 1, f.Current.Revision); Assert.Equal(layout, f.Current.PostersById[f.Main].Poster.ContentLayoutJson);
        Assert.Equal(2, f.Current.ContentCatalog.Items.Single().Version);
        Assert.Equal(entry.Item.Id, f.Current.ContentCatalog.Items.Single().Id);
        Assert.Equal(before.PostersById[b].EntityVersion + 1, f.Current.PostersById[b].EntityVersion);
    }

    [Fact] public void StaleContentOrAffectedPosterNeverPartiallyApplies()
    {
        using var f = new Fixture(); var stale = f.AddCommand(f.Main, Entry());
        f.Commit(f.AddCommand(f.Main, Entry("different")));
        Assert.Equal(StateCommandStatus.Conflict, f.Coordinator.Execute(stale).Status);
        Assert.Single(f.Current.ContentCatalog.Items);
        var current = f.AddCommand(f.Main, Entry());
        var p = f.Current.PostersById[f.Main];
        f.Coordinator.Execute(new PosterStateCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter,
            PosterStateCommandKind.Rename, f.Current.Revision, f.Main, p.Poster with { Title = "changed" }) { ReadSet = StateReadSet.ForPoster(f.Main, p.EntityVersion) });
        Assert.Equal(StateCommandStatus.Conflict, f.Coordinator.Execute(current).Status); Assert.Single(f.Current.ContentCatalog.Items);
    }

    [Fact] public void SqlFailureRollsBackPosterCatalogAndReceipt()
    {
        using var f = new Fixture(); long revision = f.Current.Revision;
        f.Sql("CREATE TRIGGER reject_reference BEFORE INSERT ON content_poster_items BEGIN SELECT RAISE(ABORT,'test'); END;");
        var command = f.AddCommand(f.Main, Entry());
        Assert.False(f.Coordinator.Execute(command).Succeeded);
        Assert.Equal(revision, f.Coordinator.Refresh().Revision); Assert.Empty(f.Current.ContentCatalog.Items);
        Assert.Null(f.Current.PostersById[f.Main].Poster.ContentLayoutJson);
        Assert.Equal(0L, f.Sql("SELECT count(*) FROM content_demo_archive"));
        f.Sql("DROP TRIGGER reject_reference;"); f.Commit(command);
    }

    [Fact] public void LayoutOnlyReplacementCannotOrphanReferences()
    {
        using var f = new Fixture(); f.Commit(f.AddCommand(f.Main, Entry()));
        var p = f.Current.PostersById[f.Main];
        ContentLayoutPolicy.TryRead(p.Poster.ContentLayoutJson, f.Main, out var old, out _);
        var desired = ContentLayoutSamples.Equal(f.Main) with { DocumentVersion = old!.DocumentVersion + 1, ItemSetVersion = old.ItemSetVersion + 1 };
        var result = f.Coordinator.Execute(new ContentLayoutCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter,
            f.Current.Revision, f.Main, StateReadSet.ForPoster(f.Main, p.EntityVersion), old.DocumentVersion, old.ItemSetVersion, desired, true));
        Assert.False(result.Succeeded); Assert.Single(f.Current.ContentCatalog.PosterItems);
    }

    [Fact] public void RemovingPosterDoesNotRemoveSharedContentOrFiles()
    {
        using var f = new Fixture(); f.Commit(f.AddCommand(f.Main, Entry()));
        var preview = f.Coordinator.PrepareRemoval(f.Main)!;
        f.Commit(new RemovePosterCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter, preview.Revision, preview.ReadSet, f.Main, true));
        Assert.Single(f.Current.ContentCatalog.Items); Assert.Empty(f.Current.ContentCatalog.PosterItems);
        Assert.Empty(f.Current.ContentCatalog.Bindings); Assert.Equal(2, f.Current.ContentVersion);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UpgradeTwelvePreservesOpaqueDemoAndMigrationHistory(bool unknown)
    {
        using var f = new Fixture();
        string raw = unknown ? "{\"schemaVersion\":999,\"future\":true}" : ContentLayoutPolicy.Serialize(ContentLayoutSamples.Equal(f.Main));
        f.SetLayout(raw);
        var history = f.Sql("SELECT group_concat(checksum_sha256) FROM schema_migrations WHERE version<=12");
        f.DowngradeToTwelve();
        Assert.True(f.Store.Initialize().Succeeded);
        Assert.Equal(18L, f.Sql("PRAGMA user_version")); Assert.Equal(history, f.Sql("SELECT group_concat(checksum_sha256) FROM schema_migrations WHERE version<=12"));
        Assert.Equal(raw, new SqliteStateCommandRepository(f.Store).Read().Catalog.Posters.Single().Poster.ContentLayoutJson);
        Assert.Empty(f.Current.ContentCatalog.Items);
        if (unknown) Assert.False(f.Coordinator.Execute(f.AddCommand(f.Main, Entry())).Succeeded);
    }

    [Fact] public void FailedThirteenMigrationLeavesTwelveIntact()
    {
        using var f = new Fixture(); f.DowngradeToTwelve();
        f.Sql("CREATE TABLE content_items (collision TEXT);");
        Assert.False(f.Store.Initialize().Succeeded); Assert.Equal(12L, f.Sql("PRAGMA user_version"));
        Assert.Equal(0L, f.Sql("SELECT count(*) FROM schema_migrations WHERE version=13"));
    }

    internal static ContentReferenceEntry Entry(string name = "sample.txt") => new(new(Guid.NewGuid(), ContentKind.File,
        new("local-file-reference-v1", @"C:\fixture\" + name), new(ContentTargetKind.FileSystem, @"C:\fixture\" + name), name,
        ContentCapability.Reference | ContentCapability.Open), ContentReferenceChoice.CreateIndependent);

    internal sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "DesktopUp.Reference.Tests", Guid.NewGuid().ToString("N"));
        public SqliteStateStore Store { get; }
        public UserDataPaths Paths { get; }
        public StateProjectionCoordinator Coordinator { get; }
        public string Main => BuiltInStateIdentities.PrimaryPosterId;
        public Fixture()
        {
            Paths = new(root); Store = new(Paths); Assert.True(Store.Initialize().Succeeded);
            Coordinator = new(new SqliteStateCommandRepository(Store));
            Coordinator.EnsureInitialized(PosterConfigurationFactory.NewBasicBox("test", "DISPLAY1") with { PosterId = Main });
        }
        public DesktopUpProjection Current { get { Coordinator.TryGetCurrent(out var p); return p!; } }
        public ContentReferenceCommand AddCommand(string id, params ContentReferenceEntry[] entries) => new(Guid.NewGuid().ToString(),
            StateCommandSource.ControlCenter, Current.Revision, StateReadSet.ForPoster(id, Current.PostersById[id].EntityVersion) with
            { ContentVersion = Current.ContentVersion }, id, entries, true);
        public void Commit(ContentReferenceCommand command) { var r = Coordinator.Execute(command); Assert.True(r.Succeeded, r.Error); }
        public void Commit(RemovePosterCommand command) { var r = Coordinator.Execute(command); Assert.True(r.Succeeded, r.Error); }
        public string NewPoster()
        {
            var r = Coordinator.Execute(new CreatePosterCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter,
                Current.Revision, StateReadSet.ForCatalog(Current.CatalogVersion), PosterConfigurationFactory.NewBasicBox("new", "new")));
            Assert.True(r.Succeeded, r.Error); return r.ResultPosterId!;
        }
        public object? Sql(string sql)
        {
            using var c = new SqliteConnection($"Data Source={Path.Combine(Paths.State, "desktopup.sqlite3")};Pooling=False"); c.Open();
            using var q = c.CreateCommand(); q.CommandText = sql; return q.ExecuteScalar();
        }
        public void SetLayout(string raw)
        {
            using var c = new SqliteConnection($"Data Source={Path.Combine(Paths.State, "desktopup.sqlite3")};Pooling=False"); c.Open();
            using var q = c.CreateCommand(); q.CommandText = "UPDATE posters SET content_layout_json=$raw"; q.Parameters.AddWithValue("$raw", raw); q.ExecuteNonQuery();
            Coordinator.Refresh();
        }
        public void DowngradeToTwelve() => Sql("DROP TABLE managed_storage_adoptions; DROP TABLE content_transfer_closures; DROP TABLE storage_provision_closures; DROP TABLE content_transfer_items; DROP TABLE content_transfers; DROP TABLE managed_storage_collections; DROP TABLE managed_storage_roots; DROP TABLE content_icon_assets; DROP TABLE content_open_facts; DROP TABLE content_open_requests; DROP TABLE content_demo_archive; DROP TABLE content_poster_items; DROP TABLE content_poster_bindings; DROP TABLE content_memberships; DROP TABLE content_collections; DROP TABLE content_items; DROP TABLE content_head; DELETE FROM schema_migrations WHERE version>=13; UPDATE state_head SET schema_version=12; PRAGMA user_version=12;");
        public void Dispose() { Store.Dispose(); SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
    }
}
