using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

public enum NewContentKind { Folder, TextFile, Shortcut }
public sealed record NewContentLocation(RegisteredManagedStorage? Storage, long ContentVersion, long PosterVersion)
{
    public MappedNewContent? Mapping { get; init; }
    public string Directory => Mapping?.Path ?? Storage!.Collection.StorageLocator!;
}

// Newly generated objects enter the SAME durable move/index pipeline as drag-in.
// Temporary staging contains no user's source files. Never overwrite a collision or
// recursively clean a directory; uncertain files remain with an explicit location.
public sealed class ContentCreationService(SqliteStateStore store)
{
    internal Action<string, string>? Checkpoint { get; set; }
    public static string SuggestName(string directory, NewContentKind kind)
    {
        string stem = kind switch { NewContentKind.Folder => "新建文件夹", NewContentKind.TextFile => "新建文本文档", NewContentKind.Shortcut => "新建快捷方式", _ => throw new IOException("不支持的新建类型。") };
        string extension = kind switch { NewContentKind.TextFile => ".txt", NewContentKind.Shortcut => ".lnk", _ => "" };
        directory = LocalTransferFiles.Normalize(directory);
        using var parent = LocalTransferFiles.LockDirectory(directory);
        for (int i = 1; i <= 1000; i++)
        {
            string name = stem + (i == 1 ? "" : $" ({i})") + extension;
            if (!LocalTransferFiles.Exists(Path.Combine(directory, name))) return name;
        }
        throw new IOException("默认名称已占用，请整理目录后重试。");
    }
    public string Create(string posterId, NewContentLocation location, NewContentKind kind, string name,
        string? shortcutTarget = null, CancellationToken token = default)
    {
        if (!Enum.IsDefined(kind)) throw new IOException("不支持的新建类型。");
        if (LocalFileNamePolicy.Error(name) is { } error) throw new IOException(error);
        if (kind == NewContentKind.Shortcut && !name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
            kind == NewContentKind.TextFile && !name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            throw new IOException("请保留此类型的扩展名（.lnk / .txt）。");
        token.ThrowIfCancellationRequested();
        byte[] bytes = [];
        if (kind == NewContentKind.Shortcut)
        {
            string target = LocalTransferFiles.Normalize(shortcutTarget ?? "");
            _ = LocalTransferFiles.Identity(target); // Existing ordinary local file/folder; do not resolve/execute.
            bytes = ShellShortcutFile.Encode(target);
        }
        if (location.Mapping is not null) return new MappedDirectoryService(store).CreateNew(posterId, location, kind, name, bytes, token);
        var journal = new SqliteTransferJournal(store);
        string? stage = null, source = null; LocalTransferObject? directory = null, created = null;
        bool mayClean = true;
        try
        {
            LocalTransferPreview preview;
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
                Checkpoint?.Invoke("Created", source);
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
            Checkpoint?.Invoke("Journaled", source);
            var finish = new AutomaticTransferCompletion(journal).Execute(preview.Plan.Request.OperationId, token).Single();
            mayClean = finish.Kind != TransferFinishKind.Preserved;
            if (finish.Kind != TransferFinishKind.Completed)
                throw new IOException(finish.Kind == TransferFinishKind.NotMoved ? "本次新建未完成，未添加卡片、未覆盖现有内容，可直接重试。" :
                    $"新建结果暂未确认；文件保留，未覆盖现有内容。暂存：{source}\n目标：{preview.Files[0].Destination}");
            return preview.Files[0].Destination;
        }
        finally
        {
            // Identity AND bytes/emptiness must still match. Never delete a changed or replaced object.
            if (mayClean && source is not null && created is not null && stage is not null && directory is not null)
            {
                try
                {
                    using var parent = LocalTransferFiles.LockDirectory(stage);
                    if (LocalTransferFiles.Identity(stage) == directory.Identity) RemoveExactEmptyOrFile(source, created);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
            if (stage is not null && directory is not null) RemoveExactEmptyOrFile(stage, directory);
        }
    }
    internal static void RemoveExactEmptyOrFile(string path, LocalTransferObject expected)
    {
        try
        {
            if (!LocalTransferFiles.Exists(path)) return;
            using var pin = new LocalTransferFiles.PinnedObject(path, delete: true);
            if (pin.Snapshot() != expected || expected.Directory && Directory.EnumerateFileSystemEntries(path).Any()) return;
            pin.Delete();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* Preserve unknown ownership/bytes. */ }
    }
}
