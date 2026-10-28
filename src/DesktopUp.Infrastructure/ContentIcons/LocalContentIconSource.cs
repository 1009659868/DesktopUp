using DesktopUp.Windows;
using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Infrastructure;

public sealed class LocalContentIconSource : IContentIconSource
{
    public ContentItem Inspect(string path)
    {
        var preview = LocalContentReferenceProbe.Inspect(path);
        return preview.Item ?? throw new InvalidDataException(preview.Message);
    }
}
