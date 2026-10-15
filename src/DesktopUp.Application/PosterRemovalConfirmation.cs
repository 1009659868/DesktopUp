namespace DesktopUp.Application;

public sealed record PosterRemovalRequest(string PosterId, long EntityVersion, StateCommandSource Source,
    PosterInputSessionKey? Origin = null, long OriginLayoutRevision = 0);
public sealed record PosterRemovalTicket(Guid Id, PosterRemovalRequest Request, PosterRemovalPreview Preview)
{
    public string Message => $"海报：{Preview.Title}\nID：{Preview.PosterId}\n\n{Preview.Consequences}\n\n" +
        "实际文件按上述规则处理；完成后仅移除此海报的展示配置与关联，不删除真实文件或已安装主题包。\n\n" +
        (Preview.IsLastPoster ? "这是最后一张海报。删除后下次启动仍为空目录，不会自动补建默认海报；控制中心和托盘继续运行。" : "其他海报不受删除影响。") +
        "\n此操作没有界面内撤销；如需恢复配置，请使用已有备份。";
}

// One explicit confirmation at a time, frozen identity/read set, single-use completion.
// File-task blocking must be connected when V3-05 introduces real file operations.
public sealed class PosterRemovalConfirmation
{
    public PosterRemovalTicket? Pending { get; private set; }
    public PosterRemovalTicket? Begin(PosterRemovalRequest request, PosterRemovalPreview preview)
    {
        if (Pending is not null || request.Source is not (StateCommandSource.Poster or StateCommandSource.ControlCenter) ||
            request.PosterId != preview.PosterId || preview.ReadSet.Posters.GetValueOrDefault(request.PosterId) != request.EntityVersion ||
            preview.ReadSet.CatalogVersion is null) return null;
        return Pending = new(Guid.NewGuid(), request, preview with { ReadSet = preview.ReadSet.Freeze() });
    }
    public bool IsCurrent(ControlCenterProjection projection) => Pending is { } p &&
        projection.CatalogVersion == p.Preview.ReadSet.CatalogVersion &&
        projection.PostersById.GetValueOrDefault(p.Request.PosterId)?.EntityVersion == p.Request.EntityVersion;
    public RemovePosterCommand? Complete(Guid ticketId, bool confirmed)
    {
        if (Pending is not { } p || p.Id != ticketId) return null;
        Pending = null;
        return confirmed ? new($"remove-{p.Id:N}", p.Request.Source, p.Preview.Revision,
            p.Preview.ReadSet, p.Request.PosterId, Confirmed: true) : null;
    }
    public void Cancel() => Pending = null;
}
