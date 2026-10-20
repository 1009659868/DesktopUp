namespace DesktopUp.Application;

// Keeps poster definitions and borrowed/external content for a possible reinstall.
public sealed class UninstallContentUseCase(IContentReturnStorage storage, ContentReturnUseCase returns)
{
    // Preserve mode must not enumerate a possibly offline disk or settle file work.
    public IReadOnlyList<string> RetainedLocations() => storage.RetainedLocations();

    public UninstallContentInventory Inspect()
    {
        using var lease = storage.AcquireExecution();
        return storage.Inventory();
    }

    public async Task<UninstallContentResult> Return(UninstallContentInventory preview, nint owner,
        CancellationToken token = default, Action<string>? progress = null)
    {
        using var lease = storage.AcquireExecution();
        storage.BackfillOrigins();
        var initial = storage.Inventory();
        if (StateRequestFingerprint.Compute(initial) != StateRequestFingerprint.Compute(preview))
            return new(false, [], initial.Locations, ["内容已变化，请重新卸载；文件未移动。"]);

        var returned = new List<string>();
        var errors = new List<string>();
        var expected = storage.ReadState();
        bool Current() => !token.IsCancellationRequested && storage.ReadState().Revision == expected.Revision;

        foreach (var source in initial.Files)
        {
            try
            {
                if (!Current()) throw new IOException("已停止归还或配置变化；程序与剩余文件保留。");
                progress?.Invoke(source.Path);
                var result = await returns.ReturnOne(ContentReturnIntent.UninstallScope, source,
                    expected.ContentCatalog, Current, owner, token).ConfigureAwait(false);
                expected = result.State;
                returned.Add(result.Path);
                if (result.Stopped || token.IsCancellationRequested)
                    throw new IOException("已取消；已归还文件保持原位，剩余文件保留。");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                errors.Add(e.Message);
                break;
            }
        }

        try
        {
            var remaining = storage.Inventory();
            if (remaining.Files.Count > 0 && errors.Count == 0)
                errors.Add("收纳目录仍有内容；停止卸载，未删除任何剩余文件。");
            if (!Current() && errors.Count == 0)
                errors.Add("配置变化或操作已取消，停止卸载。");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            errors.Add(e.Message);
        }
        return new(errors.Count == 0, returned, initial.Locations, errors);
    }
}
