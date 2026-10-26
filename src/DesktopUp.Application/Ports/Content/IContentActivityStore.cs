using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record ContentActivityBatch(long Position, int Consumed, bool HasMore,
    IReadOnlyList<Guid> ChangedContentIds);
public sealed record ContentActivitySnapshot(long Position, bool HasPending,
    IReadOnlyDictionary<Guid, ContentActivityUsage> Items);

public interface IContentActivityStore
{
    ContentActivityBatch ConsumeNext(int maximumFacts = 128);
    ContentActivitySnapshot Read(IReadOnlyCollection<Guid> contentIds);
}
