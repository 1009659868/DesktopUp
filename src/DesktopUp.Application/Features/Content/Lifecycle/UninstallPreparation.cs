namespace DesktopUp.Application;

// A single maintenance invocation owns these report facts. The host owns UI and exit codes.
public sealed class UninstallPreparation(IUninstallProfile profile, ContentReturnUseCase returns,
    UninstallContentUseCase uninstall)
{
    private readonly List<string> _returned = [];
    private readonly List<string> _retained = [];
    public int ReturnedCount => _returned.Count;
    public int RetainedLocationCount => _retained.Count;
    public bool OpenExisting() => profile.OpenExisting();

    public async Task Execute(bool keep, bool silent, nint owner, CancellationToken token, Action<string>? progress = null)
    {
        _retained.AddRange(await Task.Run(uninstall.RetainedLocations));
        if (!keep)
        {
            var notices = await Task.Run(returns.Reconcile);
            if (notices.Count > 0) throw new IOException(string.Join("\n", notices));
            var inventory = await Task.Run(uninstall.Inspect);
            if (silent && inventory.Files.Count > 0)
                throw new IOException("静默卸载不移动收纳文件，请使用交互卸载，或明确保留原位。");

            var result = await Task.Run(() => uninstall.Return(inventory, owner, token, progress));
            _returned.AddRange(result.Returned);
            if (!result.Completed) throw new IOException(string.Join("\n", result.Errors));
        }
        token.ThrowIfCancellationRequested();
        profile.SaveReport(keep ? "保留原位" : "归还完成", _returned, _retained, []);
    }

    public void SaveFailureReport(string error) =>
        profile.SaveReport("未完成，程序及剩余文件保留", _returned, _retained, [error]);
}
