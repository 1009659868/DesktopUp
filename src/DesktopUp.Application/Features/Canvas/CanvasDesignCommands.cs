using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record CanvasDesignCommand(string RequestId, long ExpectedRevision, StateReadSet ReadSet,
    string PosterId, long ExpectedDesignRevision, CanvasDesign? Design, bool Undo = false);

public sealed partial class StateProjectionCoordinator
{
    public StateCommandResult Execute(CanvasDesignCommand command) => Run(command.RequestId, StateCommandSource.ControlCenter,
        command.ExpectedRevision, command.ReadSet, reads => command with { ReadSet = Required(reads),
            Design = command.Design is { } design ? CanvasDeclaration.Freeze(design) : null }, CanvasDesignCommands.Prepare);
}

public static class CanvasDesignCommands
{
    public static PosterDefinition Desired(PosterDefinition poster, CanvasDesignCommand command)
    {
        var presentation = PosterPresentationPolicy.Resolve(poster);
        if (!presentation.CanEdit) throw new ArgumentException("当前海报格式只读，原配置保留。");
        var prior = presentation.Parameters.Canvas;
        if ((prior?.Revision ?? 0) != command.ExpectedDesignRevision) throw new ArgumentException("设计版本已变化，请重新预览。");
        if (command.Undo && prior?.CanUndo != true) throw new ArgumentException("没有可撤销的画板设计。");
        var candidate = command.Undo ? prior!.Previous : command.Design;
        if (candidate is not null) candidate = CanvasDeclaration.Freeze(candidate);
        var next = new CanvasDesignState(checked((prior?.Revision ?? 0) + 1), candidate, command.Undo ? null : prior?.Current,
            !command.Undo, command.RequestId, command.Undo ? null : new(poster.WidthDip, poster.ExpandedHeightDip));
        var desired = poster with { LayoutParametersJson = PosterPresentationPolicy.Serialize(presentation.Parameters with { Canvas = next }, poster.LayoutParametersJson) };
        var frame = command.Undo ? prior?.PreviousFrame : CanvasSurfaceGeometry.IsWholePoster(candidate) ? candidate!.Recipe.Artboard : null;
        if (frame is not null)
            desired = desired with { WidthDip = frame.Width, ExpandedHeightDip = frame.Height,
                NormalizedWidth = poster.NormalizedWidth * frame.Width / poster.WidthDip,
                NormalizedExpandedHeight = poster.NormalizedExpandedHeight * frame.Height / poster.ExpandedHeightDip };
        if (PosterPresentationPolicy.ValidateEdit(poster, desired) is { } error) throw new ArgumentException(error);
        ValidateSpace(desired);
        return desired;
    }
    internal static StateMutationPlan Prepare(CanvasDesignCommand command, DesktopStateSnapshot state)
    {
        if (!command.ReadSet.Posters.ContainsKey(command.PosterId)) return StateMutationPlan.Reject(StateCommandStatus.PermissionDenied, "缺少目标版本检查。");
        var poster = state.Catalog.Posters.FirstOrDefault(p => p.Poster.PosterId == command.PosterId)?.Poster;
        if (poster is null) return StateMutationPlan.Reject(StateCommandStatus.NotFound, "目标海报不存在。");
        if (poster.IsLocked || poster.PlacementPending) return StateMutationPlan.Reject(StateCommandStatus.PermissionDenied, "请先解除锁定或完成安置。");
        return new() { Updates = [Desired(poster, command)], ResultPosterId = command.PosterId };
    }
    public static void ValidateSpace(PosterDefinition poster)
    {
        if (PosterPresentationPolicy.Resolve(poster).Parameters.Canvas?.Current is { } design)
            CanvasSurfaceGeometry.ValidateHosting(design, CanvasLayout.Resolve(design, poster.WidthDip, poster.ExpandedHeightDip));
        if (!ContentFrameMinimumPolicy.TryForPoster(poster, out var minimum, out var error) ||
            poster.WidthDip + .01 < minimum!.WidthDip || poster.ExpandedHeightDip + .01 < minimum.HeightDip)
            throw new ArgumentException(error ?? "外框不足以容纳当前内容和组件，请先扩大外框或减少组件。");
    }
}
