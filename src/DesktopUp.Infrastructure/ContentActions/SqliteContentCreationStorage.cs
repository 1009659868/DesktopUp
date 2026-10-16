using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

// Keeps staged-file identity, durable intent and existing transfer transaction together.
public sealed class SqliteContentCreationStorage(SqliteStateStore store) : IContentCreationStorage
{
    public IContentCreationAttempt BeginAttempt(Action<string, string>? checkpoint) => new Attempt(store, checkpoint);
    public string CreateMapped(string posterId, NewContentLocation location, NewContentKind kind,
        string name, byte[] bytes, CancellationToken token) =>
        new MappedDirectoryService(store).CreateNew(posterId, location, kind, name, bytes, token);

    private sealed class Attempt(SqliteStateStore store, Action<string, string>? checkpoint) : IContentCreationAttempt
    {
        private readonly SqliteTransferJournal journal = new(store);
        private string? stage, source;
        private LocalTransferObject? directory, created;
        private bool mayClean = true;
        private LocalTransferPreview preview = null!;
        public PreparedContentCreation Prepare(string posterId, NewContentLocation location, NewContentKind kind,
            string name, byte[] bytes, CancellationToken token)
        {
            using (journal.AcquireExecutor())
            {
                var before = new SqliteStateCommandRepository(store).Read();
                var owner = before.Catalog.Posters.SingleOrDefault(p => p.Poster.PosterId == posterId);
                if (before.ContentVersion != location.ContentVersion || owner?.EntityVersion != location.PosterVersion)
                    throw new IOException("新建窗口打开后内容或海报已变化，请重新新建。");
                if (before.ContentCatalog.Bindings.Any(b => b.PosterId == posterId &&
                    before.ContentCatalog.Collections.Any(c => c.Id == b.CollectionId && c.Kind == ContentCollectionKind.Mapped)))
                    throw new IOException("映射目录的新建尚未开放。");
                if (journal.Read().Any(r => !r.IsFinished) || new SqliteStorageRelocationJournal(store).Read().Any(r => r.Commit is null && r.Closure is null))
                    throw new IOException("请先等待当前文件操作完成。");
                var storage = location.Storage ?? throw new IOException("收纳位置缺失。");
                ManagedStorageProvisioner.ValidateRegistration(store, storage.Root, storage.Collection);
                string destination = LocalTransferFiles.Normalize(Path.Combine(storage.Collection.StorageLocator!, name));
                if (store.ProtectedTransferPath(destination) || LocalTransferFiles.Exists(destination))
                    throw new IOException("该名称已存在或路径受保护，请换一个名称。未覆盖任何文件。");
                token.ThrowIfCancellationRequested();
                stage = Path.Combine(storage.Root.Locator, ".desktopup-new-" + Guid.NewGuid().ToString("N"));
                directory = LocalTransferFiles.CreateOwnedDirectory(stage);
                using var stagingLease = LocalTransferFiles.LockDirectory(stage);
                if (LocalTransferFiles.Identity(stage) != directory.Identity) throw new IOException("新建暂存目录已变化。");
                source = Path.Combine(stage, name);
                if (kind == NewContentKind.Folder) created = LocalTransferFiles.CreateOwnedDirectory(source);
                else created = LocalTransferFiles.CreateOwnedFile(source, bytes);
                checkpoint?.Invoke("Created", source);
                token.ThrowIfCancellationRequested();
                preview = DirectTransferPreparation.Incoming(before, posterId, [source], storage);
                if (preview.Files[0].Objects.Count != 1 || preview.Files[0].Objects[0] != created)
                    throw new IOException("新建暂存内容被外部改变，已保留，未收纳：" + source);
                try { journal.Begin(preview.Plan, preview.Files, preview.Plan.Fingerprint, storage.Collection.Id); }
                catch
                {
                    // A failed commit acknowledgement is not proof the intent was absent.
                    try { mayClean = !journal.Read().Any(r => r.OperationId == preview.Plan.Request.OperationId); }
                    catch { mayClean = false; }
                    throw;
                }
                // Once durable, only the existing transfer reconciler may decide its fate.
                mayClean = false;
            }
            return new(preview.Plan.Request.OperationId, source!, preview.Files[0].Destination);
        }
        public ContentCreationFinish Complete(Guid operationId, CancellationToken token)
        {
            var finish = new AutomaticTransferCompletion(journal).Execute(operationId, token).Single();
            mayClean = finish.Kind != TransferFinishKind.Preserved;
            return finish.Kind switch
            {
                TransferFinishKind.Completed => ContentCreationFinish.Completed,
                TransferFinishKind.NotMoved => ContentCreationFinish.NotMoved,
                _ => ContentCreationFinish.Preserved
            };
        }
        public void Dispose()
        {
            // Identity AND bytes/emptiness must still match. Never delete a changed or replaced object.
            if (mayClean && source is not null && created is not null && stage is not null && directory is not null)
            {
                try
                {
                    using var parent = LocalTransferFiles.LockDirectory(stage);
                    if (LocalTransferFiles.Identity(stage) == directory.Identity) ContentCreationService.RemoveExactEmptyOrFile(source, created);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
            if (stage is not null && directory is not null) ContentCreationService.RemoveExactEmptyOrFile(stage, directory);
        }
    }
}
