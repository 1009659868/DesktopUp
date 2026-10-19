using DesktopUp.Application;
using DesktopUp.Infrastructure;

namespace DesktopUp;

public partial class App
{
    private ContentReturnUseCase? _contentReturns;
    private ContentReturnUseCase ContentReturns => _contentReturns ??= CreateContentReturns();

    private ContentReturnUseCase CreateContentReturns()
    {
        var store = _composition!.Store;
        return new(new SqliteContentReturnStorage(store), new LocalContentReturnFiles(store), new ShellDirectoryTransfer());
    }

    private UninstallPreparation CreateUninstallPreparation()
    {
        var store = _composition!.Store;
        var paths = _composition!.Paths;
        return new(new LocalUninstallProfile(store, paths), ContentReturns,
            new UninstallContentUseCase(new SqliteContentReturnStorage(store), ContentReturns));
    }
}
