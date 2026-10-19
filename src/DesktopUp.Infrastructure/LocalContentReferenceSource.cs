using DesktopUp.Windows;
using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

public sealed class LocalContentReferenceSource : IContentReferenceSource
{
    public LocalReferencePreview Inspect(string path) => LocalContentReferenceProbe.Inspect(path);
    public bool IsCurrent(LocalReferencePreview preview) => LocalContentReferenceProbe.IsCurrent(preview);
}
