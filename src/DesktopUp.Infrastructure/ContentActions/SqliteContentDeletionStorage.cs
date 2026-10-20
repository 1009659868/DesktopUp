using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

public sealed class SqliteContentDeletionStorage(SqliteStateStore store) : IContentDeletionStorage
{
    public IContentDeletionAttempt BeginAttempt() => new Attempt(store);
    private sealed class Attempt(SqliteStateStore store) : IContentDeletionAttempt
    {
        private readonly NativeShellExitJournal journal = new(store);
        private ShellExitIntent? intent;
        private long posterVersion;
        public IDisposable AcquireExecutor() => new SqliteTransferJournal(store).AcquireExecutor();
        public IReadOnlyList<string> Prepare(string posterId, IReadOnlyList<string> itemIds)
        {
            var before = new SqliteStateCommandRepository(store).Read();
            var poster = before.Catalog.Posters.Single(p => p.Poster.PosterId == posterId);
            if (poster.Poster.IsLocked) throw new IOException("海报已锁定，未删除。");
            posterVersion = poster.EntityVersion;
            intent = journal.Begin(posterId, itemIds, explicitDelete: true);
            return intent.Items.Select(i => i.Path).ToArray();
        }
        public void AuthorizeDelete() => journal.AuthorizeDelete(intent!);
        public bool IsCurrent(string path)
        {
            var item = intent!.Items.Single(i => i.Path == path);
            if (LocalTransferFiles.Identity(Path.GetDirectoryName(item.Path)!) != item.ParentIdentity ||
                LocalTransferFiles.Identity(item.Path) != item.Identity) return false;
            var now = new SqliteStateCommandRepository(store).Read();
            return now.Catalog.Posters.Any(p => p.Poster.PosterId == intent.PosterId && !p.Poster.IsLocked && p.EntityVersion == posterVersion) &&
                now.ContentCatalog.PosterItems.Any(p => p.Id == item.CardId && p.PosterId == intent.PosterId) &&
                now.ContentCatalog.Items.Any(i => i.Id == item.ContentId && i.Version == item.Version && i.Target.Locator == item.Path);
        }
        public int ObserveAndClose()
        {
            if (intent is null) return 0;
            int removed = journal.ObserveReleasedMove(intent);
            journal.Close(intent);
            return removed;
        }
    }
}
