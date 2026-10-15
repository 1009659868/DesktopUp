using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;

internal static class CrossPosterReviewFixture
{
    internal static void Create(string requested)
    {
        string profile=Path.GetFullPath(requested), review=Path.GetDirectoryName(profile)!;
        string repo=AppContext.BaseDirectory;
        while(!File.Exists(Path.Combine(repo,"DesktopUp.sln")))repo=Path.GetDirectoryName(repo)??throw new IOException("Repository fixture required.");
        if(!profile.StartsWith(Path.Combine(repo,"out","drop-d3")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(profile)!="profile" || Directory.Exists(profile) || !File.Exists(Path.Combine(review,".cross-poster-fixture")))
            throw new IOException("New owned review profile required; existing data preserved.");
        using var store=new SqliteStateStore(new UserDataPaths(profile));
        var init=store.Initialize();if(!init.Succeeded)throw new IOException(init.Error);
        var repository=new SqliteStateCommandRepository(store);var coordinator=new StateProjectionCoordinator(repository);
        PosterDefinition Definition(string name,double x)=>PosterConfigurationFactory.NewBasicBox(name,"DISPLAY1") with
            { WidthDip=280,ExpandedHeightDip=480,HorizontalAnchor=x,VerticalAnchor=.15 };
        var first=Definition("A · 普通收纳",.02) with {PosterId=BuiltInStateIdentities.PrimaryPosterId};
        repository.EnsureInitialized(first);
        coordinator.EnsureInitialized();
        string Add(string name,double x,string? directory=null)
        {
            var state=repository.Read();var command=new CreatePosterCommand(Guid.NewGuid().ToString(),StateCommandSource.ControlCenter,state.Revision,
                new(state.Catalog.Posters.ToDictionary(p=>p.Poster.PosterId,p=>p.EntityVersion),CatalogVersion:state.Catalog.CatalogVersion,ContentVersion:state.ContentVersion),Definition(name,x));
            if(directory is not null)
            {
                var result=new MappedDirectoryService(store).Create(command,directory);
                if(result.Status!=StateCommandStatus.Committed)throw new IOException(result.Error);return result.ResultPosterId!;
            }
            var added=coordinator.Execute(command);if(!added.Succeeded)throw new IOException(added.Error);
            return repository.Read().Catalog.Posters.Single(p=>p.Poster.Title==name).Poster.PosterId;
        }
        string second=Add("B · 普通收纳",.34);
        string root=Path.Combine(review,"storage");Directory.CreateDirectory(root);
        var provisioner=new ManagedStorageProvisioner(store);var preview=provisioner.Preview(root);var storage=provisioner.Confirm(preview,preview.Fingerprint);
        void New(string poster,NewContentKind kind,string name)
        {
            var state=repository.Read();new ContentCreationService(store).Create(poster,new(storage,state.ContentVersion,
                state.Catalog.Posters.Single(p=>p.Poster.PosterId==poster).EntityVersion),kind,name);
        }
        New(first.PosterId,NewContentKind.TextFile,"A-图标随卡片.txt");
        New(first.PosterId,NewContentKind.TextFile,"collision.txt");
        New(first.PosterId,NewContentKind.Folder,"A-文件夹");
        New(second,NewContentKind.TextFile,"B-邻居.txt");
        foreach(var name in new[]{"mapping-C","mapping-D","outgoing-target","incoming-files"})Directory.CreateDirectory(Path.Combine(review,name));
        foreach(var path in new[]{"mapping-C/C-文件.txt","mapping-C/collision.txt","mapping-D/D-文件.txt","incoming-files/外部拖入.txt"})
            File.WriteAllText(Path.Combine(review,path),"V3-05-07 owned review sample: "+path);
        Add("C · 映射目录",.66,Path.Combine(review,"mapping-C"));
        Add("D · 映射目录",.98,Path.Combine(review,"mapping-D"));
        var current=repository.Read();var card=current.ContentCatalog.PosterItems.First(p=>p.PosterId==first.PosterId &&
            ContentRelationPolicy.Resolve(current.ContentCatalog,first.PosterId,p.LayoutItemId)!.Item.DisplayName=="A-图标随卡片.txt");
        byte[] pixels=new byte[32*32*4];for(int i=0;i<pixels.Length;i+=4){pixels[i]=190;pixels[i+1]=80;pixels[i+2]=120;pixels[i+3]=255;}
        var reads=StateReadSet.ForPoster(first.PosterId,current.Catalog.Posters.Single(p=>p.Poster.PosterId==first.PosterId).EntityVersion) with {ContentVersion=current.ContentVersion};
        var icon=new ContentIconCommand(Guid.NewGuid().ToString(),StateCommandSource.ControlCenter,current.Revision,reads,
            ContentRelationPolicy.Resolve(current.ContentCatalog,first.PosterId,card.LayoutItemId)!.Witness,ContentIconAsset.Create(32,32,pixels),true);
        var saved=repository.Execute(new(icon.RequestId,icon.RequestId,current.Revision,reads),s=>ContentIconPolicy.Prepare(icon,s));
        if(saved.Status!=StateCommandStatus.Committed)throw new IOException(saved.Error);
        Console.WriteLine(profile);
    }
}
