using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Fixture=DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;
public sealed class OutgoingTransferIntegrationTests
{
    [Theory] [InlineData(null)] [InlineData(TransferExecutionCheckpoint.CopyProgress)]
    [InlineData(TransferExecutionCheckpoint.BeforeIndex)] [InlineData(TransferExecutionCheckpoint.IndexCommitted)]
    public void CrossVolumeOutgoingTreeCommitsBeforeSourceCleanupAndRestartsSafely(TransferExecutionCheckpoint? crash)
    {
        using var f=new Fixture();var target=Directory.CreateTempSubdirectory("DesktopUp-D4-owned-");
        try
        {
            Assert.NotEqual(Path.GetPathRoot(target.FullName),Path.GetPathRoot(f.Source));
            string source=Path.Combine(f.Source,"tree");Directory.CreateDirectory(Path.Combine(source,"empty"));
            File.WriteAllText(Path.Combine(source,"child.txt"),new string('t',270000));
            var incoming=f.Stage(source);f.Executor.Execute(incoming.Plan.Request.OperationId);
            var card=Assert.Single(f.State.ContentCatalog.PosterItems);var item=Assert.Single(f.State.ContentCatalog.Items);
            string destination=Path.Combine(target.FullName,"tree");
            var p=LocalTransferPreview.Prepare(f.State,f.Main,ContentDropRoute.ToDesktop,[(item,destination,card.Id)],isolatedDestination:true,keepSourceUntilCommit:true);
            f.Journal.Begin(p.Plan,p.Files,p.Plan.Fingerprint);
            void Check(TransferExecutionCheckpoint at,int _)
            {
                if(at==TransferExecutionCheckpoint.BeforeIndex){Assert.True(Directory.Exists(item.Target.Locator));Assert.Single(f.State.ContentCatalog.PosterItems);}
                if(at==crash)throw new TransferInterruptionException("isolated loss");
            }
            if(crash is null)new AutomaticTransferCompletion(f.Journal).Execute(p.Plan.Request.OperationId,checkpoint:Check);
            else Assert.Throws<TransferInterruptionException>(()=>new AutomaticTransferCompletion(f.Journal).Execute(p.Plan.Request.OperationId,checkpoint:Check));
            using var reopened=new SqliteStateStore(f.Paths);Assert.True(reopened.Initialize().Succeeded);
            var finish=Assert.Single(new AutomaticTransferCompletion(new(reopened)).Settle(p.Plan.Request.OperationId));
            bool committed=crash is null or TransferExecutionCheckpoint.IndexCommitted;
            Assert.Equal(committed?TransferFinishKind.Completed:TransferFinishKind.NotMoved,finish.Kind);
            Assert.Equal(!committed,Directory.Exists(item.Target.Locator));Assert.Equal(committed,Directory.Exists(destination));
            Assert.Equal(committed?0:1,f.State.ContentCatalog.PosterItems.Count);
            if(committed){Assert.Equal(270000,new FileInfo(Path.Combine(destination,"child.txt")).Length);Assert.True(Directory.Exists(Path.Combine(destination,"empty")));}
        }
        finally{target.Delete(true);} // Only this uniquely-created fixture root.
    }
    [Fact] public void CrossVolumeIndexFailureKeepsCardAndFileThenFreshAttemptSucceeds()
    {
        using var f=new Fixture();var target=Directory.CreateTempSubdirectory("DesktopUp-D4-owned-");
        try
        {
            var incoming=f.Stage(f.File("one.txt"));f.Executor.Execute(incoming.Plan.Request.OperationId);
            var item=Assert.Single(f.State.ContentCatalog.Items);var card=Assert.Single(f.State.ContentCatalog.PosterItems);
            string dest=Path.Combine(target.FullName,"one.txt");
            LocalTransferPreview Prepare()=>LocalTransferPreview.Prepare(f.State,f.Main,ContentDropRoute.ToDesktop,[(item,dest,card.Id)],isolatedDestination:true,keepSourceUntilCommit:true);
            var p=Prepare();f.Journal.Begin(p.Plan,p.Files,p.Plan.Fingerprint);
            f.Sql("CREATE TRIGGER egress_fail BEFORE UPDATE ON content_head BEGIN SELECT RAISE(ABORT,'fault'); END;");
            Assert.Equal(TransferFinishKind.NotMoved,Assert.Single(new AutomaticTransferCompletion(f.Journal).Execute(p.Plan.Request.OperationId)).Kind);
            Assert.True(File.Exists(item.Target.Locator));Assert.False(File.Exists(dest));Assert.Equal(card,Assert.Single(f.State.ContentCatalog.PosterItems));
            f.Sql("DROP TRIGGER egress_fail;");p=Prepare();f.Journal.Begin(p.Plan,p.Files,p.Plan.Fingerprint);
            Assert.Equal(TransferFinishKind.Completed,Assert.Single(new AutomaticTransferCompletion(f.Journal).Execute(p.Plan.Request.OperationId)).Kind);
            Assert.Empty(f.State.ContentCatalog.PosterItems);Assert.Equal("owned-content",File.ReadAllText(dest));
        }
        finally{target.Delete(true);}
    }
    [Fact] public void OutgoingSelectionRejectsOverlappingFilesBeforeAnyMove()
    {
        using var f=new Fixture();string child=f.File("child.txt");
        f.AddReference(LocalContentReferenceProbe.Inspect(f.Source).Item!);f.AddReference(LocalContentReferenceProbe.Inspect(child).Item!);
        Assert.Throws<IOException>(()=>DirectTransferPreparation.OutgoingSources(f.State,f.Main,f.State.ContentCatalog.PosterItems.Select(c=>c.LayoutItemId).ToArray()));
        Assert.Empty(f.Journal.Read());Assert.True(File.Exists(child));
    }
}
