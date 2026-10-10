using DesktopUp.Infrastructure;
using DesktopUp.Application;
using DesktopUp.Domain;

if(args is ["--prepare-adoption-review",var newReviewRoot])
{
    string repo=AppContext.BaseDirectory;
    while(!File.Exists(Path.Combine(repo,"DesktopUp.sln")))repo=Path.GetDirectoryName(repo)??throw new IOException("Repository fixture root missing.");
    string root=LocalTransferFiles.Normalize(newReviewRoot),allowed=LocalTransferFiles.Normalize(Path.Combine(repo,"out","transfer-adoption"));
    if(!LocalTransferFiles.Contains(allowed,root)||Directory.Exists(root)||File.Exists(root))return 64;
    Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,".transfer-fixture-owner"),"owned");
    using var store=new SqliteStateStore(new UserDataPaths(Path.Combine(root,"profile")));
    if(!store.Initialize().Succeeded)return 65;
    var repository=new SqliteStateCommandRepository(store);
    repository.EnsureInitialized(PosterConfigurationFactory.NewBasicBox("新增恢复分支隔离验收","DISPLAY1") with {PosterId=BuiltInStateIdentities.PrimaryPosterId});
    var provisioner=new ManagedStorageProvisioner(store);
    string storage=Path.Combine(root,"正常存储");Directory.CreateDirectory(storage);
    var setup=provisioner.Preview(storage);var registered=provisioner.Confirm(setup,setup.Fingerprint);
    // Register all storage before creating a pending physical transfer: the production
    // write guard correctly refuses catalogue edits while a moved file is unresolved.
    string legacy=Path.Combine(root,"旧目录待认领");Directory.CreateDirectory(legacy);var old=provisioner.Preview(legacy);var oldRegistered=provisioner.Confirm(old,old.Fingerprint);
    File.WriteAllText(Path.Combine(oldRegistered.Collection.StorageLocator!,"额外文件不自动入海报.txt"),"显式认领只补归属，不自动导入内容");
    File.Move(Path.Combine(legacy,".desktopup-storage-owner.json"),Path.Combine(root,"old-root-owner.keep"));
    File.Move(Path.Combine(oldRegistered.Collection.StorageLocator!,".desktopup-collection-owner.json"),Path.Combine(root,"old-child-owner.keep"));
    string sources=Path.Combine(root,"自建原文件");Directory.CreateDirectory(sources);
    var journal=new SqliteTransferJournal(store);
    foreach(string name in new[]{"已移动待显示.txt","两处保留待停止.txt"})
    {
        string source=Path.Combine(sources,name);File.WriteAllText(source,"本次自建测试文件，不是用户文件："+name);
        var p=LocalTransferPreview.Prepare(repository.Read(),BuiltInStateIdentities.PrimaryPosterId,ContentDropRoute.IntoManagedStorage,
            [(LocalContentReferenceProbe.Inspect(source).Item!,Path.Combine(registered.Collection.StorageLocator!,name),(Guid?)null)],registered.Root);
        journal.Begin(p.Plan,p.Files,p.Plan.Fingerprint,registered.Collection.Id);
        if(name=="已移动待显示.txt")
        {
            try{new LocalTransferExecutor(journal).Execute(p.Plan.Request.OperationId,checkpoint:(at,_)=>
            {if(at==TransferExecutionCheckpoint.AfterMove)throw new TransferInterruptionException("隔离验收：文件已移动，索引未完成");});}
            catch(TransferInterruptionException){}
        }
        else
        {File.WriteAllText(p.Files[0].Destination,"自建冲突目标，停止重试也不能删除这份文件");new LocalTransferExecutor(journal).Execute(p.Plan.Request.OperationId);}
    }
    string gap=Path.Combine(root,"创建中断待停止");Directory.CreateDirectory(gap);var interrupted=provisioner.Preview(gap);
    try{provisioner.Confirm(interrupted,interrupted.Fingerprint,at=>{if(at==StorageProvisionCheckpoint.DirectoryCreated)throw new TransferInterruptionException("isolated mkdir gap");});}
    catch(TransferInterruptionException){}
    File.WriteAllText(Path.Combine(interrupted.CollectionPath,"保留这份内容.txt"),"停止目录创建不得删除或认领它");
    Console.WriteLine(Path.Combine(root,"profile"));return 0;
}

if(args is ["--prepare-review",var newRoot])
{
    string repo=AppContext.BaseDirectory;
    while(!File.Exists(Path.Combine(repo,"DesktopUp.sln")))repo=Path.GetDirectoryName(repo)??throw new IOException("Repository fixture root missing.");
    string root=LocalTransferFiles.Normalize(newRoot),allowed=LocalTransferFiles.Normalize(Path.Combine(repo,"out","transfer-recovery"));
    if(!LocalTransferFiles.Contains(allowed,root)||Directory.Exists(root)||File.Exists(root))return 64;
    Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,".transfer-fixture-owner"),"owned");
    using var store=new SqliteStateStore(new UserDataPaths(Path.Combine(root,"profile")));
    if(!store.Initialize().Succeeded)return 65;
    var repository=new SqliteStateCommandRepository(store);
    var state=repository.EnsureInitialized(PosterConfigurationFactory.NewBasicBox("恢复入口隔离验证","DISPLAY1") with {PosterId=BuiltInStateIdentities.PrimaryPosterId});
    string storage=Path.Combine(root,"storage");Directory.CreateDirectory(storage);
    var provisioner=new ManagedStorageProvisioner(store);var preview=provisioner.Preview(storage);var registered=provisioner.Confirm(preview,preview.Fingerprint);
    string sourceDir=Path.Combine(root,"owned-source");Directory.CreateDirectory(sourceDir);
    var journal=new SqliteTransferJournal(store);
    foreach(string name in new[]{"可重试.txt","可结束.txt","两处保留.txt"})
    {
        string source=Path.Combine(sourceDir,name);File.WriteAllText(source,"DesktopUp 自建隔离测试内容，不是用户文件："+name);
        string target=Path.Combine(registered.Collection.StorageLocator!,name);
        var plan=LocalTransferPreview.Prepare(repository.Read(),BuiltInStateIdentities.PrimaryPosterId,ContentDropRoute.IntoManagedStorage,
            [(LocalContentReferenceProbe.Inspect(source).Item!,target,(Guid?)null)],registered.Root);
        journal.Begin(plan.Plan,plan.Files,plan.Plan.Fingerprint,registered.Collection.Id);
        if(name=="两处保留.txt")File.WriteAllText(target,"自建冲突目标：不可覆盖或删除");
    }
    new LocalTransferExecutor(journal).Recover();
    Console.WriteLine(Path.Combine(root,"profile"));return 0;
}

if(args is ["--adoption-crash",var adoptionRoot,var collectionId,var adoptionBoundary])
{
    string root=LocalTransferFiles.Normalize(adoptionRoot);
    if(!File.Exists(Path.Combine(root,".transfer-fixture-owner"))||File.ReadAllText(Path.Combine(root,".transfer-fixture-owner"))!="owned")return 60;
    using var store=new SqliteStateStore(new UserDataPaths(Path.Combine(root,"profile")));
    if(!store.Initialize().Succeeded)return 61;
    var registered=new SqliteTransferJournal(store).StorageLocations();
    if(registered.Any(x=>!LocalTransferFiles.Contains(root,x.Root.Locator)||!LocalTransferFiles.Contains(root,x.Collection.StorageLocator!)))return 62;
    var service=new ManagedStorageAdoption(store);var p=service.Preview(Guid.Parse(collectionId));
    var point=Enum.Parse<StorageAdoptionCheckpoint>(adoptionBoundary);
    service.Confirm(p,p.Fingerprint,at=>{if(at==point)Environment.Exit(74);});return 63;
}

if (args is ["--transfer-crash", var fixtureRoot, var operation, var boundary])
{
    string root=Path.GetFullPath(fixtureRoot);
    if(!File.Exists(Path.Combine(root,".transfer-fixture-owner")) ||
        File.ReadAllText(Path.Combine(root,".transfer-fixture-owner"))!="owned") return 60;
    using var store=new SqliteStateStore(new UserDataPaths(Path.Combine(root,"profile")));
    if(!store.Initialize().Succeeded)return 61;
    var journal=new SqliteTransferJournal(store);
    if(journal.Read().Any(row=>!LocalTransferFiles.Contains(root,row.File.Source)||!LocalTransferFiles.Contains(root,row.File.Destination)))return 62;
    var point=Enum.Parse<TransferExecutionCheckpoint>(boundary);
    new LocalTransferExecutor(journal).Execute(Guid.Parse(operation),checkpoint:(at,_)=>
    { if(at==point)Environment.Exit(73); });
    return 63;
}
return 2;
