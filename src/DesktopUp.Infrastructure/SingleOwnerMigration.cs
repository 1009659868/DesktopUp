using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

public static class SingleOwnerMigration
{
    public static int Apply(SqliteStateStore store)
    {
        var repository=new SqliteStateCommandRepository(store); var before=repository.Read();
        var duplicates=ContentSingleOwnerPolicy.DuplicateCards(before.ContentCatalog,before.Catalog.Posters.Select(p=>p.Poster.PosterId).ToArray());
        if(duplicates.Count==0)return 0;
        // Full DB backup includes old relations, authored layouts and icon bytes. No source file changes.
        var backup=store.CreateBackup("before-single-owner");
        if(!backup.Succeeded)throw new IOException("旧共享关系备份失败，未迁移。");
        var plan=ContentRelationRemoval.Prepare(before,duplicates.Select(p=>p.Id).ToArray());
        var reads=new StateReadSet(plan.Updates.ToDictionary(p=>p.PosterId,p=>before.Catalog.Posters.Single(x=>x.Poster.PosterId==p.PosterId).EntityVersion),
            ContentVersion:before.ContentVersion,CatalogVersion:before.Catalog.CatalogVersion);
        var result=repository.Execute(new("single-owner-"+Guid.NewGuid().ToString("N"),StateRequestFingerprint.Compute(duplicates),before.Revision,reads),_=>plan);
        if(result.Status!=StateCommandStatus.Committed)throw new IOException(result.Error??"唯一归属迁移未提交。");
        return duplicates.Count;
    }
}
