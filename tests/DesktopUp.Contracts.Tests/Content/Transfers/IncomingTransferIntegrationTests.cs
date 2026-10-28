using DesktopUp.Windows;
using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed class IncomingTransferIntegrationTests
{
    [Theory] [InlineData(ContentFlowAxis.Vertical)] [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void EdgeDropKeepsNewCardInsideCrossAxis(ContentFlowAxis axis)
    {
        var d=ContentLayoutSamples.Equal("A",0) with {FlowAxis=axis};var viewport=new ContentLayoutViewport(300,240);
        var snapshot=ContentLayoutSolver.Resolve(d,viewport);
        var intent=IncomingContentPlacement.From(snapshot,viewport,new(IncomingDropArea.Content,299,239))!;
        Assert.InRange(axis==ContentFlowAxis.Vertical?intent.Column!.Value:intent.Row!.Value,0,snapshot.CrossAxisCells-4);
    }
    private static ContentLayoutDocument Layout(ManagedTransferPersistenceTests.Fixture f)
    {
        Assert.True(ContentLayoutPolicy.TryRead(f.State.Catalog.Posters.Single().Poster.ContentLayoutJson,f.Main,out var d,out _));return d!;
    }
    private static LocalTransferPreview Prepare(ManagedTransferPersistenceTests.Fixture f,string source,IncomingContentPlacement? landing=null)
    {
        var p=DirectTransferPreparation.Incoming(f.State,f.Main,[source],new(f.Root,f.Collection),landing);
        f.Journal.Begin(p.Plan,p.Files,p.Plan.Fingerprint,f.Collection.Id);return p;
    }
    [Fact] public void LandingAndMembershipCommitOnceAndDuplicateExecuteDoesNotRepeat()
    {
        using var f=new ManagedTransferPersistenceTests.Fixture();
        f.AddReference(LocalContentReferenceProbe.Inspect(f.File("existing.txt")).Item!);
        long version=Layout(f).DocumentVersion,revision=f.State.Revision;
        var p=Prepare(f,f.File("incoming.txt"),new(new(300,400),ReadOrder:0,Column:0,Row:0));
        var result=new AutomaticTransferCompletion(f.Journal).Execute(p.Plan.Request.OperationId);
        Assert.Equal(TransferFinishKind.Completed,Assert.Single(result).Kind);
        Assert.Equal(revision+1,f.State.Revision);Assert.Equal(version+1,Layout(f).DocumentVersion);
        var item=f.State.ContentCatalog.Items.Single(x=>x.DisplayName=="incoming.txt");
        var member=f.State.ContentCatalog.Memberships.Single(m=>m.ContentItemId==item.Id && m.Origin!=ContentMembershipOrigin.Source);
        var card=f.State.ContentCatalog.PosterItems.Single(x=>x.MembershipId==member.Id);
        Assert.Equal(1,Layout(f).Placements.Single(x=>x.ItemId==card.LayoutItemId).PinnedIndex);
        new AutomaticTransferCompletion(f.Journal).Execute(p.Plan.Request.OperationId);
        Assert.Equal(revision+1,f.State.Revision);
    }
    [Fact] public void ManualLandingUsesLocalDisplacementAndDoesNotChangeMode()
    {
        using var f=new ManagedTransferPersistenceTests.Fixture();
        f.AddReference(LocalContentReferenceProbe.Inspect(f.File("existing.txt")).Item!);
        var before=f.State;var poster=before.Catalog.Posters.Single();
        var edited=ContentLayoutEditing.Apply(Layout(f),new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Manual},new(300,400));
        Assert.True(edited.Accepted,edited.Error);
        Assert.Equal(StateCommandStatus.Committed,f.Repository.Execute(new("manual","manual",before.Revision,
            StateReadSet.ForPoster(f.Main,poster.EntityVersion)),_=>new(){Updates=[poster.Poster with {ContentLayoutJson=ContentLayoutPolicy.Serialize(edited.Document)}]}).Status);
        var oldId=Assert.Single(Layout(f).Placements).ItemId;
        var p=Prepare(f,f.File("incoming.txt"),new(new(300,400),ReadOrder:0,Column:0,Row:0));
        Assert.Equal(TransferFinishKind.Completed,Assert.Single(new AutomaticTransferCompletion(f.Journal).Execute(p.Plan.Request.OperationId)).Kind);
        var d=Layout(f);Assert.Equal(ContentLayoutMode.Manual,d.Mode);
        Assert.Equal(0,d.Placements.Single(x=>x.ItemId!=oldId).Row);
        Assert.True(d.Placements.Single(x=>x.ItemId==oldId).Row>=4);
    }
    [Theory] [InlineData(null)] [InlineData(TransferExecutionCheckpoint.CopyProgress)]
    [InlineData(TransferExecutionCheckpoint.CopyRecorded)] [InlineData(TransferExecutionCheckpoint.BeforeIndex)]
    [InlineData(TransferExecutionCheckpoint.IndexCommitted)]
    public void CrossVolumeTreeKeepsOriginalUntilCommitAndRestartSettlesWithoutRecopy(TransferExecutionCheckpoint? crash)
    {
        using var f=new ManagedTransferPersistenceTests.Fixture();
        var external=Directory.CreateTempSubdirectory("desktopup-d3-");
        try
        {
            Assert.NotEqual(Path.GetPathRoot(f.Source),Path.GetPathRoot(external.FullName));
            string folder=Path.Combine(external.FullName,"tree");Directory.CreateDirectory(Path.Combine(folder,"empty"));
            File.WriteAllText(Path.Combine(folder,"one.txt"),new string('x',270000));
            var p=Prepare(f,folder);long revision=f.State.Revision;
            void Check(TransferExecutionCheckpoint at,int _)
            {
                if(at==TransferExecutionCheckpoint.BeforeIndex){Assert.True(Directory.Exists(folder));Assert.Empty(f.State.ContentCatalog.PosterItems);}
                if(at==crash)throw new TransferInterruptionException("loss");
            }
            if(crash is null)new AutomaticTransferCompletion(f.Journal).Execute(p.Plan.Request.OperationId,checkpoint:Check);
            else Assert.Throws<TransferInterruptionException>(()=>new AutomaticTransferCompletion(f.Journal).Execute(p.Plan.Request.OperationId,checkpoint:Check));
            using var reopened=new SqliteStateStore(f.Paths);Assert.True(reopened.Initialize().Succeeded);
            var result=Assert.Single(new AutomaticTransferCompletion(new(reopened)).Settle());
            bool committed=crash is null or TransferExecutionCheckpoint.IndexCommitted;
            Assert.Equal(committed?TransferFinishKind.Completed:TransferFinishKind.NotMoved,result.Kind);
            Assert.Equal(!committed,Directory.Exists(folder));Assert.Equal(committed,Directory.Exists(p.Files[0].Destination));
            Assert.Equal(revision+(committed?1:0),f.State.Revision);
            if(committed){Assert.True(Directory.Exists(Path.Combine(p.Files[0].Destination,"empty")));Assert.Equal(270000,new FileInfo(Path.Combine(p.Files[0].Destination,"one.txt")).Length);}
        }
        finally{external.Delete(true);} // Unique fixture-created temp root only.
    }
    [Fact] public void CrossVolumeIndexFailureAutomaticallyRemovesOwnedCopyAndAllowsFreshRetry()
    {
        using var f=new ManagedTransferPersistenceTests.Fixture();var external=Directory.CreateTempSubdirectory("desktopup-d3-");
        try
        {
            string source=Path.Combine(external.FullName,"item.txt");File.WriteAllText(source,"keep");
            var p=Prepare(f,source);
            f.Sql("CREATE TRIGGER ingress_fail BEFORE UPDATE ON content_head BEGIN SELECT RAISE(ABORT,'fault'); END;");
            Assert.Equal(TransferFinishKind.NotMoved,Assert.Single(new AutomaticTransferCompletion(f.Journal).Execute(p.Plan.Request.OperationId)).Kind);
            Assert.True(File.Exists(source));Assert.False(File.Exists(p.Files[0].Destination));Assert.Empty(f.State.ContentCatalog.PosterItems);
            f.Sql("DROP TRIGGER ingress_fail;");var retry=Prepare(f,source);
            Assert.Equal(TransferFinishKind.Completed,Assert.Single(new AutomaticTransferCompletion(f.Journal).Execute(retry.Plan.Request.OperationId)).Kind);
        }
        finally{external.Delete(true);}
    }
}
