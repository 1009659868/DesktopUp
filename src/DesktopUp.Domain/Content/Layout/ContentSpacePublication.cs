namespace DesktopUp.Domain;

/// <summary>RAM-only continuity of the last fully published surface. Preparing a candidate,
/// a partial native batch, or a stale callback can never grant overflow admission.</summary>
public sealed class ContentSpacePublication
{
    public ContentSpaceDecision? Published { get; private set; }

    public bool TryPublish(ContentSpaceDecision? candidate, ContentSpaceWitness? latest, bool wholeSurfaceVerified)
    {
        if (!wholeSurfaceVerified || candidate is not { Disposition: ContentSpaceDisposition.Publish } ||
            latest is null || candidate.Witness != latest) return false;
        if (Published is { } old &&
            (old.Witness.PosterId != latest.PosterId || old.Witness.SurfaceSessionId != latest.SurfaceSessionId ||
             old.Witness.Environment.RuntimeSessionId != latest.Environment.RuntimeSessionId ||
             latest.DocumentVersion < old.Witness.DocumentVersion || latest.ItemSetVersion < old.Witness.ItemSetVersion ||
             latest.LayoutGeneration < old.Witness.LayoutGeneration || latest.ViewportGeneration < old.Witness.ViewportGeneration ||
             latest.Environment.DisplayGeneration < old.Witness.Environment.DisplayGeneration ||
             latest.Environment.ShellGeneration < old.Witness.Environment.ShellGeneration)) return false;
        Published = candidate;
        return true;
    }
}
