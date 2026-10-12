using DesktopUp.Application;
using DesktopUp.Infrastructure;

internal static class StorageRelocationCrashFixture
{
    internal static int Run(string fixtureRoot,string boundary)
    {
        string repo=AppContext.BaseDirectory;
        while(!File.Exists(Path.Combine(repo,"DesktopUp.sln")))repo=Path.GetDirectoryName(repo)??throw new IOException("Fixture repository missing.");
        string root=LocalTransferFiles.Normalize(fixtureRoot),allowed=LocalTransferFiles.Normalize(Path.Combine(repo,"out","transfer-journal","fixtures"));
        if(!LocalTransferFiles.Contains(allowed,root)||!File.Exists(Path.Combine(root,".transfer-fixture-owner"))||
            File.ReadAllText(Path.Combine(root,".transfer-fixture-owner"))!="owned")return 60;
        using var store=new SqliteStateStore(new UserDataPaths(Path.Combine(root,"profile")));
        if(!store.Initialize().Succeeded)return 61;
        // Empty collections only: the adapter below never proves/copies a real content file.
        if(new SqliteStateCommandRepository(store).Read().ContentCatalog.Items.Count!=0)return 62;
        if(new SqliteTransferJournal(store).StorageLocations().Any(x=>!LocalTransferFiles.Contains(root,x.Root.Locator)))return 63;
        string target=Path.Combine(root,"crash-destination");Directory.CreateDirectory(target);
        var journal=new SqliteStorageRelocationJournal(store);var plan=journal.Preview(target);
        var point=Enum.Parse<StorageRelocationCheckpoint>(boundary);
        void At(StorageRelocationCheckpoint p){if(p==point)Environment.Exit(75);}
        journal.Begin(plan,plan.Fingerprint,At);
        var verifier=new EmptyFixtureVerifier(StorageBundleOwnership.CreatePrepared(store,plan),plan.Fingerprint);
        journal.RecordTargets(plan.Request.OperationId,verifier,At);journal.Commit(plan.Request.OperationId,verifier,At);return 64;
    }
    private sealed class EmptyFixtureVerifier(StorageBundleClaim claim,string fingerprint):IStorageRelocationVerifier
    {
        public StorageRelocationProofLease Acquire(StorageRelocationPlan plan)=>new Lease(new(fingerprint,"empty-collection-test-only",claim));
        private sealed class Lease(StorageRelocationTargets targets):StorageRelocationProofLease
        {public override StorageRelocationTargets Targets=>targets;public override void Dispose(){} }
    }
}
