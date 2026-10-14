using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using static DesktopUp.Contracts.Tests.ContentReferencePersistenceTests;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentIconPersistenceTests
{
    internal static ContentIconAsset Icon(byte color = 70) => ContentIconAsset.Create(2, 1, [color, 0, 0, 255, 0, 0, 0, 0]);
    internal static ContentIconCommand Command(Fixture f, string poster, ContentIconAsset? asset)
    {
        var relation = f.Current.ContentCatalog.PosterItems.First(p => p.PosterId == poster);
        var witness = ContentRelationPolicy.Resolve(f.Current.ContentCatalog, poster, relation.LayoutItemId)!.Witness;
        return new(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter, f.Current.Revision,
            StateReadSet.ForPoster(poster, f.Current.PostersById[poster].EntityVersion) with { ContentVersion = f.Current.ContentVersion }, witness, asset, true);
    }
    private static void Commit(Fixture f, ContentIconCommand c) { var r = f.Coordinator.Execute(c); Assert.True(r.Succeeded, r.Error); }

    [Fact] public void CanonicalHashIncludesDimensionsAndCopiesInputAndOutput()
    {
        byte[] data = [0,0,0,0,1,2,3,255]; var a = ContentIconAsset.Create(2,1,data); data[4] = 100;
        a.Pixels[4] = 200;
        Assert.Equal(1,a.Pixels[4]); Assert.NotEqual(a.Id, ContentIconAsset.Create(1,2,a.Pixels).Id);
        Assert.Equal(a.Id,ContentIconAsset.Create(2,1,a.Pixels).Id); Assert.True(ContentIconAsset.ValidId(a.Id));
        Assert.False(ContentIconAsset.ValidId(a.Id.ToUpperInvariant()));
    }
    [Theory] [InlineData(0,1,4)] [InlineData(257,1,1028)] [InlineData(1,1,3)]
    public void InvalidRasterDimensionsFail(int w,int h,int length) => Assert.Throws<ArgumentException>(()=>ContentIconAsset.Create(w,h,new byte[length]));
    [Fact] public void UnpremultipliedPixelIsRejected() => Assert.Throws<ArgumentException>(()=>ContentIconAsset.Create(1,1,[200,0,0,10]));

    [Fact] public async Task OneCardChangesOnceWithoutChangingTargetOtherPosterOrLayoutAndRestarts()
    {
        using var f = new Fixture(); var entry = Entry(); f.Commit(f.AddCommand(f.Main,entry)); var b=f.NewPoster();
        f.Commit(f.AddCommand(b,Entry("other.txt")));
        var before=f.Current; var command=Command(f,f.Main,Icon()); Commit(f,command);
        Assert.Equal(before.Revision+1,f.Current.Revision); Assert.Equal(before.ContentVersion+1,f.Current.ContentVersion);
        Assert.Equal(before.ContentCatalog.Items,f.Current.ContentCatalog.Items);
        Assert.Equal(before.PostersById[b].Poster,f.Current.PostersById[b].Poster);
        Assert.Equal(before.PostersById[b].EntityVersion,f.Current.PostersById[b].EntityVersion);
        Assert.Equal(before.PostersById[b].PosterRevision,f.Current.PostersById[b].PosterRevision);
        Assert.Equal(before.PostersById[f.Main].Poster,f.Current.PostersById[f.Main].Poster);
        Assert.Null(f.Current.ContentCatalog.PosterItems.Single(p=>p.PosterId==b).IconAssetId);
        Assert.Equal(StateCommandStatus.Replayed,f.Coordinator.Execute(command).Status);
        Commit(f,Command(f,f.Main,Icon())); Assert.Equal(before.Revision+1,f.Current.Revision);
        using var reopened=new SqliteStateStore(f.Paths); Assert.True(reopened.Initialize().Succeeded);
        var stored=await new SqliteContentIconStore(reopened).ReadAsync(Icon().Id,CancellationToken.None);
        Assert.Equal(Icon().Pixels,stored!.Pixels);
        Assert.Equal(Icon().Id,new SqliteStateCommandRepository(reopened).Read().ContentCatalog.PosterItems.Single(p=>p.PosterId==f.Main).IconAssetId);
    }
    [Fact] public void DeduplicatesAssetsAndCollectsOnlyAfterLastReferenceIsRemoved()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));var b=f.NewPoster();f.Commit(f.AddCommand(b,Entry("other.txt")));
        Commit(f,Command(f,f.Main,Icon()));Commit(f,Command(f,b,Icon())); Assert.Equal(1L,f.Sql("SELECT count(*) FROM content_icon_assets"));
        Commit(f,Command(f,f.Main,null)); Assert.Equal(1L,f.Sql("SELECT count(*) FROM content_icon_assets"));
        var relation=f.Current.ContentCatalog.PosterItems.Single(p=>p.PosterId==b);
        var reads=Command(f,b,null).ReadSet;
        Assert.True(f.Coordinator.Execute(new ContentDetachCommand(Guid.NewGuid().ToString(),StateCommandSource.ControlCenter,
            f.Current.Revision,reads,b,[relation.LayoutItemId],true)).Succeeded);
        Assert.Equal(0L,f.Sql("SELECT count(*) FROM content_icon_assets")); Assert.Equal(2,f.Current.ContentCatalog.Items.Count);
    }
    [Fact] public void RemovingPosterReclaimsItsOwnedIconWithoutRemovingSharedEntity()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));Commit(f,Command(f,f.Main,Icon()));
        f.Commit(new RemovePosterCommand(Guid.NewGuid().ToString(),StateCommandSource.ControlCenter,f.Current.Revision,
            StateReadSet.ForPoster(f.Main,f.Current.PostersById[f.Main].EntityVersion) with {CatalogVersion=f.Current.CatalogVersion},f.Main,true));
        Assert.Equal(0L,f.Sql("SELECT count(*) FROM content_icon_assets")); Assert.Single(f.Current.ContentCatalog.Items);
    }
    [Theory] [InlineData("stale")] [InlineData("locked")] [InlineData("cancelled")] [InlineData("unconfirmed")]
    public void InvalidAdmissionNeverStoresAssetOrPartiallyChangesCard(string reason)
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));var cmd=Command(f,f.Main,Icon());
        if(reason=="stale") cmd=cmd with {Relation=cmd.Relation with {ItemVersion=99}};
        if(reason=="locked") { f.Sql("UPDATE posters SET is_locked=1"); f.Coordinator.Refresh(); }
        if(reason=="unconfirmed")cmd=cmd with {Confirmed=false};
        long revision=f.Current.Revision;
        var result=f.Coordinator.Execute(cmd,new CancellationToken(reason=="cancelled"));
        Assert.False(result.Succeeded);Assert.Equal(revision,f.Current.Revision);
        Assert.Equal(0L,f.Sql("SELECT count(*) FROM content_icon_assets"));Assert.Null(f.Current.ContentCatalog.PosterItems.Single().IconAssetId);
    }
    [Fact] public void ConflictingReadSetAndRequestReuseDoNotReplaceIcon()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));var stale=Command(f,f.Main,Icon(1));
        var committed=Command(f,f.Main,Icon(2));Commit(f,committed);
        Assert.Equal(StateCommandStatus.Conflict,f.Coordinator.Execute(stale).Status);
        Assert.Equal(StateCommandStatus.ValidationFailed,f.Coordinator.Execute(committed with {Asset=Icon(3)}).Status);
        Assert.Equal(Icon(2).Id,f.Current.ContentCatalog.PosterItems.Single().IconAssetId);
    }
    [Theory] [InlineData("content_poster_items")] [InlineData("state_command_receipts")]
    public void SqlFailureRollsBackBothBlobAndReference(string table)
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));Commit(f,Command(f,f.Main,Icon(1)));
        var before=f.Current;f.Sql($"CREATE TRIGGER icon_test_fail BEFORE INSERT ON {table} BEGIN SELECT RAISE(ABORT,'owned fault'); END;");
        Assert.Equal(StateCommandStatus.Failed,f.Coordinator.Execute(Command(f,f.Main,Icon(2))).Status);
        Assert.Equal(before.Revision,f.Current.Revision);Assert.Equal(Icon(1).Id,f.Current.ContentCatalog.PosterItems.Single().IconAssetId);
        Assert.Equal(1L,f.Sql("SELECT count(*) FROM content_icon_assets"));
    }
    [Fact] public async Task BackupRestoreIncludesImageAndReferenceAtomically()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));Commit(f,Command(f,f.Main,Icon()));
        var backup=f.Store.CreateBackup("icon-roundtrip");Assert.True(backup.Succeeded,backup.Error);
        byte[] original=File.ReadAllBytes(Path.Combine(f.Paths.Backups,backup.FileName!));
        Commit(f,Command(f,f.Main,null)); f.Store.Dispose();
        var restore=new StateBackupRecovery(f.Paths,()=>true).Restore(backup.FileName!);Assert.True(restore.Succeeded,restore.Error);
        using var reopened=new SqliteStateStore(f.Paths);Assert.True(reopened.Initialize().Succeeded);
        Assert.Equal(Icon().Id,new SqliteStateCommandRepository(reopened).Read().ContentCatalog.PosterItems.Single().IconAssetId);
        Assert.Equal(Icon().Pixels,(await new SqliteContentIconStore(reopened).ReadAsync(Icon().Id,CancellationToken.None))!.Pixels);
        Assert.Equal(original,File.ReadAllBytes(Path.Combine(f.Paths.Backups,backup.FileName!)));
    }
    private static void Downgrade(Fixture f) => f.Sql(StorageSchemaFixture.Drop19+"ALTER TABLE content_poster_items DROP COLUMN icon_asset_id; DROP TABLE managed_storage_adoptions; DROP TABLE content_transfer_closures; DROP TABLE storage_provision_closures; DROP TABLE content_transfer_items; DROP TABLE content_transfers; DROP TABLE managed_storage_collections; DROP TABLE managed_storage_roots; DROP TABLE content_icon_assets; DELETE FROM schema_migrations WHERE version>=15; UPDATE state_head SET schema_version=14; PRAGMA user_version=14;");
    [Fact] public void MigrationPreservesV14JsonAndChecksumsAndCreatesBackup()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));Downgrade(f);
        var raw=f.Sql("SELECT body_json FROM content_poster_items"); var checks=f.Sql("SELECT group_concat(checksum_sha256) FROM schema_migrations ORDER BY version");
        var result=f.Store.Initialize(); Assert.True(result.Succeeded,result.Error);Assert.Equal(14,result.PreviousSchemaVersion);Assert.NotNull(result.PreMigrationBackupFileName);
        Assert.Equal(raw,f.Sql("SELECT body_json FROM content_poster_items"));Assert.Equal(checks,f.Sql("SELECT group_concat(checksum_sha256) FROM schema_migrations WHERE version<=14 ORDER BY version"));
        Assert.Equal(24L,f.Sql("PRAGMA user_version")); Assert.Equal(0L,f.Sql("SELECT count(*) FROM content_icon_assets"));
    }
    [Fact] public void FailedMigrationKeepsVersionAndOriginalRelations()
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));Downgrade(f);
        var original=f.Sql("SELECT body_json FROM content_poster_items");
        f.Sql("CREATE TABLE content_icon_assets (owned_collision TEXT);");
        Assert.False(f.Store.Initialize().Succeeded);Assert.Equal(14L,f.Sql("PRAGMA user_version"));
        Assert.Equal(original,f.Sql("SELECT body_json FROM content_poster_items"));Assert.Equal(0L,f.Sql("SELECT count(*) FROM pragma_table_info('content_poster_items') WHERE name='icon_asset_id'"));
    }
    [Theory] [InlineData("future")] [InlineData("corrupt")] [InlineData("future-field")]
    public void FutureSchemaOrCorruptedAssetIsNotSilentlyReset(string damage)
    {
        using var f=new Fixture();f.Commit(f.AddCommand(f.Main,Entry()));Commit(f,Command(f,f.Main,Icon()));
        f.Sql(damage switch {"future"=>"PRAGMA user_version=9999", "corrupt"=>"UPDATE content_icon_assets SET pixels=zeroblob(length(pixels))",
            _=>"UPDATE content_poster_items SET body_json=json_set(body_json,'$.futureIconMode',1)"});
        var original=f.Sql("SELECT hex(pixels) FROM content_icon_assets");
        Assert.False(f.Store.Initialize().Succeeded); Assert.Equal(original,f.Sql("SELECT hex(pixels) FROM content_icon_assets"));
    }
}
