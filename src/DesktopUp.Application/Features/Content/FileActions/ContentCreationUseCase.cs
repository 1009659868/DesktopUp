namespace DesktopUp.Application;

// Validation, mapped/managed routing and completion policy are independent of storage.
public sealed class ContentCreationUseCase(IContentCreationStorage storage, IContentActionFiles files,
    Action<string, string>? checkpoint = null)
{
    public string SuggestName(string directory, NewContentKind kind)
        => SuggestName(files, directory, kind);

    public static string SuggestName(IContentActionFiles files, string directory, NewContentKind kind)
    {
        string stem = kind switch { NewContentKind.Folder => "新建文件夹", NewContentKind.TextFile => "新建文本文档", NewContentKind.Shortcut => "新建快捷方式", _ => throw new IOException("不支持的新建类型。") };
        string extension = kind switch { NewContentKind.TextFile => ".txt", NewContentKind.Shortcut => ".lnk", _ => "" };
        directory = files.Normalize(directory);
        using var parent = files.LockDirectory(directory);
        for (int i = 1; i <= 1000; i++)
        {
            string name = stem + (i == 1 ? "" : $" ({i})") + extension;
            if (!files.Exists(Path.Combine(directory, name))) return name;
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
            string target = files.Normalize(shortcutTarget ?? "");
            _ = files.Identity(target); // Existing ordinary local file/folder; do not resolve/execute.
            bytes = files.EncodeShortcut(target);
        }
        if (location.Mapping is not null) return storage.CreateMapped(posterId, location, kind, name, bytes, token);
        using var attempt = storage.BeginAttempt(checkpoint);
        var prepared = attempt.Prepare(posterId, location, kind, name, bytes, token);
        checkpoint?.Invoke("Journaled", prepared.Source);
        var finish = attempt.Complete(prepared.OperationId, token);
        if (finish != ContentCreationFinish.Completed)
            throw new IOException(finish == ContentCreationFinish.NotMoved ? "本次新建未完成，未添加卡片、未覆盖现有内容，可直接重试。" :
                $"新建结果暂未确认；文件保留，未覆盖现有内容。暂存：{prepared.Source}\n目标：{prepared.Destination}");
        return prepared.Destination;
    }
}
