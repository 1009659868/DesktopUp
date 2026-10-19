namespace DesktopUp.Application;

// Shared by first collection, global settings and startup. All IO is delegated; no UI or scheduler.
public sealed class StorageLocationUseCase(IStorageLocationStorage storage, StorageRelocationUseCase relocation,
    Action<string>? creationCheckpoint = null)
{
    public string? StartupNotice { get; private set; }
    public string ProposedDefaultPath
    {
        get
        {
            var context = storage.DefaultContext();
            return ManagedStorageLocationPolicy.ProposeDefaultPath(context.ProfileIdentity, context.Kind,
                context.LocalApplicationData, context.IsolatedStorageBase, context.ProtectedDirectories);
        }
    }

    public StorageLocationView Read()
    {
        var selection = storage.ReadLocation();
        var locations = storage.Locations();
        if (selection.Selection.ActiveRootId is { } id)
        {
            var owned = locations.Where(x => x.Root.Id == id).ToArray();
            if (owned.Length == 0) throw new IOException("原收纳位置未登记，未切换到其他目录。");
            storage.ValidateAvailable(owned);
            return new(owned[0].Root.Locator, true, false, locations, StartupNotice);
        }
        var roots = locations.Select(x => x.Root.Id).Distinct().ToArray();
        if (roots.Length == 0) return new(ProposedDefaultPath, false, false, locations, StartupNotice);
        if (roots.Length == 1)
        {
            storage.ValidateAvailable(locations);
            return new(locations[0].Root.Locator, true, false, locations, StartupNotice);
        }
        return new("存在多个旧位置，请明确选择用于新增收纳的位置", false, true, locations, StartupNotice);
    }

    public void SelectExisting(Guid rootId) => storage.Select(rootId, storage.ReadLocation());

    // Viewing settings is read-only; only explicit first use creates a default root.
    public RegisteredManagedStorage EnsureReady(CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var view = Read();
        if (view.NeedsSelection)
            throw new IOException("请先在全局收纳位置中选择已有目录；不会自动合并旧位置。");
        if (view.Choices.Count == 0)
        {
            string root = ProposedDefaultPath;
            storage.PrepareDefaultDirectory(root, cancellation);
            var preview = storage.PreviewProvision(root);
            cancellation.ThrowIfCancellationRequested();
            storage.ConfirmProvision(preview, preview.Fingerprint);
            creationCheckpoint?.Invoke("Registered");
        }
        var state = storage.ReadLocation();
        var all = storage.Locations();
        var chosen = state.Selection.ActiveRootId ?? all.Select(x => x.Root.Id).Distinct().Single();
        storage.Select(chosen, state);
        var active = all.Where(x => x.Root.Id == chosen).OrderBy(x => x.Collection.Id).ToArray();
        storage.ValidateOwnership(active);
        return active[0];
    }

    public StorageChangePreview PreviewChange(string destination)
    {
        var location = storage.ReadLocation();
        if (storage.Locations().Length == 0)
            return new(null, storage.PreviewEmptyProvision(destination), location, 0, 0, 0, []);

        var plan = storage.PreviewRelocation(destination);
        var payload = storage.DescribePayload(plan);
        return new(plan, null, location, payload.Files, payload.Directories, payload.Bytes, payload.Posters);
    }

    public StorageChangeResult Change(StorageChangePreview preview, string confirmation,
        CancellationToken cancellation = default)
    {
        if (confirmation != preview.Fingerprint) throw new IOException("请确认本次位置预览。");
        cancellation.ThrowIfCancellationRequested();
        if (preview.Provision is { } provision)
        {
            if (storage.ReadLocation() != preview.Location || storage.Locations().Length != 0)
                throw new IOException("收纳目录已经变化，请重新预览。");
            using var target = storage.LockEmptyDestination(preview.Destination);
            var result = storage.ConfirmProvision(provision, confirmation);
            // Durable registration survives interrupted selection; next use selects the sole root.
            storage.Select(result.Root.Id, storage.ReadLocation());
            return new(true, true, [], "收纳位置已设置；没有文件需要迁移。");
        }

        var copied = relocation.Execute(preview.Plan!, confirmation, cancellation);
        return new(copied.Committed, copied.Cleaned, copied.RetainedPaths,
            copied.Committed
                ? (copied.Cleaned ? "收纳位置已更改，内容已迁移。" : "新位置已生效；部分旧副本保留在下列位置。")
                : (copied.Cleaned ? "未更改位置；原内容保留，可直接重试。" : "未更改位置；无法核实的副本已保留，请查看下列位置。"));
    }

    public IReadOnlyList<StorageChangeResult> Reconcile()
    {
        var results = new List<StorageChangeResult>();
        foreach (var row in storage.Relocations().Where(x => x.Closure is null && x.Phase != StorageRelocationPhase.Completed))
        {
            var done = relocation.Reconcile(row.OperationId);
            results.Add(new(done.Committed, done.Cleaned, done.RetainedPaths,
                done.Committed ? "上次更改已生效。" : "上次更改未提交，原位置保持。"));
        }
        StartupNotice = results.Count == 0 ? null : string.Join("\n", results.Select(x =>
            x.Message + (x.Cleaned ? "" : " 保留的文件位置：\n" + string.Join("\n", x.RetainedPaths))));
        return results;
    }
}
