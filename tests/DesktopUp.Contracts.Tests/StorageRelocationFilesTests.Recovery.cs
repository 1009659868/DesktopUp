using System.Diagnostics;
using DesktopUp.Application;
using DesktopUp.Infrastructure;
using F=DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed partial class StorageRelocationFilesTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public void CancellationAfterCommitOrDuringCleanupKeepsNewIndexAndRemainingOldCopies(bool duringCleanup)
    {
        using var f=new F();Seed(f);var p=Preview(f);using var cancel=new CancellationTokenSource();int removed=0;
        var result=Execute(f.Store,p,p.Fingerprint,cancel.Token,
            checkpoint:at=>{if(at==StorageCopyCheckpoint.SourceDeleted){removed++;if(duringCleanup)cancel.Cancel();}},
            transactionCheckpoint:at=>{if(at==StorageRelocationCheckpoint.Committed&&!duringCleanup)cancel.Cancel();});
        Assert.True(result.Committed,result.Reason);Assert.False(result.Cleaned);Assert.NotEmpty(result.RetainedPaths);
        Assert.Equal(duringCleanup?1:0,removed);Assert.Equal("owned-content",File.ReadAllText(Target(f,p,"real.txt")));
        Assert.Equal(p.Request.Destination.Id,new SqliteStorageRelocationJournal(f.Store).ReadLocation().Selection.ActiveRootId);
    }
    [Fact] public void MissingReferencedObjectCannotBePublishedAsAVerifiedMigration()
    {
        using var f=new F();string source=Seed(f);File.Delete(source);var p=Preview(f);long revision=f.State.Revision;
        var result=Execute(f.Store,p,p.Fingerprint);Assert.False(result.Committed);Assert.True(result.Cleaned,result.Reason);
        Assert.Equal(revision,f.State.Revision);Assert.Empty(Directory.EnumerateFileSystemEntries(p.Request.Destination.Locator));
    }
    [Fact] public void LockedSourceCannotBeCopiedAndDoesNotAcquireTheDestination()
    {
        using var f=new F();string source=Seed(f);var p=Preview(f);
        using var locked=new FileStream(source,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        Assert.Throws<IOException>(()=>Execute(f.Store,p,p.Fingerprint));
        Assert.Empty(new SqliteStorageRelocationJournal(f.Store).Read());Assert.Empty(Directory.EnumerateFileSystemEntries(p.Request.Destination.Locator));
    }
    [Fact] public void EmptyCollectionsAndMultipleRootsUseTheSameRealCopyProtocol()
    {
        using var f=new F();Seed(f);string second=Path.Combine(f.DirectoryRoot,"empty-root");Directory.CreateDirectory(second);
        var provisioner=new ManagedStorageProvisioner(f.Store);var preview=provisioner.Preview(second);var registered=provisioner.Confirm(preview,preview.Fingerprint);
        var p=Preview(f);var result=Execute(f.Store,p,p.Fingerprint);
        Assert.True(result.Committed,result.Reason);Assert.True(result.Cleaned,result.Reason);
        var locations=new SqliteTransferJournal(f.Store).StorageLocations();Assert.Equal(2,locations.Count);Assert.Single(locations.Select(x=>x.Root.Id).Distinct());
        Assert.Contains(locations,x=>x.Collection.Id==registered.Collection.Id);
    }
    [Fact] public void UnexpectedTargetChildrenNeverGetSweptOnAnOrdinaryCopyFailure()
    {
        using var f=new F();Seed(f);var p=Preview(f);string foreign=Path.Combine(p.Request.Destination.Locator,"keep.txt");
        var result=Execute(f.Store,p,p.Fingerprint,checkpoint:at=>
        {if(at==StorageCopyCheckpoint.ManifestWritten)File.WriteAllText(foreign,"external");});
        Assert.False(result.Committed);Assert.False(result.Cleaned);Assert.Equal("external",File.ReadAllText(foreign));
    }
    [Theory] [InlineData("ManifestWritten")] [InlineData("ObjectCreated")] [InlineData("CopyProgress")]
    [InlineData("Prepared")] [InlineData("TargetsRecorded")] [InlineData("IndexWritten")] [InlineData("ReceiptWritten")]
    [InlineData("Committed")] [InlineData("SourceDeleted")] [InlineData("TargetDeleted")]
    public void IndependentProcessLossReconcilesWithoutRecopyingOrLosingSource(string boundary)
    {
        using var f=new F();string source=Seed(f);long revision=f.State.Revision;
        var start=new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"content-test-helpers","DesktopUp.TransferFixture.exe")){UseShellExecute=false,CreateNoWindow=true};
        foreach(string arg in new[]{"--storage-copy-crash",f.DirectoryRoot,boundary})start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;Assert.True(process.WaitForExit(20000));Assert.Equal(76,process.ExitCode);
        using var restarted=new SqliteStateStore(f.Paths);Assert.True(restarted.Initialize().Succeeded);
        var journal=new SqliteStorageRelocationJournal(restarted);var row=Assert.Single(journal.Read());
        bool committed=boundary is "Committed" or "SourceDeleted";
        var result=new StorageRelocationFiles(restarted).Recover(row.Plan.Request.OperationId);
        Assert.Equal(committed,result.Committed);Assert.True(result.Cleaned,result.Reason);
        Assert.Equal(revision+(committed?1:0),new SqliteStateCommandRepository(restarted).Read().Revision);
        Assert.Equal(!committed,File.Exists(source));
        if(committed)Assert.Equal("owned-content",File.ReadAllText(Target(f,row.Plan,"real.txt")));
        else Assert.Empty(Directory.EnumerateFileSystemEntries(row.Plan.Request.Destination.Locator));
        Assert.True(new StorageRelocationFiles(restarted).Recover(row.Plan.Request.OperationId).Cleaned);
    }
    [Theory] [InlineData("success")] [InlineData("cancel")] [InlineData("index-failure")]
    [Trait("Category","LocalMultiVolume")]
    public void RealDifferentVolumesCopyNestedFoldersShortcutBytesAndEmptyDirectories(string mode)
    {
        using var f=new F();string source=Seed(f);
        string allowed=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DesktopUp.IsolatedTests","storage-relocation");
        string root=Path.Combine(allowed,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,".owned-test"),"D2-03");
        try
        {
            string target=Path.Combine(root,"destination");var p=Preview(f,target);
            Assert.NotEqual(LocalTransferFiles.Identity(f.Root.Locator)[..16],LocalTransferFiles.Identity(target)[..16]);
            byte[] link=File.ReadAllBytes(Path.Combine(f.Collection.StorageLocator!,"folder","shortcut.lnk"));DateTime time=File.GetLastWriteTimeUtc(source);
            using var cancel=new CancellationTokenSource();long revision=f.State.Revision;
            var result=Execute(f.Store,p,p.Fingerprint,cancel.Token,
                checkpoint:at=>{if(mode=="cancel"&&at==StorageCopyCheckpoint.CopyProgress)cancel.Cancel();},
                transactionCheckpoint:at=>{if(mode=="index-failure"&&at==StorageRelocationCheckpoint.ReceiptWritten)throw new IOException("isolated cross-volume SQL failure");});
            Assert.True(result.Cleaned,result.Reason);
            if(mode!="success")
            {
                Assert.False(result.Committed);Assert.True(File.Exists(source));Assert.Equal(revision,f.State.Revision);
                Assert.Empty(Directory.EnumerateFileSystemEntries(target));return;
            }
            Assert.True(result.Committed,result.Reason);
            Assert.Equal(link,File.ReadAllBytes(Target(f,p,"folder/shortcut.lnk")));Assert.True(Directory.Exists(Target(f,p,"folder/empty")));
            Assert.Equal(time,File.GetLastWriteTimeUtc(Target(f,p,"real.txt")));Assert.False(File.Exists(source));
        }
        finally
        {
            // Unique test directory with an exact sentinel, not a user-selected storage location.
            if(Path.GetDirectoryName(Path.GetFullPath(root))==allowed&&File.ReadAllText(Path.Combine(root,".owned-test"))=="D2-03")Directory.Delete(root,true);
        }
    }
    [Fact] public void ValidWindowsShortcutMovesItsOwnBytesWithoutChangingOrExecutingItsTarget()
    {
        using var f=new F();string target=f.File("launch-target.txt"),link=Path.Combine(f.Source,"launch.lnk");
        dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell",throwOnError:true)!)!;
        try
        {
            dynamic shortcut=shell.CreateShortcut(link);
            try{shortcut.TargetPath=target;shortcut.Description="D2-03 isolated shortcut";shortcut.Save();}
            finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);}
            byte[] bytes=File.ReadAllBytes(link);var incoming=f.Stage(link);f.Executor.Execute(incoming.Plan.Request.OperationId);
            var p=Preview(f);var result=Execute(f.Store,p,p.Fingerprint);Assert.True(result.Committed,result.Reason);Assert.True(result.Cleaned,result.Reason);
            Assert.Equal(bytes,File.ReadAllBytes(Target(f,p,"launch.lnk")));Assert.Equal("owned-content",File.ReadAllText(target));
            dynamic copied=shell.CreateShortcut(Target(f,p,"launch.lnk"));
            try{Assert.Equal(target,(string)copied.TargetPath);}finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(copied);}
        }
        finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
    }
    [Theory] [InlineData("manifest")] [InlineData("creation-gap")] [InlineData("unknown-version")] [InlineData("null-inventory")]
    public void BrokenOperationalEvidenceNeverAuthorizesDeletingCopies(string change)
    {
        using var f=new F();string source=Seed(f);var p=Preview(f);var files=new StorageRelocationFiles(f.Store);files.Prepare(p,p.Fingerprint);
        string ledger=Path.Combine(f.Store.StorageRelocationDirectory,p.Request.OperationId.ToString("N"));
        if(change=="manifest")File.AppendAllText(Path.Combine(ledger,"manifest.json"),"broken");
        if(change=="unknown-version")File.WriteAllText(Path.Combine(ledger,"manifest.json"),File.ReadAllText(Path.Combine(ledger,"manifest.json")).Replace("\"FormatVersion\":1","\"FormatVersion\":99"));
        if(change=="null-inventory")
        {
            var node=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(ledger,"manifest.json")))!;node["Collections"]=null;
            File.WriteAllText(Path.Combine(ledger,"manifest.json"),node.ToJsonString());
        }
        if(change=="creation-gap")File.Move(Path.Combine(ledger,"created-00000.json"),Path.Combine(ledger,"created-00000.keep"));
        var result=files.Recover(p.Request.OperationId);Assert.False(result.Committed);Assert.False(result.Cleaned);
        Assert.True(File.Exists(source));Assert.True(File.Exists(Target(f,p,"real.txt")));
        Assert.NotNull(new SqliteStorageRelocationJournal(f.Store).Get(p.Request.OperationId).Closure);
    }
    [Fact] public void SourceOrTargetReplacementIsNotMistakenForTheOriginalObject()
    {
        using var f=new F();string source=Seed(f);var p=Preview(f);var files=new StorageRelocationFiles(f.Store);files.Prepare(p,p.Fingerprint);
        string target=Target(f,p,"real.txt");File.Move(target,target+".keep");File.WriteAllText(target,"external replacement");
        Assert.Throws<IOException>(()=>files.Acquire(p));var result=files.Recover(p.Request.OperationId);
        Assert.False(result.Cleaned);Assert.Equal("external replacement",File.ReadAllText(target));Assert.True(File.Exists(source));
    }
    [Fact] public void LaterRetryCannotCleanAPathPreviouslyMarkedForPreservation()
    {
        using var f=new F();string source=Seed(f);var p=Preview(f);var files=new StorageRelocationFiles(f.Store);var journal=new SqliteStorageRelocationJournal(f.Store);
        files.Prepare(p,p.Fingerprint);journal.RecordTargets(p.Request.OperationId,files);journal.Commit(p.Request.OperationId,files);
        File.WriteAllText(source,"external edit");Assert.False(files.Recover(p.Request.OperationId).Cleaned);
        File.WriteAllText(source,"owned-content");Assert.False(files.Recover(p.Request.OperationId).Cleaned);Assert.True(File.Exists(source));
    }
    [Fact] public void NewBundleCanBeMigratedAgainWithoutCopyingOldOwnershipMetadata()
    {
        using var f=new F();Seed(f);var p=Preview(f);var first=Execute(f.Store,p,p.Fingerprint);Assert.True(first.Cleaned,first.Reason);
        var second=Preview(f);var result=Execute(f.Store,second,second.Fingerprint);Assert.True(result.Committed,result.Reason);Assert.True(result.Cleaned,result.Reason);
        Assert.Equal("owned-content",File.ReadAllText(Target(f,second,"real.txt")));Assert.True(File.Exists(Target(f,second,StorageBundleOwnership.ChildMarker)));
    }
}
