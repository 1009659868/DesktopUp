using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentLayoutHistoryTransactionTests
{
    [Theory]
    [InlineData("guides")] [InlineData("single-card")] [InlineData("membership")]
    public void Undo_redo_rebase_versions_and_pass_real_command_sqlite_single_revision(string kind)
    {
        using var f=new Fixture();var before=f.Read();var h=new ContentLayoutHistory();
        var desired=kind=="membership"?Rebase(before,ContentLayoutSamples.Covers("A")):
            ContentLayoutEditing.Apply(before,kind=="guides"?new(ContentLayoutEditKind.Guides){ShowBaselineGrid=false}:
                new(ContentLayoutEditKind.Resize,before.Placements[0].ItemId){ColumnSpan=6,RowSpan=4},new(354,300)).Document;
        long revision=f.Coordinator.Refresh().Revision;
        Assert.Equal(StateCommandStatus.Committed,f.Commit(before,desired).Status);Assert.Equal(revision+1,f.Coordinator.Refresh().Revision);
        h.Record(before,f.Read());var after=f.Read();var undo=h.ProposeUndo(after);Assert.NotNull(undo);
        var undoResult=f.Commit(after,undo.Document);Assert.Equal(StateCommandStatus.Committed,undoResult.Status);
        Assert.True(h.Confirm(undo,f.Read()));Assert.True(ContentLayoutEditing.SameState(before,f.Read(),true));
        Assert.Equal(revision+2,f.Coordinator.Refresh().Revision);
        if(kind=="guides") Assert.All(f.Read().Placements,p=>Assert.Equal(1,p.Version));
        if(kind=="single-card") {Assert.Equal(3,f.Read().Placements[0].Version);Assert.All(f.Read().Placements.Skip(1),p=>Assert.Equal(1,p.Version));}
        if(kind=="membership") Assert.Equal(3,f.Read().ItemSetVersion);
        var redo=h.ProposeRedo(f.Read());Assert.NotNull(redo);var redoResult=f.Commit(f.Read(),redo.Document);
        Assert.Equal(StateCommandStatus.Committed,redoResult.Status);Assert.True(h.Confirm(redo,f.Read()));
        Assert.True(ContentLayoutEditing.SameState(after,f.Read(),true));Assert.Equal(revision+3,f.Coordinator.Refresh().Revision);
        if(kind=="membership") {Assert.Equal(4,f.Read().ItemSetVersion);Assert.All(f.Read().Placements.Skip(6),p=>Assert.Equal(1,p.Version));}
        using var reopened=new SqliteStateStore(f.Paths);Assert.True(reopened.Initialize().Succeeded);
        var persisted=new SqlitePosterRepository(reopened).Read("A")!.Poster.ContentLayoutJson;
        Assert.Equal(ContentLayoutPolicy.Serialize(f.Read()),persisted);
    }

    [Fact]
    public void Failed_transaction_does_not_consume_history_or_partial_versions()
    {
        using var f=new Fixture();var before=f.Read();var after=ContentLayoutEditing.Apply(before,new(ContentLayoutEditKind.Guides){ShowBaselineGrid=false},new(354,300)).Document;
        Assert.True(f.Commit(before,after).Succeeded);var h=new ContentLayoutHistory();h.Record(before,f.Read());var p=h.ProposeUndo(f.Read())!;
        using(var connection=new SqliteConnection($"Data Source={Path.Combine(f.Paths.State,"desktopup.sqlite3")};Pooling=False"))
        {connection.Open();using var command=connection.CreateCommand();command.CommandText="CREATE TRIGGER undo_fault BEFORE UPDATE ON posters BEGIN SELECT RAISE(ABORT,'undo fault'); END;";command.ExecuteNonQuery();}
        var result=f.Commit(f.Read(),p.Document);Assert.False(result.Succeeded);Assert.False(h.Confirm(p,f.Read()));
        Assert.Equal(1,h.UndoCount);Assert.Equal(0,h.RedoCount);Assert.True(ContentLayoutEditing.SameState(after,f.Read()));
    }

    private static ContentLayoutDocument Rebase(ContentLayoutDocument before,ContentLayoutDocument desired)
    {
        var old=before.Placements.ToDictionary(p=>p.ItemId,StringComparer.Ordinal);
        return desired with {DocumentVersion=before.DocumentVersion+1,ItemSetVersion=before.ItemSetVersion+1,
            Placements=desired.Placements.Select(p=>old.TryGetValue(p.ItemId,out var current)?p with{Version=current.Version+((p with{Version=current.Version})==current?0:1)}:p with{Version=1}).ToArray()};
    }
    private sealed class Fixture:IDisposable
    {
        private readonly string _root=Path.Combine(Path.GetTempPath(),"DesktopUp.ContentHistory.Tests",Guid.NewGuid().ToString("N"));
        private readonly SqliteStateStore _store;private readonly SqliteStateCommandRepository _repository;
        public UserDataPaths Paths{get;}public StateProjectionCoordinator Coordinator{get;}
        public Fixture(){Paths=new(_root);_store=new(Paths);Assert.True(_store.Initialize().Succeeded);_repository=new(_store);Coordinator=new(_repository);
            Coordinator.EnsureInitialized(PosterConfigurationFactory.NewBasicBox("A","DISPLAY1") with{PosterId="A",ThemeInstanceId="theme-A"});}
        public ContentLayoutDocument Read(){Assert.True(ContentLayoutPolicy.TryRead(_repository.Read().Catalog.Posters[0].Poster.ContentLayoutJson,"A",out var d,out var error),error);return d!;}
        public StateCommandResult Commit(ContentLayoutDocument before,ContentLayoutDocument desired)
        {
            var state=_repository.Read();var p=state.Catalog.Posters[0];bool changed=!before.Placements.Select(i=>i.ItemId).ToHashSet().SetEquals(desired.Placements.Select(i=>i.ItemId));
            return Coordinator.Execute(new ContentLayoutCommand(Guid.NewGuid().ToString("N"),StateCommandSource.ControlCenter,state.Revision,"A",StateReadSet.ForPoster("A",p.EntityVersion),before.DocumentVersion,before.ItemSetVersion,desired,changed));
        }
        public void Dispose(){_store.Dispose();SqliteConnection.ClearAllPools();if(Directory.Exists(_root))Directory.Delete(_root,true);}
    }
}
