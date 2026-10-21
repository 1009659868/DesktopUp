using DesktopUp.Windows;
using DesktopUp.Application;
using DesktopUp.Infrastructure;
using F=DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

public sealed class ManagedStorageAdoptionTests
{
    [Theory] [InlineData(StorageAdoptionCheckpoint.IntentWritten)] [InlineData(StorageAdoptionCheckpoint.RootClaimed)]
    [InlineData(StorageAdoptionCheckpoint.ChildClaimed)] [InlineData(StorageAdoptionCheckpoint.Activated)]
    public void IndependentProcessCrashDoesNotInventAnActivatedClaim(StorageAdoptionCheckpoint point)
    {
        using var f=new F();Legacy(f);
        string helper=Path.Combine(AppContext.BaseDirectory,"content-test-helpers","DesktopUp.TransferFixture.exe");
        var start=new System.Diagnostics.ProcessStartInfo(helper){UseShellExecute=false,CreateNoWindow=true};
        foreach(string value in new[]{"--adoption-crash",f.DirectoryRoot,f.Collection.Id.ToString(),point.ToString()})start.ArgumentList.Add(value);
        using var child=System.Diagnostics.Process.Start(start)!;Assert.True(child.WaitForExit(15000));Assert.Equal(74,child.ExitCode);
        using var restarted=new SqliteStateStore(f.Paths);Assert.True(restarted.Initialize().Succeeded);
        Assert.Equal(point==StorageAdoptionCheckpoint.Activated?1L:0L,f.Sql("SELECT count(*) FROM managed_storage_adoptions WHERE activated=1"));
        if(point!=StorageAdoptionCheckpoint.Activated)
        {
            Assert.Throws<IOException>(()=>ManagedStorageProvisioner.ValidateRegistration(restarted,f.Root,f.Collection));
            var service=new ManagedStorageAdoption(restarted);var p=service.Preview(f.Collection.Id);service.Confirm(p,p.Fingerprint);
        }
        ManagedStorageProvisioner.ValidateRegistration(restarted,f.Root,f.Collection);Assert.Empty(f.State.ContentCatalog.Items);
    }
    [Fact] public void ExecutorLeaseBlocksPreviewAndConfirmationWithoutWritingMarkers()
    {
        using var f=new F();Legacy(f);var service=new ManagedStorageAdoption(f.Store);var p=service.Preview(f.Collection.Id);
        using var lease=f.Journal.AcquireExecutor();
        Assert.Throws<IOException>(()=>service.Preview(f.Collection.Id));Assert.Throws<IOException>(()=>service.Confirm(p,p.Fingerprint));
        Assert.False(File.Exists(RootClaim(f)));
    }
    private static void Legacy(F f)
    {
        // These are exclusively fixture-owned markers; preserve them outside the inventory.
        File.Move(Path.Combine(f.Root.Locator,".desktopup-storage-owner.json"),Path.Combine(f.DirectoryRoot,"original-root-claim.keep"));
        File.Move(Path.Combine(f.Collection.StorageLocator!,".desktopup-collection-owner.json"),Path.Combine(f.DirectoryRoot,"original-child-claim.keep"));
    }
    private static string RootClaim(F f)=>Path.Combine(f.Root.Locator,ManagedStorageAdoption.RootMarker);
    private static string ChildClaim(F f)=>Path.Combine(f.Collection.StorageLocator!,ManagedStorageAdoption.ChildMarker);
    [Fact] public void PreviewIsReadOnlyThenExplicitAdoptionPreservesFilesReferencesAndRevision()
    {
        using var f=new F();string file=f.File("registered.txt");var transfer=f.Stage(file);f.Executor.Execute(transfer.Plan.Request.OperationId);
        Legacy(f);File.WriteAllText(Path.Combine(f.Collection.StorageLocator!,"extra.txt"),"must-not-auto-import");
        var service=new ManagedStorageAdoption(f.Store);var before=StateRequestFingerprint.Compute(f.State);
        Assert.Single(service.Candidates());var p=service.Preview(f.Collection.Id);
        Assert.Equal(2,p.FileCount);Assert.Equal(0,p.DirectoryCount);Assert.False(File.Exists(RootClaim(f)));
        Assert.Equal(0L,f.Sql("SELECT count(*) FROM managed_storage_adoptions"));
        Assert.Throws<IOException>(()=>f.Stage(f.File("not-yet.txt")));
        service.Confirm(p,p.Fingerprint);service.Confirm(p,p.Fingerprint);
        Assert.Equal(before,StateRequestFingerprint.Compute(f.State));Assert.Single(f.State.ContentCatalog.Items);
        Assert.Equal("must-not-auto-import",File.ReadAllText(Path.Combine(f.Collection.StorageLocator!,"extra.txt")));
        Assert.Equal(1L,f.Sql("SELECT count(*) FROM managed_storage_adoptions WHERE activated=1"));Assert.Empty(service.Candidates());
        var next=f.Stage(f.File("now-ready.txt"));Assert.Equal(DurableTransferPhase.IndexCommitted,Assert.Single(f.Executor.Execute(next.Plan.Request.OperationId)).Phase);
    }
    [Theory] [InlineData(StorageAdoptionCheckpoint.IntentWritten)] [InlineData(StorageAdoptionCheckpoint.RootClaimed)]
    [InlineData(StorageAdoptionCheckpoint.ChildClaimed)] [InlineData(StorageAdoptionCheckpoint.Activated)]
    public void EachDurableBoundarySurvivesRestartWithoutImplicitActivation(StorageAdoptionCheckpoint boundary)
    {
        using var f=new F();Legacy(f);var service=new ManagedStorageAdoption(f.Store);var p=service.Preview(f.Collection.Id);
        Assert.Throws<TransferInterruptionException>(()=>service.Confirm(p,p.Fingerprint,at=>{if(at==boundary)throw new TransferInterruptionException("isolated loss");}));
        using var restarted=new SqliteStateStore(f.Paths);Assert.True(restarted.Initialize().Succeeded);
        var again=new ManagedStorageAdoption(restarted);var before=StateRequestFingerprint.Compute(f.State);
        if(boundary!=StorageAdoptionCheckpoint.Activated)
        {
            Assert.Equal(0L,f.Sql("SELECT count(*) FROM managed_storage_adoptions WHERE activated=1"));Assert.Single(again.Candidates());
            Assert.Throws<IOException>(()=>ManagedStorageProvisioner.ValidateRegistration(restarted,f.Root,f.Collection));
            var fresh=again.Preview(f.Collection.Id);again.Confirm(fresh,fresh.Fingerprint);
        }
        else again.Confirm(p,p.Fingerprint);
        ManagedStorageProvisioner.ValidateRegistration(restarted,f.Root,f.Collection);
        Assert.Equal(before,StateRequestFingerprint.Compute(f.State));Assert.Equal(1L,f.Sql("SELECT count(*) FROM managed_storage_adoptions"));
    }
    [Theory] [InlineData("wrong")] [InlineData("content")] [InlineData("state")] [InlineData("added-file")] [InlineData("identity")]
    public void StaleOrUnconfirmedPreviewCannotWriteClaims(string change)
    {
        using var f=new F();Legacy(f);string file=Path.Combine(f.Collection.StorageLocator!,"inventory.txt");File.WriteAllText(file,"before");
        var service=new ManagedStorageAdoption(f.Store);var p=service.Preview(f.Collection.Id);
        if(change=="content")File.WriteAllText(file,"after");
        if(change=="state")f.AddReference(LocalContentReferenceProbe.Inspect(f.File("state.txt")).Item!);
        if(change=="added-file")File.WriteAllText(Path.Combine(f.Collection.StorageLocator!,"another.txt"),"extra");
        if(change=="identity"){File.Move(file,file+".keep");File.WriteAllText(file,"before");}
        Assert.Throws<IOException>(()=>service.Confirm(p,change=="wrong"?"wrong":p.Fingerprint));
        Assert.False(File.Exists(RootClaim(f)));Assert.Equal(0L,f.Sql("SELECT count(*) FROM managed_storage_adoptions"));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void ReplacedRootOrChildCannotBeTakenOver(bool root)
    {
        using var f=new F();Legacy(f);string path=root?f.Root.Locator:f.Collection.StorageLocator!;
        Directory.Move(path,path+".keep");Directory.CreateDirectory(path);
        Assert.Throws<IOException>(()=>new ManagedStorageAdoption(f.Store).Preview(f.Collection.Id));
        Assert.False(File.Exists(RootClaim(f)));Assert.True(Directory.Exists(path+".keep"));
    }
    [Theory] [InlineData("old-root")] [InlineData("old-child")] [InlineData("future-root")] [InlineData("foreign-child")]
    public void UnknownOrOtherDeclarationsAreNeverOverwritten(string kind)
    {
        using var f=new F();Legacy(f);
        string marker=kind switch {"old-root"=>Path.Combine(f.Root.Locator,".desktopup-storage-owner.json"),
            "old-child"=>Path.Combine(f.Collection.StorageLocator!,".desktopup-collection-owner.json"),"future-root"=>RootClaim(f),_=>ChildClaim(f)};
        File.WriteAllText(marker,"{\"FormatVersion\":999}");
        Assert.ThrowsAny<Exception>(()=>new ManagedStorageAdoption(f.Store).Preview(f.Collection.Id));
        Assert.Equal("{\"FormatVersion\":999}",File.ReadAllText(marker));Assert.Equal(0L,f.Sql("SELECT count(*) FROM managed_storage_adoptions"));
    }
    [Fact] public void PartialClaimsRequireNewConfirmationIfStateOrInventoryChanges()
    {
        using var f=new F();Legacy(f);var service=new ManagedStorageAdoption(f.Store);var p=service.Preview(f.Collection.Id);
        Assert.Throws<TransferInterruptionException>(()=>service.Confirm(p,p.Fingerprint,at=>{if(at==StorageAdoptionCheckpoint.ChildClaimed)throw new TransferInterruptionException("loss");}));
        var root=File.ReadAllBytes(RootClaim(f));var child=File.ReadAllBytes(ChildClaim(f));
        f.AddReference(LocalContentReferenceProbe.Inspect(f.File("other.txt")).Item!);
        File.WriteAllText(Path.Combine(f.Collection.StorageLocator!,"keep.txt"),"not imported");
        Assert.Throws<IOException>(()=>service.Confirm(p,p.Fingerprint));
        var next=service.Preview(f.Collection.Id);Assert.NotEqual(p.Fingerprint,next.Fingerprint);service.Confirm(next,next.Fingerprint);
        Assert.Equal(root,File.ReadAllBytes(RootClaim(f)));Assert.Equal(child,File.ReadAllBytes(ChildClaim(f)));
        Assert.Equal(2L,f.Sql("SELECT count(*) FROM managed_storage_adoptions"));Assert.Equal(1L,f.Sql("SELECT count(*) FROM managed_storage_adoptions WHERE activated=1"));
    }
    [Theory] [InlineData("prepare")] [InlineData("activate")]
    public void SqlFailureCannotGrantOwnershipAndRecoveryRemainsExplicit(string phase)
    {
        using var f=new F();Legacy(f);var service=new ManagedStorageAdoption(f.Store);var p=service.Preview(f.Collection.Id);
        f.Sql($"CREATE TRIGGER fail_adopt BEFORE {(phase=="prepare"?"INSERT":"UPDATE")} ON managed_storage_adoptions BEGIN SELECT RAISE(ABORT,'fault'); END;");
        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(()=>service.Confirm(p,p.Fingerprint));
        Assert.Equal(0L,f.Sql("SELECT count(*) FROM managed_storage_adoptions WHERE activated=1"));
        Assert.Equal(phase=="activate",File.Exists(ChildClaim(f)));Assert.Throws<IOException>(()=>f.Stage(f.File("safe.txt")));
        f.Sql("DROP TRIGGER fail_adopt;");var fresh=service.Preview(f.Collection.Id);service.Confirm(fresh,fresh.Fingerprint);
        ManagedStorageProvisioner.ValidateRegistration(f.Store,f.Root,f.Collection);
    }
    [Fact] public void ConcurrentInventoryChangeAfterClaimsPreventsActivation()
    {
        using var f=new F();Legacy(f);var service=new ManagedStorageAdoption(f.Store);var p=service.Preview(f.Collection.Id);
        Assert.Throws<IOException>(()=>service.Confirm(p,p.Fingerprint,at=>
        {if(at==StorageAdoptionCheckpoint.ChildClaimed)File.WriteAllText(Path.Combine(f.Collection.StorageLocator!,"concurrent.txt"),"keep");}));
        Assert.Equal(0L,f.Sql("SELECT count(*) FROM managed_storage_adoptions WHERE activated=1"));Assert.Empty(f.State.ContentCatalog.Items);
    }
    [Fact] public void OutstandingFileOperationMustBeResolvedBeforeAdoption()
    {
        using var f=new F();var transfer=f.Stage(f.File("pending.txt"));Legacy(f);var service=new ManagedStorageAdoption(f.Store);
        Assert.Throws<IOException>(()=>service.Preview(f.Collection.Id));
        var stop=f.Journal.PreviewStop(transfer.Plan.Request.OperationId,0);f.Journal.ConfirmStop(stop,stop.Fingerprint);
        var p=service.Preview(f.Collection.Id);service.Confirm(p,p.Fingerprint);Assert.True(File.Exists(transfer.Files[0].Source));
    }
    [Fact] public void CopiedProfileCannotReusePhysicalOwnersClaims()
    {
        using var f=new F();Legacy(f);var service=new ManagedStorageAdoption(f.Store);var p=service.Preview(f.Collection.Id);service.Confirm(p,p.Fingerprint);
        var otherPaths=new UserDataPaths(Path.Combine(f.DirectoryRoot,"other-profile"));
        Directory.CreateDirectory(otherPaths.State);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Copy(Path.Combine(f.Paths.State,"desktopup.sqlite3"),Path.Combine(otherPaths.State,"desktopup.sqlite3"));
        using var other=new SqliteStateStore(otherPaths);Assert.True(other.Initialize().Succeeded);
        Assert.Throws<IOException>(()=>ManagedStorageProvisioner.ValidateRegistration(other,f.Root,f.Collection));
    }
    [Fact] public void AdoptionCannotBeUsedToCreateSecondClaimOrRegisterArbitraryDirectory()
    {
        using var f=new F();Legacy(f);var service=new ManagedStorageAdoption(f.Store);
        Assert.Throws<IOException>(()=>service.Preview(Guid.NewGuid()));var p=service.Preview(f.Collection.Id);service.Confirm(p,p.Fingerprint);
        Assert.Throws<IOException>(()=>new ManagedStorageProvisioner(f.Store).Preview(f.Root.Locator));
        Assert.Throws<IOException>(()=>service.Preview(f.Collection.Id));
    }
    [Fact] public void LostClaimOrUnknownDbFormatBlocksPreviouslyAdoptedStorage()
    {
        using var f=new F();Legacy(f);var service=new ManagedStorageAdoption(f.Store);var p=service.Preview(f.Collection.Id);service.Confirm(p,p.Fingerprint);
        File.Move(ChildClaim(f),Path.Combine(f.DirectoryRoot,"adopted-child.keep"));
        Assert.Throws<IOException>(()=>ManagedStorageProvisioner.ValidateRegistration(f.Store,f.Root,f.Collection));
        File.Move(Path.Combine(f.DirectoryRoot,"adopted-child.keep"),ChildClaim(f));
        f.Sql("UPDATE managed_storage_adoptions SET body_json=json_set(body_json,'$.FormatVersion',999)");
        Assert.Throws<IOException>(()=>ManagedStorageProvisioner.ValidateRegistration(f.Store,f.Root,f.Collection));
    }
    [Fact] public void Schema17UpgradePreservesChecksumsAndDoesNotCreateOwnership()
    {
        using var f=new F();Legacy(f);
        f.Sql(StorageSchemaFixture.Drop19+"DROP TABLE managed_storage_adoptions;DELETE FROM schema_migrations WHERE version>=18;UPDATE state_head SET schema_version=17;PRAGMA user_version=17;");
        var sums=f.Sql("SELECT group_concat(checksum_sha256) FROM schema_migrations ORDER BY version");
        var result=f.Store.Initialize();Assert.True(result.Succeeded,result.Error);Assert.Equal(26,result.Head!.SchemaVersion);
        Assert.Equal(sums,f.Sql("SELECT group_concat(checksum_sha256) FROM schema_migrations WHERE version<=17 ORDER BY version"));
        Assert.False(File.Exists(RootClaim(f)));Assert.Equal(0L,f.Sql("SELECT count(*) FROM managed_storage_adoptions"));
    }
    [Fact] public void FailedSchema18RollsBackWithoutClaims()
    {
        using var f=new F();Legacy(f);
        f.Sql(StorageSchemaFixture.Drop19+"DROP TABLE managed_storage_adoptions;DELETE FROM schema_migrations WHERE version>=18;UPDATE state_head SET schema_version=17;PRAGMA user_version=17;CREATE TABLE managed_storage_adoptions(conflict TEXT);");
        Assert.False(f.Store.Initialize().Succeeded);Assert.Equal(17L,f.Sql("PRAGMA user_version"));Assert.False(File.Exists(RootClaim(f)));
    }
}
