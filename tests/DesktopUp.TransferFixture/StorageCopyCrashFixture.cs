using DesktopUp.Application;
using DesktopUp.Infrastructure;

internal static class StorageCopyCrashFixture
{
    internal static int Run(string fixtureRoot,string boundary)
    {
        string repo=AppContext.BaseDirectory;
        while(!File.Exists(Path.Combine(repo,"DesktopUp.sln")))repo=Path.GetDirectoryName(repo)??throw new IOException("Fixture repository missing.");
        string root=LocalTransferFiles.Normalize(fixtureRoot),allowed=LocalTransferFiles.Normalize(Path.Combine(repo,"out","transfer-journal","fixtures"));
        if(!LocalTransferFiles.Contains(allowed,root)||File.ReadAllText(Path.Combine(root,".transfer-fixture-owner"))!="owned")return 60;
        using var store=new SqliteStateStore(new UserDataPaths(Path.Combine(root,"profile")));if(!store.Initialize().Succeeded)return 61;
        if(new SqliteTransferJournal(store).StorageLocations().Any(x=>!LocalTransferFiles.Contains(root,x.Root.Locator)))return 63;
        string destination=Path.Combine(root,"copy-crash-destination");Directory.CreateDirectory(destination);
        var journal=new SqliteStorageRelocationJournal(store);var plan=journal.Preview(destination);
        using var cancel=new CancellationTokenSource();
        new StorageRelocationUseCase(new LocalStorageRelocationExecution(store,copyCheckpoint:p=>
        {
            if(boundary=="TargetDeleted"&&p==StorageCopyCheckpoint.Prepared)cancel.Cancel();
            if(p.ToString()==boundary)Environment.Exit(76);
        },transactionCheckpoint:p=>{if(p.ToString()==boundary)Environment.Exit(76);})).Execute(plan,plan.Fingerprint,cancel.Token);return 64;
    }
}
