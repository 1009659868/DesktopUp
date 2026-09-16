using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record ControlCenterPosterRow(string PosterId, string Title, string Summary, string RuntimeStatus)
{
    public string AccessibleName => $"{Title} · {Summary}";
}

// UI-session state only. It neither writes the database nor changes the catalog's compatibility alias.
public sealed class ControlCenterSelection
{
    public ControlCenterProjection? Projection { get; private set; }
    public string? SelectedPosterId { get; private set; }
    public string? Notice { get; private set; }

    public bool ApplyProjection(ControlCenterProjection projection)
    {
        if (Projection is not null && projection.Revision < Projection.Revision) return false;
        bool maySelectInitial = Projection is null || Projection.Posters.Count == 0;
        Projection = projection;
        if (SelectedPosterId is not null && !projection.PostersById.ContainsKey(SelectedPosterId))
        {
            SelectedPosterId = null;
            Notice = "所选海报已移除，请重新选择。";
        }
        else if (SelectedPosterId is null && maySelectInitial)
        {
            SelectedPosterId = projection.PostersById.ContainsKey(BuiltInStateIdentities.PrimaryPosterId)
                ? BuiltInStateIdentities.PrimaryPosterId : projection.Posters.FirstOrDefault()?.Poster.PosterId;
            Notice = null;
        }
        return true;
    }

    public bool Select(string posterId)
    {
        if (Projection?.PostersById.ContainsKey(posterId) != true) return false;
        SelectedPosterId = posterId;
        Notice = null;
        return true;
    }

    public ControlCenterView? BuildView(IReadOnlyList<PosterSessionSnapshot> sessions, bool environmentPending, bool globallyHidden = false)
    {
        if (Projection is not { } projection) return null;
        var runtime = sessions.ToDictionary(s => s.PosterId, StringComparer.Ordinal);
        var rows = projection.Posters.Select(p =>
        {
            string status = p.Poster.PlacementPending ? "等待安置" :
                runtime.GetValueOrDefault(p.Poster.PosterId)?.Status == PosterSessionStatus.Degraded ? "运行降级" :
                !p.Poster.IsVisible ? "已隐藏" : globallyHidden ? "全局临时隐藏" :
                runtime.GetValueOrDefault(p.Poster.PosterId)?.Status switch
                {
                    PosterSessionStatus.Degraded => "运行降级",
                    PosterSessionStatus.Ready when !environmentPending => "运行正常",
                    PosterSessionStatus.Ready => "恢复中",
                    PosterSessionStatus.Starting => "等待宿主",
                    _ => "等待宿主"
                };
            return new ControlCenterPosterRow(p.Poster.PosterId, p.Poster.Title,
                $"{(p.Poster.IsLocked ? "已锁定" : "未锁定")} · {(p.Poster.IsCollapsed ? "已收起" : "已展开")} · {status}", status);
        }).ToArray();
        PosterSurfaceProjection? selected = SelectedPosterId is not null
            ? projection.PostersById.GetValueOrDefault(SelectedPosterId) : null;
        return new(projection, selected, Array.AsReadOnly(rows), Notice) { GloballyHidden = globallyHidden };
    }
}

public sealed record ControlCenterView(ControlCenterProjection Projection, PosterSurfaceProjection? SelectedPoster,
    IReadOnlyList<ControlCenterPosterRow> Posters, string? Notice)
{
    public bool GloballyHidden { get; init; }
    public string? SelectedPosterId => SelectedPoster?.Poster.PosterId;

    // Freeze exactly the displayed ID, desired value and entity version before dispatch.
    // A later selection change or catalog refresh must never retarget this command.
    public PosterStateCommand? CreatePosterCommand(PosterStateCommandKind kind, string requestId)
    {
        if (SelectedPoster is not { } target) return null;
        if (kind == PosterStateCommandKind.SetCollapsed && target.Poster.PlacementPending) return null;
        PosterDefinition desired = kind switch
        {
            PosterStateCommandKind.SetLocked => target.Poster with { IsLocked = !target.Poster.IsLocked },
            PosterStateCommandKind.SetCollapsed => target.Poster with { IsCollapsed = !target.Poster.IsCollapsed },
            PosterStateCommandKind.SetVisible => target.Poster with { IsVisible = !target.Poster.IsVisible },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return new(requestId, StateCommandSource.ControlCenter, kind, Projection.Revision, target.Poster.PosterId, desired)
        { ReadSet = StateReadSet.ForPoster(target.Poster.PosterId, target.EntityVersion) };
    }
}
