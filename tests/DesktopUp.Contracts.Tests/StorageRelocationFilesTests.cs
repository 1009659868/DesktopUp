using DesktopUp.Application;
using DesktopUp.Infrastructure;
using F=DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed partial class StorageRelocationFilesTests
{
    private static string Seed(F f)
    {
        var staged=f.Stage(f.File("real.txt"));f.Executor.Execute(staged.Plan.Request.OperationId);
        string root=f.Collection.StorageLocator!;Directory.CreateDirectory(Path.Combine(root,"folder","empty"));
        File.WriteAllBytes(Path.Combine(root,"folder","shortcut.lnk"),Enumerable.Range(0,256).Select(x=>(byte)x).ToArray());
        File.WriteAllBytes(Path.Combine(root,"large.bin"),Enumerable.Repeat((byte)71,512*1024).ToArray());
        return Path.Combine(root,"real.txt");
    }
    private static StorageRelocationPlan Preview(F f,string? target=null)
    {target??=Path.Combine(f.DirectoryRoot,"destination-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(target);return new SqliteStorageRelocationJournal(f.Store).Preview(target);}
    private static string Target(F f,StorageRelocationPlan p,string name)=>Path.Combine(p.Request.Destination.Locator,"collection-"+f.Collection.Id.ToString("N"),name);

    [Fact] public void RealCopiesRetainSourcesBeforeOneCommitThenCleanOnlyOldPayload()
    {
        using var f=new F();string source=Seed(f);var before=f.State;var p=Preview(f);var files=new StorageRelocationFiles(f.Store);var journal=new SqliteStorageRelocationJournal(f.Store);
        files.Prepare(p,p.Fingerprint);Assert.True(File.Exists(source));Assert.Equal(before.Revision,f.State.Revision);
        Assert.Equal(File.ReadAllBytes(source),File.ReadAllBytes(Target(f,p,"real.txt")));
        Assert.False(File.Exists(Target(f,p,".desktopup-collection-owner.json")));
        Assert.True(Directory.Exists(Target(f,p,"folder/empty")));
        journal.RecordTargets(p.Request.OperationId,files);journal.Commit(p.Request.OperationId,files);
        Assert.True(File.Exists(source));var result=files.Recover(p.Request.OperationId);
        Assert.True(result.Committed,result.Reason);Assert.True(result.Cleaned,result.Reason);Assert.False(File.Exists(source));
        Assert.True(File.Exists(Path.Combine(f.Collection.StorageLocator!,".desktopup-collection-owner.json")));
        Assert.Equal(before.Revision+1,f.State.Revision);Assert.Equal(before.ContentCatalog.PosterItems,f.State.ContentCatalog.PosterItems);
        Assert.Equal("owned-content",File.ReadAllText(f.State.ContentCatalog.Items.Single().Target.Locator));
        Assert.Equal(StorageRelocationPhase.Completed,journal.Get(p.Request.OperationId).Phase);
        Assert.True(files.Recover(p.Request.OperationId).Cleaned);
    }
    [Fact] public void NativeContentLeasesPreventWritesAndReplacementThroughIndexCommit()
    {
        using var f=new F();string source=Seed(f);var p=Preview(f);var files=new StorageRelocationFiles(f.Store);files.Prepare(p,p.Fingerprint);
        using(var proof=files.Acquire(p))
        {
            Assert.Throws<IOException>(()=>File.WriteAllText(source,"changed"));
            Assert.Throws<IOException>(()=>File.Move(Target(f,p,"real.txt"),Target(f,p,"renamed.txt")));
        }
        var result=files.Recover(p.Request.OperationId);Assert.False(result.Committed);Assert.True(result.Cleaned,result.Reason);Assert.True(File.Exists(source));
    }
    [Theory] [InlineData((int)StorageCopyCheckpoint.ManifestWritten)] [InlineData((int)StorageCopyCheckpoint.ObjectCreated)]
    [InlineData((int)StorageCopyCheckpoint.CopyProgress)] [InlineData((int)StorageCopyCheckpoint.FileCopied)] [InlineData((int)StorageCopyCheckpoint.Prepared)] [InlineData((int)StorageCopyCheckpoint.Verified)]
    public void CancellationAutomaticallyRemovesOnlyProvenTargetsAndAllowsFreshRetry(int point)
    {
        using var f=new F();string source=Seed(f);long revision=f.State.Revision;var p=Preview(f);using var cancel=new CancellationTokenSource();
        var files=new StorageRelocationFiles(f.Store);var result=files.Execute(p,p.Fingerprint,cancel.Token,at=>{if((int)at==point)cancel.Cancel();});
        Assert.False(result.Committed);Assert.True(result.Cleaned,result.Reason);Assert.True(File.Exists(source));Assert.Equal(revision,f.State.Revision);
        Assert.Empty(Directory.EnumerateFileSystemEntries(p.Request.Destination.Locator));
        var fresh=Preview(f,p.Request.Destination.Locator);var retry=files.Execute(fresh,fresh.Fingerprint);Assert.True(retry.Committed,retry.Reason);Assert.True(retry.Cleaned,retry.Reason);
    }
    [Theory] [InlineData("source")] [InlineData("target")] [InlineData("new-source")] [InlineData("new-target")]
    public void ExternalChangesBeforeCommitCannotBeHiddenByOldProof(string change)
    {
        using var f=new F();string source=Seed(f);var p=Preview(f);long revision=f.State.Revision;
        var result=new StorageRelocationFiles(f.Store).Execute(p,p.Fingerprint,checkpoint:at=>
        {
            if(at!=StorageCopyCheckpoint.Verified)return;
            if(change=="source")File.WriteAllText(source,"external source");
            if(change=="target")File.WriteAllText(Target(f,p,"real.txt"),"external target");
            if(change=="new-source")File.WriteAllText(Path.Combine(f.Collection.StorageLocator!,"extra.txt"),"external");
            if(change=="new-target")File.WriteAllText(Target(f,p,"extra.txt"),"external");
        });
        Assert.False(result.Committed);Assert.Equal(revision,f.State.Revision);Assert.True(File.Exists(source));
        if(change=="source")Assert.Equal("external source",File.ReadAllText(source));
        if(change=="target")Assert.Equal("external target",File.ReadAllText(Target(f,p,"real.txt")));
        if(change=="new-target")Assert.Equal("external",File.ReadAllText(Target(f,p,"extra.txt")));
    }
    [Theory] [InlineData((int)StorageRelocationCheckpoint.IndexWritten)] [InlineData((int)StorageRelocationCheckpoint.ReceiptWritten)]
    public void IndexFailureKeepsOriginalFilesAndRollsBackEveryLocator(int point)
    {
        using var f=new F();string source=Seed(f);var before=StateRequestFingerprint.Compute(f.State);var p=Preview(f);
        var result=new StorageRelocationFiles(f.Store).Execute(p,p.Fingerprint,transactionCheckpoint:at=>{if((int)at==point)throw new IOException("isolated SQL failure");});
        Assert.False(result.Committed);Assert.True(result.Cleaned,result.Reason);Assert.Equal(before,StateRequestFingerprint.Compute(f.State));Assert.True(File.Exists(source));
    }
    [Theory] [InlineData("source")] [InlineData("target")] [InlineData("new-source")]
    public void ChangesAfterCommitRetainOldCopiesWithoutRollingBackNewIndex(string change)
    {
        using var f=new F();string source=Seed(f);var p=Preview(f);long revision=f.State.Revision;
        var files=new StorageRelocationFiles(f.Store);var journal=new SqliteStorageRelocationJournal(f.Store);
        files.Prepare(p,p.Fingerprint);journal.RecordTargets(p.Request.OperationId,files);journal.Commit(p.Request.OperationId,files);
        // Commit holds its proof leases through its final checkpoint. External editing belongs
        // AFTER Commit returns, not inside a callback that still owns the no-write handles.
        if(change=="source")File.WriteAllText(source,"external source");
        if(change=="target")File.WriteAllText(Target(f,p,"real.txt"),"external target");
        if(change=="new-source")File.WriteAllText(Path.Combine(f.Collection.StorageLocator!,"added.txt"),"external");
        var result=files.Recover(p.Request.OperationId);
        Assert.True(result.Committed,result.Reason);Assert.False(result.Cleaned);Assert.NotEmpty(result.RetainedPaths);Assert.Equal(revision+1,f.State.Revision);
        Assert.StartsWith(p.Request.Destination.Locator,f.State.ContentCatalog.Items.Single().Target.Locator);
        if(change!="new-source")Assert.True(File.Exists(source));
    }
    [Theory] [InlineData("readonly")] [InlineData("ads")]
    public void UnsupportedFileFeaturesAreRejectedWithoutClaimingDestination(string feature)
    {
        using var f=new F();string source=Seed(f);
        if(feature=="readonly")File.SetAttributes(source,FileAttributes.ReadOnly);else File.WriteAllText(source+":extra","alternate");
        try
        {
            var p=Preview(f);var result=new StorageRelocationFiles(f.Store).Execute(p,p.Fingerprint);
            Assert.False(result.Committed);Assert.True(File.Exists(source));Assert.Empty(Directory.EnumerateFileSystemEntries(p.Request.Destination.Locator));
        }
        finally{if(feature=="readonly")File.SetAttributes(source,FileAttributes.Normal);}
    }
}
