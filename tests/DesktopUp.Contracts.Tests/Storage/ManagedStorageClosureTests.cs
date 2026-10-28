using DesktopUp.Windows;
using DesktopUp.Application;
using DesktopUp.Infrastructure;
using F=DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class ManagedStorageClosureTests
{
    private static StorageProvisionPreview Interrupt(F f,ManagedStorageProvisioner service,StorageProvisionCheckpoint point)
    {
        string root=Path.Combine(f.DirectoryRoot,"interrupted");Directory.CreateDirectory(root);
        var p=service.Preview(root);
        Assert.Throws<TransferInterruptionException>(()=>service.Confirm(p,p.Fingerprint,at=>{if(at==point)throw new TransferInterruptionException("isolated loss");}));
        return p;
    }
    [Theory] [InlineData(StorageProvisionCheckpoint.IntentWritten)] [InlineData(StorageProvisionCheckpoint.RootClaimed)]
    [InlineData(StorageProvisionCheckpoint.DirectoryCreated)] [InlineData(StorageProvisionCheckpoint.ChildClaimed)]
    public void StopPreservesDirectoryAndClaimsAndRemainsClosedAfterRestart(StorageProvisionCheckpoint point)
    {
        using var f=new F();var service=new ManagedStorageProvisioner(f.Store);var p=Interrupt(f,service,point);
        if(Directory.Exists(p.CollectionPath))File.WriteAllText(Path.Combine(p.CollectionPath,"keep.txt"),"unclaimed-user-content");
        var before=Directory.GetFiles(p.Intent.Root.Locator,"*",SearchOption.AllDirectories).ToDictionary(x=>x,File.ReadAllText);
        var stop=service.PreviewStop(p.Intent.CollectionId);long revision=f.State.Revision;
        service.ConfirmStop(stop,stop.Fingerprint);service.ConfirmStop(stop,stop.Fingerprint);
        using var store=new SqliteStateStore(f.Paths);Assert.True(store.Initialize().Succeeded);
        var restarted=new ManagedStorageProvisioner(store);
        Assert.DoesNotContain(restarted.Pending(),x=>x.CollectionId==p.Intent.CollectionId);Assert.Single(restarted.Stopped());
        Assert.Equal(revision,f.State.Revision);Assert.Single(f.Journal.StorageLocations());
        Assert.Equal(before.Keys.Order(),Directory.GetFiles(p.Intent.Root.Locator,"*",SearchOption.AllDirectories).Order());
        foreach(var (path,text) in before)Assert.Equal(text,File.ReadAllText(path));
        Assert.Throws<IOException>(()=>restarted.Confirm(p,p.Fingerprint));Assert.Throws<IOException>(()=>restarted.Preview(p.Intent.Root.Locator));
        Assert.True(File.Exists(Path.Combine(f.Paths.State,"managed-file-history.guard")));
        Assert.Equal(2,Directory.GetFiles(Path.Combine(f.Paths.State,"storage-provisions"),"*.json").Length);
    }
    [Fact] public void StopUnprovenChildDoesNotAdoptItAndAnotherRootCanBeExplicitlyRegistered()
    {
        using var f=new F();var service=new ManagedStorageProvisioner(f.Store);var p=Interrupt(f,service,StorageProvisionCheckpoint.DirectoryCreated);
        var stop=service.PreviewStop(p.Intent.CollectionId);service.ConfirmStop(stop,stop.Fingerprint);
        Assert.False(File.Exists(Path.Combine(p.CollectionPath,".desktopup-collection-owner.json")));
        string root=Path.Combine(f.DirectoryRoot,"new-root");Directory.CreateDirectory(root);
        var next=service.Preview(root);service.Confirm(next,next.Fingerprint);
        Assert.Equal(2,f.Journal.StorageLocations().Count);Assert.True(Directory.Exists(p.CollectionPath));
    }
    [Theory] [InlineData("missing")] [InlineData("replaced")] [InlineData("unknown-marker")]
    public void StopDoesNotRequireOrGuessExternalDirectoryOwnership(string change)
    {
        using var f=new F();var service=new ManagedStorageProvisioner(f.Store);var p=Interrupt(f,service,StorageProvisionCheckpoint.DirectoryCreated);
        if(change=="unknown-marker")File.WriteAllText(Path.Combine(p.CollectionPath,".desktopup-collection-owner.json"),"future-schema");
        else
        {
            Directory.Move(p.Intent.Root.Locator,p.Intent.Root.Locator+".keep");
            if(change=="replaced"){Directory.CreateDirectory(p.Intent.Root.Locator);File.WriteAllText(Path.Combine(p.Intent.Root.Locator,"unknown.txt"),"preserve");}
        }
        var stop=service.PreviewStop(p.Intent.CollectionId);service.ConfirmStop(stop,stop.Fingerprint);
        Assert.Single(service.Stopped());Assert.Single(f.Journal.StorageLocations());
        if(change=="unknown-marker")Assert.Equal("future-schema",File.ReadAllText(Path.Combine(p.CollectionPath,".desktopup-collection-owner.json")));
        else Assert.True(Directory.Exists(p.Intent.Root.Locator+".keep"));
        if(change=="replaced")Assert.Equal("preserve",File.ReadAllText(Path.Combine(p.Intent.Root.Locator,"unknown.txt")));
    }
    [Fact] public void RegisteredDirectoryCannotBeStoppedOrUnbound()
    {
        using var f=new F();var service=new ManagedStorageProvisioner(f.Store);
        Assert.Throws<IOException>(()=>service.PreviewStop(f.Collection.Id));Assert.Empty(service.Stopped());Assert.Single(f.Journal.StorageLocations());
    }
    [Fact] public void LowLevelRegisterCannotBypassStoppedCreationWithValidMarkers()
    {
        using var f=new F();var service=new ManagedStorageProvisioner(f.Store);var p=Interrupt(f,service,StorageProvisionCheckpoint.ChildClaimed);
        var stop=service.PreviewStop(p.Intent.CollectionId);service.ConfirmStop(stop,stop.Fingerprint);
        var collection=new DesktopUp.Domain.Collection(p.Intent.CollectionId,"must-not-register",DesktopUp.Domain.ContentCollectionKind.Managed,
            p.CollectionPath,DesktopUp.Domain.ContentStorageOwnership.Managed);
        Assert.Throws<IOException>(()=>f.Journal.Register(p.Intent.Root,collection,new(new Dictionary<string,long>(),ContentVersion:f.State.ContentVersion)));
        Assert.Single(f.Journal.StorageLocations());Assert.Single(service.Stopped());
    }
    [Fact] public void RegistrationAfterPreviewInvalidatesStop()
    {
        using var f=new F();var service=new ManagedStorageProvisioner(f.Store);var p=Interrupt(f,service,StorageProvisionCheckpoint.ChildClaimed);
        var stop=service.PreviewStop(p.Intent.CollectionId);service.Confirm(p,p.Fingerprint);
        Assert.Throws<IOException>(()=>service.ConfirmStop(stop,stop.Fingerprint));Assert.Empty(service.Stopped());Assert.Equal(2,f.Journal.StorageLocations().Count);
    }
    [Theory] [InlineData("wrong")] [InlineData("state")]
    public void WrongOrStaleStopDoesNotSuppressDirectoryPending(string change)
    {
        using var f=new F();var service=new ManagedStorageProvisioner(f.Store);var p=Interrupt(f,service,StorageProvisionCheckpoint.DirectoryCreated);
        var stop=service.PreviewStop(p.Intent.CollectionId);
        if(change=="state")f.AddReference(LocalContentReferenceProbe.Inspect(f.File("new.txt")).Item!);
        Assert.Throws<IOException>(()=>service.ConfirmStop(stop,change=="wrong"?"wrong":stop.Fingerprint));
        Assert.Contains(service.Pending(),x=>x.CollectionId==p.Intent.CollectionId);Assert.Empty(service.Stopped());
    }
    [Fact] public void SqlFailurePreservesPendingIntentAndExternalObjects()
    {
        using var f=new F();var service=new ManagedStorageProvisioner(f.Store);var p=Interrupt(f,service,StorageProvisionCheckpoint.DirectoryCreated);
        var stop=service.PreviewStop(p.Intent.CollectionId);
        f.Sql("CREATE TRIGGER fail_directory_stop BEFORE INSERT ON storage_provision_closures BEGIN SELECT RAISE(ABORT,'fault'); END;");
        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(()=>service.ConfirmStop(stop,stop.Fingerprint));
        Assert.Empty(service.Stopped());Assert.Contains(service.Pending(),x=>x.CollectionId==p.Intent.CollectionId);Assert.True(Directory.Exists(p.CollectionPath));
    }
    [Fact] public void ExecutorLeaseBlocksDirectoryStopAndPreview()
    {
        using var f=new F();var service=new ManagedStorageProvisioner(f.Store);var p=Interrupt(f,service,StorageProvisionCheckpoint.DirectoryCreated);
        var stop=service.PreviewStop(p.Intent.CollectionId);using var lease=f.Journal.AcquireExecutor();
        Assert.Throws<IOException>(()=>service.PreviewStop(p.Intent.CollectionId));Assert.Throws<IOException>(()=>service.ConfirmStop(stop,stop.Fingerprint));Assert.Empty(service.Stopped());
    }
    [Fact] public void ForeignProfileCannotCloseAnOwnersIntentUsingCopiedRecord()
    {
        using var f=new F();var service=new ManagedStorageProvisioner(f.Store);var p=Interrupt(f,service,StorageProvisionCheckpoint.DirectoryCreated);
        var paths=new UserDataPaths(Path.Combine(f.DirectoryRoot,"other-profile"));using var other=new SqliteStateStore(paths);Assert.True(other.Initialize().Succeeded);
        Directory.CreateDirectory(Path.Combine(paths.State,"storage-provisions"));
        foreach(var path in Directory.GetFiles(Path.Combine(f.Paths.State,"storage-provisions"),"*.json"))File.Copy(path,Path.Combine(paths.State,"storage-provisions",Path.GetFileName(path)));
        Assert.Throws<IOException>(()=>new ManagedStorageProvisioner(other).PreviewStop(p.Intent.CollectionId));
        Assert.Contains(service.Pending(),x=>x.CollectionId==p.Intent.CollectionId);
    }
}
