using System.Security.Cryptography;
using DesktopUp.Infrastructure;
using F=DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class ManagedTransferCopyProgressTests
{
    [Theory] [InlineData(false)] [InlineData(true)] [Trait("Category","LocalMultiVolume")]
    public void RealCopyProgressCancellationOrInterruptionNeverDisposesOriginalOrPublishesSuccess(bool interrupt)
    {
        using var f=new F();var temp=Directory.CreateTempSubdirectory("DesktopUp.CopyProgress.");
        try
        {
            string source=Path.Combine(temp.FullName,"owned-large.bin");
            byte[] bytes=new byte[32*1024*1024];new Random(710).NextBytes(bytes);File.WriteAllBytes(source,bytes);
            var p=f.Stage(source);Assert.False(p.Files[0].SameVolume);
            using var cts=new CancellationTokenSource();int progress=0;
            void Observe(TransferExecutionCheckpoint at,int index)
            {
                if(at!=TransferExecutionCheckpoint.CopyProgress)return;
                progress++;
                if(interrupt)throw new TransferInterruptionException("actual native partial-copy boundary");
                cts.Cancel();
            }
            if(interrupt)Assert.Throws<TransferInterruptionException>(()=>f.Executor.Execute(p.Plan.Request.OperationId,cts.Token,Observe));
            else f.Executor.Execute(p.Plan.Request.OperationId,cts.Token,Observe);
            Assert.True(progress>0,"no intermediate callback");
            Assert.True(File.Exists(source),"source missing");
            bool hasTarget=LocalTransferFiles.Exists(p.Files[0].Destination);
            Assert.Equal(SHA256.HashData(bytes),SHA256.HashData(File.ReadAllBytes(source)));
            // Windows may preallocate the full destination length. A partial result is proven
            // by a non-final native progress callback + content mismatch, not by length alone.
            string? targetHash=hasTarget?Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p.Files[0].Destination))):null;
            if(hasTarget)Assert.NotEqual(Convert.ToHexString(SHA256.HashData(bytes)),targetHash);
            var row=Assert.Single(f.Journal.Read());Assert.Null(row.Receipt);Assert.Empty(f.State.ContentCatalog.Items);
            Assert.Equal(interrupt?DurableTransferPhase.Executing:DurableTransferPhase.Review,row.Phase);
            using var restarted=new SqliteStateStore(f.Paths);Assert.True(restarted.Initialize().Succeeded);
            var journal=new SqliteTransferJournal(restarted);Assert.Equal(DurableTransferPhase.Review,Assert.Single(new LocalTransferExecutor(journal).Recover()).Phase);
            if(hasTarget)Assert.Throws<IOException>(()=>journal.PreviewRetry(p.Plan.Request.OperationId,0));
            else Assert.NotEqual(p.Plan.Request.OperationId,journal.PreviewRetry(p.Plan.Request.OperationId,0).Preview.Plan.Request.OperationId);
            var stop=journal.PreviewStop(p.Plan.Request.OperationId,0);journal.ConfirmStop(stop,stop.Fingerprint);
            Assert.True(File.Exists(source));Assert.Equal(hasTarget,LocalTransferFiles.Exists(p.Files[0].Destination));Assert.Empty(f.State.ContentCatalog.Items);
            if(hasTarget)Assert.Equal(targetHash,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p.Files[0].Destination))));
        }
        finally{temp.Delete(true);} // Only this test-created unique directory, never desktop files.
    }
    [Fact] [Trait("Category","LocalMultiVolume")]
    public void RealCrossVolumeCopyWithProgressStillVerifiesBeforeDisposingSource()
    {
        using var f=new F();var temp=Directory.CreateTempSubdirectory("DesktopUp.CopyComplete.");
        try
        {
            string source=Path.Combine(temp.FullName,"owned.bin");byte[] bytes=new byte[4*1024*1024];new Random(42).NextBytes(bytes);File.WriteAllBytes(source,bytes);
            var p=f.Stage(source);Assert.False(p.Files[0].SameVolume);bool copied=false;
            var row=Assert.Single(f.Executor.Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>
            {if(at==TransferExecutionCheckpoint.CopyRecorded){copied=true;Assert.True(File.Exists(source));Assert.NotNull(Assert.Single(f.Journal.Read()).Receipt);}}));
            Assert.True(copied);Assert.Equal(DurableTransferPhase.IndexCommitted,row.Phase);Assert.False(File.Exists(source));Assert.Equal(bytes,File.ReadAllBytes(p.Files[0].Destination));
        }
        finally{temp.Delete(true);}
    }
}
