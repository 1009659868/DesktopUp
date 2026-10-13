using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

public sealed record LocalTransferPreview(ContentTransferPlan Plan, IReadOnlyList<LocalTransferFilePlan> Files)
{
    // Native adapters are NOT connected. Tests explicitly name an isolated target instead of
    // pretending a fabricated directory is the user's real desktop.
    public static LocalTransferPreview Prepare(DesktopStateSnapshot state, string posterId,
        ContentDropRoute route, IReadOnlyList<(ContentItem Item, string Destination, Guid? OriginCard)> inputs,
        ManagedStorageRoot? root = null, bool isolatedDestination = false,
        IncomingContentPlacement? placement = null, bool keepSourceUntilCommit = false)
    {
        var files = inputs.Select(i => LocalTransferFiles.Inspect(i.Item.Target.Locator, i.Destination,
            allowCrossVolumeTree:keepSourceUntilCommit)).ToArray();
        var entries = inputs.Select((input,index) =>
        {
            var f=files[index];
            if(input.Item.Source.ProviderId!="local-file-reference-v1")throw new IOException("Unsupported provider.");
            var item=input.Item with {Capabilities=input.Item.Capabilities|ContentCapability.Move};
            var descendants=state.ContentCatalog.Items.Where(i=>i.Source.ProviderId==item.Source.ProviderId &&
                LocalTransferFiles.Contains(f.Source,i.Target.Locator)).Select(i=>i.Id).ToArray();
            bool desktop=route!=ContentDropRoute.ToDesktop || isolatedDestination ||
                string.Equals(Path.GetDirectoryName(f.Destination),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),StringComparison.OrdinalIgnoreCase);
            return new ContentTransferEntry(item,new(f.Objects[0].Identity,f.Source,LocalTransferFiles.TreeHash(f.Objects),
                LocalTransferFiles.Ancestors(f.Source),descendants,state.ContentVersion,true),
                new(f.ParentIdentity,f.ParentIdentity,f.Destination,LocalTransferFiles.Ancestors(f.Destination),
                    string.Equals(f.Source,f.Destination,StringComparison.OrdinalIgnoreCase)?f.Objects[0].Identity:null,desktop,true),input.OriginCard);
        }).ToArray();
        var request=new ContentTransferRequest(Guid.NewGuid(),1,posterId,route,ContentDropModifiers.None,root,entries)
            { Placement=placement, KeepSourceUntilCommit=keepSourceUntilCommit };
        var result=ContentTransferPlanning.Prepare(request,SqliteTransferJournal.Snapshot(state,posterId));
        return new(result.Plan??throw new IOException(result.Reason),files);
    }
}
