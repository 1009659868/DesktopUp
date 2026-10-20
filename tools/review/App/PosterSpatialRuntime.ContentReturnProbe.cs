using DesktopUp.Application;
using DesktopUp.Infrastructure;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    internal async Task ContentReturnProbe(string root, bool restart)
    {
        var repository = new SqliteStateCommandRepository(_reviewTransferStore!);
        string sources = Path.Combine(root, "return-sources"), file = Path.Combine(sources, "return.txt");
        if (!restart)
        {
            Directory.CreateDirectory(sources); File.WriteAllText(file, "owned content return fixture");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!IncomingProbeReady)
            { if(watch.Elapsed.TotalSeconds>20)throw new IOException("Return fixture window unavailable.");await Task.Delay(100); }
            await IncomingProbe([file], frame:true);
            var before = repository.Read(); var poster = before.Catalog.Posters.Single();
            var item = before.ContentCatalog.Items.Single();
            if(File.Exists(file) || _contentReturns!.OriginalPath(item.Id)!=file)throw new IOException("Original source not recorded at native ingress.");
            var command = new RemovePosterCommand("return-probe-"+Guid.NewGuid(),StateCommandSource.ControlCenter,before.Revision,
                StateReadSet.ForPoster(poster.Poster.PosterId,poster.EntityVersion) with {CatalogVersion=before.Catalog.CatalogVersion,ContentVersion=before.ContentVersion},poster.Poster.PosterId,true);
            var result = await ReturnAndRemovePoster(command,0);
            if(!result.Removed || result.Returned!=1)throw new IOException(string.Join(";",result.Errors));
        }
        if(repository.Read().Catalog.Posters.Count!=0 || repository.Read().ContentCatalog.PosterItems.Count!=0 ||
            File.ReadAllText(file)!="owned content return fixture")throw new IOException("Return/empty catalog not retained.");
        log.Write("info","contract.content_return_passed",new{restart,nativeMouse=false,originalPath=true,emptyCatalog=true});
    }
}
