using DesktopUp.Windows;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

// Shell has already created the object. Publish its exact observed identity without
// copying, moving, or deleting it. A failed publication leaves the real object intact.
public sealed class ShellCreatedContentService(SqliteStateStore store)
{
    public void Register(string posterId, NewContentLocation location, string directoryIdentity, ShellCreatedItem created)
    {
        var request = new ShellUiRequest(Guid.NewGuid(), ShellUiAction.DirectoryMenu, location.Directory, 0, 0);
        if (!ShellUiProtocol.Valid(request, new(request.Id, true, false) { CreatedItem = created }))
            throw new IOException("系统新建结果不属于本海报目录。");
        using var executor = new SqliteTransferJournal(store).AcquireExecutor();
        using var parent = LocalTransferFiles.LockDirectory(location.Directory);
        using var itemPin = new LocalTransferFiles.PinnedObject(created.Path, delete: false);
        if (LocalTransferFiles.Identity(location.Directory) != directoryIdentity || LocalTransferFiles.Identity(created.Path) != created.Identity)
            throw new IOException("新建对象或目录已被替换；未更改文件。");
        var state = new SqliteStateCommandRepository(store).Read();
        var poster = state.Catalog.Posters.SingleOrDefault(p => p.Poster.PosterId == posterId)
            ?? throw new IOException("海报已关闭；新建对象仍保留在目录中。");
        if (poster.Poster.IsLocked) throw new IOException("海报已锁定；新建对象仍保留在目录中。");
        if (location.Mapping is { } expected)
        {
            var mapped = new MappedDirectoryService(store);
            if (mapped.ResolveNewContent(posterId) != expected) throw new IOException("映射目录已变化。");
            mapped.CommitIncoming(posterId, [created.Path], null, poster.EntityVersion);
            return;
        }
        var storage = location.Storage ?? throw new IOException("收纳位置缺失。");
        ManagedStorageProvisioner.ValidateRegistration(store, storage.Root, storage.Collection);
        var observed = LocalContentReferenceProbe.Inspect(created.Path).Item ?? throw new IOException("新建对象暂不可读取。");
        if (store.ProtectedTransferPath(created.Path)) throw new IOException("不能把受保护路径作为新建结果。");
        var plan = ContentFileActionPlans.ShellCreated(state, posterId, poster.EntityVersion, storage.Collection.Id, observed, created);
        if (plan is null) return;
        var result = store.ExecuteWrite((c,t) =>
        {
            var committed = SqliteStateCommandRepository.ExecuteInTransaction(c,t,
                plan.Request, _ => plan.Mutation);
            if (committed.Status == StateCommandStatus.Committed)
                ManagedContentObservationStore.Capture(c,t,committed.Snapshot.ContentCatalog,plan.ObservedIds);
            return committed;
        });
        if (result.Status != StateCommandStatus.Committed) throw new IOException(result.Error);
    }
}
