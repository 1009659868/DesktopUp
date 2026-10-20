using DesktopUp.Windows;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using F=DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class ManagedTransferIndexRecoveryTests
{
    private static LocalTransferPreview Interrupt(F f,string name="moved.txt")
    {
        var p=f.Stage(f.File(name));
        Assert.Throws<TransferInterruptionException>(()=>f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>
        {if(at==TransferExecutionCheckpoint.AfterMove)throw new TransferInterruptionException("owned fault");}));
        f.Journal.Mark(p.Plan.Request.OperationId,0,DurableTransferPhase.Review,error:"isolated interrupted move");
        return p;
    }
    private static string AddPoster(F f)
    {
        var p=PosterConfigurationFactory.NewBasicBox("Recovery destination","DISPLAY1") with {PosterId=Guid.NewGuid().ToString(),ThemeInstanceId=Guid.NewGuid().ToString()};var s=f.State;
        var r=f.Repository.Execute(new(Guid.NewGuid().ToString(),"poster",s.Revision,StateReadSet.ForCatalog(s.Catalog.CatalogVersion)),_=>new(){Creates=[p]});
        Assert.Equal(StateCommandStatus.Committed,r.Status);return p.PosterId;
    }
    [Fact] public void FreshConfirmationRepairsMovedFileAfterStateConflictWithoutMovingAgain()
    {
        using var f=new F();var p=Interrupt(f);f.AddReference(LocalContentReferenceProbe.Inspect(f.File("extra.txt")).Item!);
        string body=(string)f.Sql("SELECT body_json FROM content_transfers")!;
        var t=f.Journal.PreviewIndexRecovery(p.Plan.Request.OperationId,0);long revision=f.State.Revision;
        var result=f.Journal.ConfirmIndexRecovery(t,t.Fingerprint);
        Assert.Equal(StateCommandStatus.Committed,result.Status);Assert.Equal(revision+1,f.State.Revision);
        Assert.Equal(DurableTransferPhase.IndexCommitted,Assert.Single(f.Journal.Read()).Phase);
        Assert.Equal(body,f.Sql("SELECT body_json FROM content_transfers"));
        Assert.False(File.Exists(p.Files[0].Source));Assert.Equal("owned-content",File.ReadAllText(t.Destination));
        Assert.Equal(2,f.State.ContentCatalog.PosterItems.Count);
        Assert.Equal(StateCommandStatus.Replayed,f.Journal.ConfirmIndexRecovery(t,t.Fingerprint).Status);
        Assert.Equal(revision+1,f.State.Revision);
    }
    [Fact] public void DeletedOriginalPosterCanExplicitlyAssociateWithExistingPosterWithoutRecreatingIt()
    {
        using var f=new F();var p=Interrupt(f);string other=AddPoster(f);
        f.Sql($"DELETE FROM posters WHERE poster_id='{f.Main}'");
        Assert.Throws<IOException>(()=>f.Journal.PreviewIndexRecovery(p.Plan.Request.OperationId,0));
        var t=f.Journal.PreviewIndexRecovery(p.Plan.Request.OperationId,0,other);
        f.Journal.ConfirmIndexRecovery(t,t.Fingerprint);
        Assert.Equal(other,Assert.Single(f.State.Catalog.Posters).Poster.PosterId);
        Assert.Equal(other,Assert.Single(f.State.ContentCatalog.PosterItems).PosterId);
        Assert.Contains(other,Assert.Single(f.Journal.Read()).Error);
    }
    [Theory] [InlineData("content")] [InlineData("poster")] [InlineData("deleted")]
    public void ChangedStateAfterPreviewCannotConsumeConfirmation(string change)
    {
        using var f=new F();var p=Interrupt(f);var t=f.Journal.PreviewIndexRecovery(p.Plan.Request.OperationId,0);
        if(change=="content")f.AddReference(LocalContentReferenceProbe.Inspect(f.File("later.txt")).Item!);
        else if(change=="deleted")f.Sql("DELETE FROM posters");
        else f.Sql("UPDATE posters SET entity_version=entity_version+1");
        Assert.ThrowsAny<Exception>(()=>f.Journal.ConfirmIndexRecovery(t,t.Fingerprint));
        Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Journal.Read()).Phase);
        Assert.Equal("owned-content",File.ReadAllText(p.Files[0].Destination));
    }
    [Theory] [InlineData("source-reappeared")] [InlineData("target-changed")] [InlineData("target-replaced")] [InlineData("target-missing")]
    public void ChangedFilesNeverAuthorizeIndexOrCleanup(string change)
    {
        using var f=new F();var p=Interrupt(f);var t=f.Journal.PreviewIndexRecovery(p.Plan.Request.OperationId,0);
        if(change=="source-reappeared")File.WriteAllText(p.Files[0].Source,"new original path");
        else if(change=="target-changed")File.WriteAllText(p.Files[0].Destination,"changed");
        else {File.Move(p.Files[0].Destination,p.Files[0].Destination+".keep");if(change=="target-replaced")File.WriteAllText(p.Files[0].Destination,"owned-content");}
        Assert.ThrowsAny<IOException>(()=>f.Journal.ConfirmIndexRecovery(t,t.Fingerprint));
        Assert.Empty(f.State.ContentCatalog.PosterItems);Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Journal.Read()).Phase);
        if(change=="source-reappeared")Assert.Equal("new original path",File.ReadAllText(p.Files[0].Source));
    }
    [Fact] public void SqlFailureRollsBackIndexReceiptAndPhaseTogether()
    {
        using var f=new F();var p=Interrupt(f);var t=f.Journal.PreviewIndexRecovery(p.Plan.Request.OperationId,0);long revision=f.State.Revision;
        f.Sql("CREATE TRIGGER fail_recovery BEFORE INSERT ON state_command_receipts BEGIN SELECT RAISE(ABORT,'fault'); END;");
        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(()=>f.Journal.ConfirmIndexRecovery(t,t.Fingerprint));
        Assert.Equal(revision,f.State.Revision);Assert.Empty(f.State.ContentCatalog.PosterItems);
        Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Journal.Read()).Phase);
        f.Sql("DROP TRIGGER fail_recovery");f.Journal.ConfirmIndexRecovery(t,t.Fingerprint);
        using var restarted=new SqliteStateStore(f.Paths);Assert.True(restarted.Initialize().Succeeded);
        Assert.Equal(DurableTransferPhase.IndexCommitted,Assert.Single(new SqliteTransferJournal(restarted).Read()).Phase);
    }
    [Fact] public void WrongConfirmationAndRecordWitnessAreRejected()
    {
        using var f=new F();var p=Interrupt(f);var t=f.Journal.PreviewIndexRecovery(p.Plan.Request.OperationId,0);
        Assert.Throws<IOException>(()=>f.Journal.ConfirmIndexRecovery(t,"wrong"));
        f.Executor.Recover(p.Plan.Request.OperationId,0);
        Assert.Throws<IOException>(()=>f.Journal.ConfirmIndexRecovery(t,t.Fingerprint));
    }
    [Fact] public void PreviewDoesNotRewriteSharedRepair()
    {
        using var f=new F();string path=f.File("repaired.txt");f.AddReference(LocalContentReferenceProbe.Inspect(path).Item!);
        var p=f.Stage(path);Assert.Throws<TransferInterruptionException>(()=>f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>
        {if(at==TransferExecutionCheckpoint.AfterMove)throw new TransferInterruptionException("fault");}));
        f.Journal.Mark(p.Plan.Request.OperationId,0,DurableTransferPhase.Review);
        var s=f.State;var existing=Assert.Single(s.ContentCatalog.Items);var replacement=LocalContentReferenceProbe.Inspect(f.File("replacement.txt")).Item!;
        var cmd=new ContentReferenceCommand("repair",StateCommandSource.ControlCenter,s.Revision,
            StateReadSet.ForPoster(f.Main,s.Catalog.Posters.Single().EntityVersion) with {ContentVersion=s.ContentVersion},
            f.Main,[new(replacement,ContentReferenceChoice.CreateIndependent)],true,existing.Id);
        Assert.Equal(StateCommandStatus.Committed,f.Repository.Execute(new("repair","repair",s.Revision,cmd.ReadSet),before=>ContentReferencePolicy.Prepare(cmd,before)).Status);
        Assert.Throws<IOException>(()=>f.Journal.PreviewIndexRecovery(p.Plan.Request.OperationId,0));
        Assert.Equal(replacement.Target.Locator,Assert.Single(f.State.ContentCatalog.Items).Target.Locator);
        Assert.True(File.Exists(p.Files[0].Destination));
    }
    [Fact] public void DeletedOutgoingCardIsNotRecreatedAndStorageMembershipIsRemovedOnlyAfterConfirmation()
    {
        using var f=new F();var incoming=f.Stage(f.File("out.txt"));f.Executor.Execute(incoming.Plan.Request.OperationId);
        var s=f.State;var item=Assert.Single(s.ContentCatalog.Items);var card=Assert.Single(s.ContentCatalog.PosterItems);
        var p=LocalTransferPreview.Prepare(s,f.Main,ContentDropRoute.ToDesktop,[(item,Path.Combine(f.Source,"back.txt"),card.Id)],isolatedDestination:true);
        f.Journal.Begin(p.Plan,p.Files,p.Plan.Fingerprint);
        Assert.Throws<TransferInterruptionException>(()=>f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>
        {if(at==TransferExecutionCheckpoint.AfterMove)throw new TransferInterruptionException("fault");}));
        f.Journal.Mark(p.Plan.Request.OperationId,0,DurableTransferPhase.Review);
        s=f.State;var detach=new ContentDetachCommand("detach",StateCommandSource.ControlCenter,s.Revision,
            StateReadSet.ForPoster(f.Main,s.Catalog.Posters.Single().EntityVersion) with {ContentVersion=s.ContentVersion},f.Main,[card.LayoutItemId],true);
        Assert.Equal(StateCommandStatus.Committed,f.Repository.Execute(new("detach","detach",s.Revision,detach.ReadSet),before=>ContentDetachPolicy.Prepare(detach,before)).Status);
        var t=f.Journal.PreviewIndexRecovery(p.Plan.Request.OperationId,0);Assert.False(t.IsIncoming);
        f.Journal.ConfirmIndexRecovery(t,t.Fingerprint);Assert.Empty(f.State.ContentCatalog.PosterItems);
        Assert.DoesNotContain(f.State.ContentCatalog.Memberships,m=>m.CollectionId==f.Collection.Id);
        Assert.Equal(p.Files[0].Destination,Assert.Single(f.State.ContentCatalog.Items).Target.Locator);
    }
    [Fact] public void UntouchedSiblingCannotBeSilentlyRebasedByRecovery()
    {
        using var f=new F();var p=f.Stage(f.File("one.txt"),f.File("two.txt"));
        Assert.Throws<TransferInterruptionException>(()=>f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>
        {if(at==TransferExecutionCheckpoint.AfterMove)throw new TransferInterruptionException("fault");}));
        f.Journal.Mark(p.Plan.Request.OperationId,0,DurableTransferPhase.Review);
        string reads=(string)f.Sql("SELECT reads_json FROM content_transfers")!;
        var t=f.Journal.PreviewIndexRecovery(p.Plan.Request.OperationId,0);f.Journal.ConfirmIndexRecovery(t,t.Fingerprint);
        Assert.Equal(reads,f.Sql("SELECT reads_json FROM content_transfers"));
        Assert.Equal(DurableTransferPhase.Intent,f.Journal.Read()[1].Phase);Assert.True(File.Exists(p.Files[1].Source));
    }

    [Fact] public void RecoveryRetainsUnrelatedCardsAndTheirLayouts()
    {
        using var f=new F();string source=f.File("shared.txt");f.AddReference(LocalContentReferenceProbe.Inspect(source).Item!);
        string other=AddPoster(f);var s=f.State;var item=Assert.Single(s.ContentCatalog.Items);
        var cmd=new ContentReferenceCommand("share",StateCommandSource.ControlCenter,s.Revision,
            StateReadSet.ForPoster(other,s.Catalog.Posters.Single(x=>x.Poster.PosterId==other).EntityVersion) with {ContentVersion=s.ContentVersion},
            other,[new(LocalContentReferenceProbe.Inspect(f.File("other.txt")).Item!,ContentReferenceChoice.CreateIndependent)],true);
        Assert.Equal(StateCommandStatus.Committed,f.Repository.Execute(new("share","share",s.Revision,cmd.ReadSet),before=>ContentReferencePolicy.Prepare(cmd,before)).Status);
        var cards=f.State.ContentCatalog.PosterItems.ToArray();var layouts=f.State.Catalog.Posters.ToDictionary(x=>x.Poster.PosterId,x=>x.Poster.ContentLayoutJson);
        var p=f.Stage(source);
        Assert.Throws<TransferInterruptionException>(()=>f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>
        {if(at==TransferExecutionCheckpoint.AfterMove)throw new TransferInterruptionException("fault");}));
        f.Journal.Mark(p.Plan.Request.OperationId,0,DurableTransferPhase.Review);
        var ticket=f.Journal.PreviewIndexRecovery(p.Plan.Request.OperationId,0);Assert.Single(ticket.AffectedPosterIds);
        f.Journal.ConfirmIndexRecovery(ticket,ticket.Fingerprint);
        Assert.Equal(cards,f.State.ContentCatalog.PosterItems);
        foreach(var poster in f.State.Catalog.Posters)Assert.Equal(layouts[poster.Poster.PosterId],poster.Poster.ContentLayoutJson);
        Assert.Equal(p.Files[0].Destination,f.State.ContentCatalog.Items.Single(i=>i.Id==item.Id).Target.Locator);
    }

    [Fact] public void DeletingOutgoingPosterDoesNotPreventLocationRepairOrRecreatePoster()
    {
        using var f=new F();var input=f.Stage(f.File("out-deleted.txt"));f.Executor.Execute(input.Plan.Request.OperationId);
        var s=f.State;var p=LocalTransferPreview.Prepare(s,f.Main,ContentDropRoute.ToDesktop,
            [(Assert.Single(s.ContentCatalog.Items),Path.Combine(f.Source,"outside.txt"),Assert.Single(s.ContentCatalog.PosterItems).Id)],isolatedDestination:true);
        f.Journal.Begin(p.Plan,p.Files,p.Plan.Fingerprint);
        Assert.Throws<TransferInterruptionException>(()=>f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>
        {if(at==TransferExecutionCheckpoint.AfterMove)throw new TransferInterruptionException("fault");}));
        f.Journal.Mark(p.Plan.Request.OperationId,0,DurableTransferPhase.Review);f.Sql("DELETE FROM posters");
        var ticket=f.Journal.PreviewIndexRecovery(p.Plan.Request.OperationId,0);f.Journal.ConfirmIndexRecovery(ticket,ticket.Fingerprint);
        Assert.Empty(f.State.Catalog.Posters);Assert.Empty(f.State.ContentCatalog.PosterItems);
        Assert.Equal(p.Files[0].Destination,Assert.Single(f.State.ContentCatalog.Items).Target.Locator);
    }

    [Theory] [InlineData("locked")] [InlineData("hidden")] [InlineData("missing-ownership")]
    public void UnavailableDestinationNeverConsumesPhysicalResult(string condition)
    {
        using var f=new F();var p=Interrupt(f);
        if(condition=="missing-ownership")File.Move(Path.Combine(f.Root.Locator,".desktopup-storage-owner.json"),Path.Combine(f.Root.Locator,"owner.kept"));
        else f.Sql(condition=="locked"?"UPDATE posters SET is_locked=1":"UPDATE posters SET is_visible=0");
        Assert.ThrowsAny<Exception>(()=>f.Journal.PreviewIndexRecovery(p.Plan.Request.OperationId,0));
        Assert.Equal(DurableTransferPhase.Review,Assert.Single(f.Journal.Read()).Phase);Assert.True(File.Exists(p.Files[0].Destination));
    }

    [Fact] public void PreviewAloneAndExecutorLeaseNeverChangePersistentState()
    {
        using var f=new F();var p=Interrupt(f);long revision=f.State.Revision;var before=Assert.Single(f.Journal.RecoveryEntries()).Witness;
        var ticket=f.Journal.PreviewIndexRecovery(p.Plan.Request.OperationId,0);
        Assert.Equal(revision,f.State.Revision);Assert.Equal(before,Assert.Single(f.Journal.RecoveryEntries()).Witness);
        using var lease=new FileStream(Path.Combine(f.Paths.State,"transfer-executor.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        Assert.Throws<IOException>(()=>f.Journal.ConfirmIndexRecovery(ticket,ticket.Fingerprint));
        Assert.Equal(revision,f.State.Revision);Assert.Empty(f.State.ContentCatalog.PosterItems);
    }

    [Theory] [InlineData(true)] [InlineData(false)] [Trait("Category","LocalMultiVolume")]
    public void CrossVolumeRecoveryRequiresOriginalGoneAndDurableCopyProof(bool stopBeforeSourceRemoval)
    {
        using var f=new F();var temp=Directory.CreateTempSubdirectory("DesktopUp.Recovery.Index.");
        try
        {
            string source=Path.Combine(temp.FullName,"cross.txt");File.WriteAllText(source,"owned-cross-volume");var p=f.Stage(source);
            Assert.False(p.Files[0].SameVolume);
            var boundary=stopBeforeSourceRemoval?TransferExecutionCheckpoint.CopyRecorded:TransferExecutionCheckpoint.AfterMove;
            Assert.Throws<TransferInterruptionException>(()=>f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>
            {if(at==boundary)throw new TransferInterruptionException("fault");}));
            if(stopBeforeSourceRemoval)
            {
                Assert.Throws<IOException>(()=>f.Journal.PreviewIndexRecovery(p.Plan.Request.OperationId,0));
                Assert.Equal("owned-cross-volume",File.ReadAllText(source));
            }
            else
            {
                var t=f.Journal.PreviewIndexRecovery(p.Plan.Request.OperationId,0);f.Journal.ConfirmIndexRecovery(t,t.Fingerprint);
                Assert.Equal(DurableTransferPhase.IndexCommitted,Assert.Single(f.Journal.Read()).Phase);
            }
            Assert.Equal("owned-cross-volume",File.ReadAllText(p.Files[0].Destination));
        }
        finally {temp.Delete(true);} // Unique owned fixture only.
    }

    [Fact] [Trait("Category","LocalMultiVolume")]
    public void CrossVolumeBytesAloneWithoutReceiptDoNotProveDestinationIdentity()
    {
        using var f=new F();var temp=Directory.CreateTempSubdirectory("DesktopUp.Recovery.Proof.");
        try
        {
            string source=Path.Combine(temp.FullName,"copy.txt");File.WriteAllText(source,"owned-copy");var p=f.Stage(source);Assert.False(p.Files[0].SameVolume);
            Assert.Throws<TransferInterruptionException>(()=>f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>
            {if(at==TransferExecutionCheckpoint.AfterCopy)throw new TransferInterruptionException("fault");}));
            File.Move(source,source+".keep"); // Simulated external move of our fixture, not a user file.
            Assert.Throws<IOException>(()=>f.Journal.PreviewIndexRecovery(p.Plan.Request.OperationId,0));
            Assert.Empty(f.State.ContentCatalog.PosterItems);Assert.Equal("owned-copy",File.ReadAllText(p.Files[0].Destination));
        }
        finally {temp.Delete(true);}
    }

    [Fact] public void DirectoryGrownAfterPreviewIsPreservedAndNotIndexed()
    {
        using var f=new F();string source=Path.Combine(f.Source,"directory");Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source,"first.txt"),"first");var p=f.Stage(source);
        Assert.Throws<TransferInterruptionException>(()=>f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>
        {if(at==TransferExecutionCheckpoint.AfterMove)throw new TransferInterruptionException("fault");}));
        var t=f.Journal.PreviewIndexRecovery(p.Plan.Request.OperationId,0);
        File.WriteAllText(Path.Combine(p.Files[0].Destination,"later.txt"),"later");
        Assert.Throws<IOException>(()=>f.Journal.ConfirmIndexRecovery(t,t.Fingerprint));
        Assert.Equal("later",File.ReadAllText(Path.Combine(p.Files[0].Destination,"later.txt")));Assert.Empty(f.State.ContentCatalog.PosterItems);
    }
}
