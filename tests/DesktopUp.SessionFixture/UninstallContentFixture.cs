using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

internal static class UninstallContentFixture
{
    internal static void Create(string requested)
    {
        string profile=Path.GetFullPath(requested),root=Path.GetDirectoryName(profile)!;
        string repo=AppContext.BaseDirectory;
        while(!File.Exists(Path.Combine(repo,"DesktopUp.sln")))repo=Path.GetDirectoryName(repo)??throw new IOException("Repository required.");
        if(!profile.StartsWith(Path.Combine(repo,"out","drop-d3")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Path.GetFileName(profile)!="profile" ||
            Directory.Exists(profile)||!File.Exists(Path.Combine(root,".uninstall-fixture")))throw new IOException("New owned fixture required.");
        using var store=new SqliteStateStore(new UserDataPaths(profile));var initialized=store.Initialize();if(!initialized.Succeeded)throw new IOException(initialized.Error);
        var repository=new SqliteStateCommandRepository(store);repository.EnsureInitialized(PosterConfigurationFactory.NewBasicBox("卸载归还 · 自建样例","DISPLAY1") with{PosterId=BuiltInStateIdentities.PrimaryPosterId});
        string storage=Path.Combine(root,"storage"),origin=Path.Combine(root,"original");Directory.CreateDirectory(storage);Directory.CreateDirectory(origin);
        var provisioner=new ManagedStorageProvisioner(store);var preview=provisioner.Preview(storage);var registration=provisioner.Confirm(preview,preview.Fingerprint);
        foreach(string name in new[]{"return-file.txt","return-folder"})
        {
            string source=Path.Combine(origin,name);
            if(name.EndsWith(".txt"))File.WriteAllText(source,"Owned uninstall sample");else{Directory.CreateDirectory(source);File.WriteAllText(Path.Combine(source,"child.txt"),"Owned child");}
            var input=DirectTransferPreparation.Incoming(repository.Read(),BuiltInStateIdentities.PrimaryPosterId,[source],registration);
            var journal=new SqliteTransferJournal(store);journal.Begin(input.Plan,input.Files,input.Plan.Fingerprint,registration.Collection.Id);
            if(new AutomaticTransferCompletion(journal).Execute(input.Plan.Request.OperationId).Any(r=>r.Kind!=TransferFinishKind.Completed))throw new IOException("Fixture collection failed.");
        }
        File.WriteAllText(Path.Combine(root,".ready"),"owned");Console.WriteLine(profile);
    }
}
