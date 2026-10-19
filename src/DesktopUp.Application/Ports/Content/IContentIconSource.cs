using DesktopUp.Domain;

namespace DesktopUp.Application;

/// <summary>Metadata-only inspection of a selected local image; invalid/unsafe sources throw without executing them.</summary>
public interface IContentIconSource
{
    ContentItem Inspect(string path);
}
