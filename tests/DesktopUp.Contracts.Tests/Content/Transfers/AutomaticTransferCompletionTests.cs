using DesktopUp.Application;
using DesktopUp.Infrastructure;
using Fixture = DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class AutomaticTransferCompletionTests
{
    [Fact] public void OrdinaryFailureFinishesAndNewDragCanRetry()
    {
        using var f=new Fixture(); string source=f.File("retry.txt"); var p=f.Stage(source);
        var done=Assert.Single(new AutomaticTransferCompletion(f.Journal).Execute(p.Plan.Request.OperationId,
            checkpoint:(at,_)=>{if(at==TransferExecutionCheckpoint.BeforeMove)throw new IOException("fixture failure");}));
        Assert.Equal(TransferFinishKind.NotMoved,done.Kind);Assert.True(done.Item.IsFinished);
        Assert.True(File.Exists(source));Assert.False(File.Exists(p.Files[0].Destination));
        var retry=f.Stage(source);
        Assert.Equal(TransferFinishKind.Completed,Assert.Single(new AutomaticTransferCompletion(f.Journal).Execute(retry.Plan.Request.OperationId)).Kind);
    }
    [Theory]
    [InlineData(TransferExecutionCheckpoint.Claimed,false)]
    [InlineData(TransferExecutionCheckpoint.AfterMove,true)]
    [InlineData(TransferExecutionCheckpoint.FileRecorded,true)]
    public void RestartFinishesEvidenceWithoutReplayingMove(TransferExecutionCheckpoint point,bool moved)
    {
        using var f=new Fixture();var p=f.Stage(f.File("restart.txt"));
        Assert.Throws<TransferInterruptionException>(()=>f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>
        {if(at==point)throw new TransferInterruptionException("crash");}));
        var finish=Assert.Single(new AutomaticTransferCompletion(f.Journal).Settle());
        Assert.Equal(moved?TransferFinishKind.Completed:TransferFinishKind.NotMoved,finish.Kind);
        Assert.True(finish.Item.IsFinished);Assert.Equal(!moved,File.Exists(p.Files[0].Source));
        long revision=f.State.Revision;new AutomaticTransferCompletion(f.Journal).Settle();Assert.Equal(revision,f.State.Revision);
    }
    [Fact] public void NeverStartedIsFinishedNotExecuted()
    {
        using var f=new Fixture();var p=f.Stage(f.File("intent.txt"));
        Assert.Equal(TransferFinishKind.NotMoved,Assert.Single(new AutomaticTransferCompletion(f.Journal).Settle()).Kind);
        Assert.True(File.Exists(p.Files[0].Source));Assert.Empty(f.State.ContentCatalog.PosterItems);
        f.Stage(p.Files[0].Source);
    }
    [Fact] public void CollisionPreservesBothAndDoesNotReserveFutureAttempts()
    {
        using var f=new Fixture();var p=f.Stage(f.File("collision.txt"));File.WriteAllText(p.Files[0].Destination,"other");
        var finish=Assert.Single(new AutomaticTransferCompletion(f.Journal).Execute(p.Plan.Request.OperationId));
        Assert.Equal(TransferFinishKind.Preserved,finish.Kind);Assert.True(finish.Item.IsFinished);
        Assert.Equal("other",File.ReadAllText(p.Files[0].Destination));Assert.True(File.Exists(p.Files[0].Source));
        // Only this self-created competing fixture is renamed; a subsequent fresh plan works.
        File.Move(p.Files[0].Destination,p.Files[0].Destination+".other");f.Stage(p.Files[0].Source);
    }
    [Fact] public void ExternalRenameIsNotMovedBackOrReplayed()
    {
        using var f=new Fixture();var p=f.Stage(f.File("renamed.txt"));string external=p.Files[0].Source+".new";
        File.Move(p.Files[0].Source,external);
        var finish=Assert.Single(new AutomaticTransferCompletion(f.Journal).Settle());
        Assert.Equal(TransferFinishKind.Preserved,finish.Kind);Assert.True(finish.Item.IsFinished);
        Assert.True(File.Exists(external));Assert.False(File.Exists(p.Files[0].Destination));
        var fresh=f.Stage(external);Assert.Equal(TransferFinishKind.Completed,Assert.Single(new AutomaticTransferCompletion(f.Journal).Execute(fresh.Plan.Request.OperationId)).Kind);
    }
    [Fact] public void PersistentIndexFailurePreservesMovedFileAndClosesScheduling()
    {
        using var f=new Fixture();var p=f.Stage(f.File("index.txt"));
        f.Sql("CREATE TRIGGER fail_index BEFORE UPDATE ON content_head BEGIN SELECT RAISE(ABORT,'fixture'); END;");
        var finish=Assert.Single(new AutomaticTransferCompletion(f.Journal).Execute(p.Plan.Request.OperationId));
        Assert.Equal(TransferFinishKind.Preserved,finish.Kind);Assert.True(finish.Item.IsFinished);
        Assert.False(File.Exists(p.Files[0].Source));Assert.True(File.Exists(p.Files[0].Destination));
        Assert.Empty(f.State.ContentCatalog.PosterItems);
    }
    [Fact] public void DeletedPosterIsNotRecreatedByAutomaticIndexRepair()
    {
        using var f=new Fixture();var p=f.Stage(f.File("deleted-poster.txt"));
        Assert.Throws<TransferInterruptionException>(()=>f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>{if(at==TransferExecutionCheckpoint.AfterMove)throw new TransferInterruptionException("crash");}));
        f.Journal.Mark(p.Plan.Request.OperationId,0,DurableTransferPhase.Review);
        f.Sql("DELETE FROM posters");
        var done=Assert.Single(new AutomaticTransferCompletion(f.Journal).Settle());
        Assert.Equal(TransferFinishKind.Preserved,done.Kind);Assert.True(done.Item.IsFinished);
        Assert.Empty(f.State.Catalog.Posters);Assert.Empty(f.State.ContentCatalog.PosterItems);
        Assert.Equal("owned-content",File.ReadAllText(p.Files[0].Destination));
    }
    [Fact] public void AutomaticRepairDoesNotOverrideEditedTarget()
    {
        using var f=new Fixture();var p=f.Stage(f.File("edited.txt"));
        Assert.Throws<TransferInterruptionException>(()=>f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>{if(at==TransferExecutionCheckpoint.AfterMove)throw new TransferInterruptionException("crash");}));
        f.Journal.Mark(p.Plan.Request.OperationId,0,DurableTransferPhase.Review);
        f.Sql("UPDATE posters SET entity_version=entity_version+1");
        var done=Assert.Single(new AutomaticTransferCompletion(f.Journal).Settle());
        Assert.Equal(TransferFinishKind.Preserved,done.Kind);Assert.Empty(f.State.ContentCatalog.PosterItems);
        Assert.True(File.Exists(p.Files[0].Destination));
    }
    [Fact] public void IncompleteCopyIsNeverDeletedOrReportedCompleted()
    {
        using var f=new Fixture();var p=f.Stage(f.File("partial.txt"));
        File.WriteAllText(p.Files[0].Destination,"partial");
        var done=Assert.Single(new AutomaticTransferCompletion(f.Journal).Settle());
        Assert.Equal(TransferFinishKind.Preserved,done.Kind);Assert.True(done.Item.IsFinished);
        Assert.Equal("partial",File.ReadAllText(p.Files[0].Destination));Assert.Equal("owned-content",File.ReadAllText(p.Files[0].Source));
    }
    [Fact] public void AChangedOriginalSourceIsPreservedAndFreshDragRepreviewsIt()
    {
        using var f=new Fixture();var p=f.Stage(f.File("edited-source.txt"));
        File.WriteAllText(p.Files[0].Source,"changed");
        var done=Assert.Single(new AutomaticTransferCompletion(f.Journal).Execute(p.Plan.Request.OperationId));
        Assert.Equal(TransferFinishKind.Preserved,done.Kind);Assert.True(done.Item.IsFinished);
        var fresh=f.Stage(p.Files[0].Source);
        new AutomaticTransferCompletion(f.Journal).Execute(fresh.Plan.Request.OperationId);
        Assert.Equal("changed",File.ReadAllText(p.Files[0].Destination));
    }
}
