using DesktopUp.Domain;

namespace DesktopUp.Application;

/// <summary>Capabilities of known intent on its canonical logical board. These are not a current
/// viewport permission, an editing lease or authorization to convert legacy constraints.</summary>
public sealed record ContentLayoutCapabilityReport(ContentLayoutSpaceStatus SpaceStatus,
    bool CanArchive, bool CanInstantiate, bool CanAuthorOnCanonicalBoard, string? Restriction);

public static class ContentLayoutCapabilities
{
    /// <summary>Invalid/unknown data and unsolvable geometry are rejected. A known, bounded legacy
    /// layout can remain losslessly archiveable while it is unavailable for new application/editing.</summary>
    public static ContentLayoutCapabilityReport Inspect(ContentLayoutDocument document, double scale = 1, double spacing = 1)
    {
        _ = ContentLayoutPolicy.Serialize(document);
        double crossDip = document.MaximumColumns * (document.BaseCellDip * scale + document.BaseGapDip * spacing)
            - document.BaseGapDip * spacing;
        var viewport = document.FlowAxis == ContentFlowAxis.Vertical
            ? new ContentLayoutViewport(crossDip, 1024, Scale: scale, Spacing: spacing)
            : new ContentLayoutViewport(1024, crossDip, Scale: scale, Spacing: spacing);
        var snapshot = ContentLayoutSolver.Resolve(document, viewport);
        bool applicable = !snapshot.IsLayoutReadOnly;
        return new(snapshot.SpaceStatus, true, applicable, applicable,
            applicable ? null : "Known content can be archived losslessly but cannot currently be applied or authored: " + snapshot.SpaceStatus);
    }

    public static void RequireApplicable(ContentLayoutDocument document, double scale = 1, double spacing = 1)
    {
        var capability = Inspect(document, scale, spacing);
        if (!capability.CanInstantiate || !capability.CanAuthorOnCanonicalBoard)
            throw new ArgumentException(capability.Restriction);
    }
}
