using DesktopUp.Application;
using DesktopUp.Infrastructure;
using F=DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class NativeShellExitTests
{
    private static (string Path, string Card) Collect(F f)
    {
        var p=f.Stage(f.File("shell-owned.txt"));
        Assert.True(Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId)).IsFinished);
        return(p.Files[0].Destination,Assert.Single(f.State.ContentCatalog.PosterItems).LayoutItemId);
    }
    [Fact] public void ReleasedNativeMoveOnlyRemovesIndexAndKeepsDestinationBytes()
    {
        using var f=new F(); var (path,card)=Collect(f); var journal=new NativeShellExitJournal(f.Store);
        var intent=journal.Begin(f.Main,[card]); string destination=Path.Combine(f.Source,"shell-target.txt");
        File.Move(path,destination); journal.End(intent,true);
        Assert.Equal(1,journal.ObserveReleasedMove(intent)); Assert.Empty(f.State.ContentCatalog.PosterItems);
        Assert.Equal("owned-content",File.ReadAllText(destination)); Assert.Equal(0,journal.ObserveReleasedMove(intent));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void MissingBeforeDragAlwaysRequiresExplicitPostReleaseConfirmation(bool restart)
    {
        using var f=new F(); var (path,card)=Collect(f); File.Move(path,Path.Combine(f.Source,"already-outside.txt"));
        var journal=new NativeShellExitJournal(f.Store); var intent=journal.Begin(f.Main,[card]); Assert.True(intent.Items[0].Missing);
        journal.End(intent,true);
        if(restart) {using var opened=new SqliteStateStore(f.Paths); Assert.True(opened.Initialize().Succeeded); Assert.Equal(0,new NativeShellExitJournal(opened).Reconcile());}
        Assert.Equal(0,journal.ObserveReleasedMove(intent)); Assert.Single(f.State.ContentCatalog.PosterItems);
        Assert.Equal(1,journal.RemoveStaleReferencesAfterUserConfirmation(intent)); Assert.Empty(f.State.ContentCatalog.PosterItems);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void CancelledOrNeverReleasedGestureCannotRemoveAnything(bool explicitCancel)
    {
        using var f=new F(); var (path,card)=Collect(f); var journal=new NativeShellExitJournal(f.Store); var intent=journal.Begin(f.Main,[card]);
        File.Move(path,Path.Combine(f.Source,"external.txt")); if(explicitCancel)journal.End(intent,false);
        Assert.Equal(0,journal.Reconcile()); Assert.Single(f.State.ContentCatalog.PosterItems);
    }
    [Fact] public void CopyOrSameParentRenameKeepsCardAndNoTargetIsDeleted()
    {
        using var f=new F(); var (path,card)=Collect(f); var journal=new NativeShellExitJournal(f.Store); var intent=journal.Begin(f.Main,[card]);
        File.Copy(path,Path.Combine(f.Source,"copied.txt")); journal.End(intent,true);
        Assert.Equal(0,journal.ObserveReleasedMove(intent)); File.Move(path,Path.Combine(Path.GetDirectoryName(path)!,"renamed.txt"));
        Assert.Equal(0,journal.ObserveReleasedMove(intent)); Assert.Single(f.State.ContentCatalog.PosterItems);
    }
    [Fact] public void DisconnectedParentIsNotProofOfAnExit()
    {
        using var f=new F(); var (path,card)=Collect(f); var journal=new NativeShellExitJournal(f.Store); var intent=journal.Begin(f.Main,[card]);
        string parent=Path.GetDirectoryName(path)!; Directory.Move(parent,parent+"-offline"); journal.End(intent,true);
        Assert.Equal(0,journal.ObserveReleasedMove(intent)); Assert.Single(f.State.ContentCatalog.PosterItems);
    }
    [Fact] public void NewReferencesCannotPublishASecondOwner()
    {
        using var f=new ContentReferencePersistenceTests.Fixture(); var input=ContentReferencePersistenceTests.Entry();
        f.Commit(f.AddCommand(f.Main,input)); string other=f.NewPoster();
        var result=f.Coordinator.Execute(f.AddCommand(other,input with {Choice=ContentReferenceChoice.Reuse,ReuseItemId=input.Item.Id}));
        Assert.False(result.Succeeded); Assert.Single(f.Current.ContentCatalog.PosterItems);
    }
    [Fact] public void LegacySharedCardsAreBackedUpBeforeDeterministicSingleOwnerMigration()
    {
        using var f=new ContentReferencePersistenceTests.Fixture(); var a=ContentReferencePersistenceTests.Entry("a.txt");
        f.Commit(f.AddCommand(f.Main,a)); string b=f.NewPoster(); var other=ContentReferencePersistenceTests.Entry("b.txt");
        f.Commit(f.AddCommand(b,other));
        // Only this owned fixture synthesizes the accepted pre-DEC-047 shared representation.
        f.Sql($"UPDATE content_memberships SET item_id='{a.Item.Id}',body_json=json_set(body_json,'$.contentItemId','{a.Item.Id}') WHERE item_id='{other.Item.Id}';");
        var catalog=new SqliteStateCommandRepository(f.Store).Read().ContentCatalog;
        Assert.Equal(2,catalog.PosterItems.Count);
        var before=new SqliteStateCommandRepository(f.Store).Read();
        var removed=ContentSingleOwnerPolicy.DuplicateCards(catalog,before.Catalog.Posters.Select(p=>p.Poster.PosterId).ToArray()).Single();
        Assert.Equal(1,SingleOwnerMigration.Apply(f.Store)); f.Coordinator.Refresh();
        Assert.DoesNotContain(removed,f.Current.ContentCatalog.PosterItems); Assert.Single(f.Current.ContentCatalog.PosterItems);
        Assert.Equal(catalog.Items,f.Current.ContentCatalog.Items); Assert.NotEmpty(Directory.GetFiles(f.Paths.Backups,"*.sqlite3"));
        Assert.Equal(0,SingleOwnerMigration.Apply(f.Store));
    }
}
