using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

internal static class ContentReturnReviewFixture
{
    internal static void Create(string requested, bool uninstall = false)
    {
        string profile=Path.GetFullPath(requested), root=Path.GetDirectoryName(profile)!;
        string repo=AppContext.BaseDirectory;
        while(!File.Exists(Path.Combine(repo,"DesktopUp.sln")))repo=Path.GetDirectoryName(repo)??throw new IOException("Repository fixture required.");
        if(!profile.StartsWith(Path.Combine(repo,"out","drop-d3")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(profile)!="profile" || Directory.Exists(profile) || !File.Exists(Path.Combine(root,".content-return-fixture")))
            throw new IOException("New owned return fixture required; existing data preserved.");
        using var store=new SqliteStateStore(new UserDataPaths(profile));
        var initialized=store.Initialize();if(!initialized.Succeeded)throw new IOException(initialized.Error);
        var repository=new SqliteStateCommandRepository(store);var coordinator=new StateProjectionCoordinator(repository);
        PosterDefinition Definition(string name,double x)=>PosterConfigurationFactory.NewBasicBox(name,"DISPLAY1") with
            {WidthDip=280,ExpandedHeightDip=480,HorizontalAnchor=x,VerticalAnchor=.15};
        var first=Definition("A · 原路返回",.02) with {PosterId=BuiltInStateIdentities.PrimaryPosterId};
        repository.EnsureInitialized(first);coordinator.EnsureInitialized();
        string Add(string name,double x,string? directory=null)
        {
            var state=repository.Read();var command=new CreatePosterCommand(Guid.NewGuid().ToString(),StateCommandSource.ControlCenter,state.Revision,
                new(state.Catalog.Posters.ToDictionary(p=>p.Poster.PosterId,p=>p.EntityVersion),CatalogVersion:state.Catalog.CatalogVersion,ContentVersion:state.ContentVersion),Definition(name,x));
            if(directory is not null)
            {var result=new MappedDirectoryService(store).Create(command,directory);if(result.Status!=StateCommandStatus.Committed)throw new IOException(result.Error);return result.ResultPosterId!;}
            coordinator.Refresh();var added=coordinator.Execute(command);if(!added.Succeeded)throw new IOException(added.Error);return added.ResultPosterId!;
        }
        string second=Add("B · 返回桌面",.34), third=Add("C · 冲突取消",.66);
        foreach(var name in new[]{"storage","origin-A","origin-B","origin-C","mapping-D"})Directory.CreateDirectory(Path.Combine(root,name));
        var provisioner=new ManagedStorageProvisioner(store);var preview=provisioner.Preview(Path.Combine(root,"storage"));var storage=provisioner.Confirm(preview,preview.Fingerprint);
        void Collect(string poster,string source)
        {
            var plan=DirectTransferPreparation.Incoming(repository.Read(),poster,[source],storage);
            var journal=new SqliteTransferJournal(store);journal.Begin(plan.Plan,plan.Files,plan.Plan.Fingerprint,storage.Collection.Id);
            var results=new AutomaticTransferCompletion(journal).Execute(plan.Plan.Request.OperationId);
            if(results.Any(r=>r.Kind!=TransferFinishKind.Completed))throw new IOException("Owned sample collection failed.");
        }
        string Make(string directory,string name){string path=Path.Combine(root,directory,name);File.WriteAllText(path,"DesktopUp owned lifecycle sample: "+name);return path;}
        Collect(first.PosterId,Make("origin-A","A-return.txt"));
        string folder=Path.Combine(root,"origin-A","A-folder");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"child.txt"),"owned child");Collect(first.PosterId,folder);
        string unique="DesktopUp-05-12-"+Guid.NewGuid().ToString("N")[..8];
        Collect(second,Make("origin-B",unique+"-fallback.txt"));
        Directory.Delete(Path.Combine(root,"origin-B")); // Known-empty, fixture-only original directory.
        var current=repository.Read();new ContentCreationService(store).Create(second,new(storage,current.ContentVersion,
            current.Catalog.Posters.Single(p=>p.Poster.PosterId==second).EntityVersion),NewContentKind.TextFile,unique+"-new.txt");
        string collision=Make("origin-C","C-collision.txt");Collect(third,collision);File.WriteAllText(collision,"existing unrelated owned sample; must not silently overwrite");
        Make("mapping-D","D-mapped.txt");Add("D · 映射不搬动",.98,Path.Combine(root,"mapping-D"));
        if (uninstall)
            File.WriteAllText(Path.Combine(storage.Collection.StorageLocator!, unique+"-no-card.txt"), "Owned sample without a visible card");
        Console.WriteLine(profile);
    }
}
