using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record LocalReferencePreview(string Input, ContentItem? Item, string Message,
    long? Length = null, long? LastWriteTicks = null);

/// <summary>Metadata-only source inspection and revalidation; never executes, moves or enumerates content.</summary>
public interface IContentReferenceSource
{
    LocalReferencePreview Inspect(string path);
    bool IsCurrent(LocalReferencePreview preview);
}
