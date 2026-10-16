using DesktopUp.Domain;

namespace DesktopUp.Application;

// Shared active-layout admission for runtime proposals and author-edit previews.
public static class ContentProjectionValidation
{
    public static void RequireWritable(ContentLayoutDocument document, ContentLayoutViewport viewport)
    {
        var snapshot = ContentLayoutSolver.Resolve(document, viewport);
        if (snapshot.IsLayoutReadOnly)
            throw new ContentLayoutException("当前内容空间不支持这次主动布局变化：" + snapshot.SpaceStatus);
    }
}
