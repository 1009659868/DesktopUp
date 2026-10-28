using DesktopUp.Domain;

namespace DesktopUp.Application;

public interface ICanvasPreviewHost
{
    bool Preview(PosterStateCommand command);
    void Restore(string posterId);
}

/// <summary>One target, one bounded draft. No file operations, timers or global snapshots retained.
/// Native preparation precedes commit; committed state remains authority if publication fails.</summary>
public sealed class CanvasDesignSession : IDisposable
{
    private readonly IStateProjectionCoordinator state;
    private readonly IStateCommandService commands;
    private readonly ICanvasPreviewHost host;
    private readonly PosterSurfaceProjection basis;
    private CanvasDesignCommand? prepared;
    private bool disposed;
    public ICanvasWorkspace? Workspace { get; }
    public double Width => basis.Poster.WidthDip;
    public double Height => basis.Poster.ExpandedHeightDip;
    public string PosterId => basis.Poster.PosterId;
    public CanvasDesign? Design { get; private set; }
    public long DraftRevision { get; private set; }
    public string? PreviewId { get; private set; }
    public string? RequestId => prepared?.RequestId;
    public string CapabilityRevision => CanvasCapabilities.Revision;
    public CanvasDesignSession(IStateProjectionCoordinator state, IStateCommandService commands, ICanvasPreviewHost host, string posterId, ICanvasWorkspace? workspace = null)
    {
        this.state = state; this.commands = commands; this.host = host;
        Workspace = workspace;
        basis = state.Refresh().PostersById.GetValueOrDefault(posterId) ?? throw new ArgumentException("海报不存在。");
        if (!basis.Presentation.CanEdit || basis.Poster.IsLocked || basis.Poster.IsCollapsed) throw new ArgumentException("请先展开并解除海报锁定。");
        Design = basis.Presentation.Parameters.Canvas?.Current;
    }
    public void Revise(CanvasDesign? design)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        host.Restore(PosterId); prepared = null; PreviewId = null;
        var frozen = design is null ? null : CanvasDeclaration.Freeze(design);
        Design = frozen; DraftRevision = checked(DraftRevision + 1);
    }
    public void Preview(bool undo = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        host.Restore(PosterId); prepared = null; PreviewId = null;
        var current = state.Refresh();
        if (current.PostersById.GetValueOrDefault(PosterId)?.EntityVersion != basis.EntityVersion)
            throw new ArgumentException("海报已变化，请关闭编辑器后重新打开。");
        var command = new CanvasDesignCommand("canvas-" + Guid.NewGuid().ToString("N"), current.Revision,
            StateReadSet.ForPoster(PosterId, basis.EntityVersion), PosterId, basis.Presentation.Parameters.Canvas?.Revision ?? 0, Design, undo);
        var desired = CanvasDesignCommands.Desired(basis.Poster, command);
        if ((undo ? basis.Presentation.Parameters.Canvas?.Previous : Design) is { } checkedDesign) Workspace?.Verify(checkedDesign);
        var preview = new PosterStateCommand(command.RequestId, StateCommandSource.ControlCenter,
            PosterStateCommandKind.SetPresentation, current.Revision, PosterId, desired) { ReadSet = command.ReadSet };
        try
        {
            if (!host.Preview(preview)) throw new ArgumentException("当前宿主不能安全预览，原设计已保留。");
            prepared = command; PreviewId = Guid.NewGuid().ToString("N");
        }
        catch { host.Restore(PosterId); throw; }
    }
    public StateCommandResult Apply(string previewId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (prepared is null || PreviewId != previewId) throw new ArgumentException("预览已失效，请重新预览。");
        try
        {
            // Fresh target/CAS and actual host preparation. Member sets are not supplied by a draft.
            var current = state.Refresh();
            if (current.PostersById.GetValueOrDefault(PosterId)?.EntityVersion != basis.EntityVersion)
                throw new ArgumentException("海报已变化，未覆盖新内容或人工修改。");
            var desired = CanvasDesignCommands.Desired(current.PostersById[PosterId].Poster, prepared);
            if ((prepared.Undo ? basis.Presentation.Parameters.Canvas?.Previous : prepared.Design) is { } checkedDesign) Workspace?.Verify(checkedDesign);
            if (!host.Preview(new(prepared.RequestId, StateCommandSource.ControlCenter, PosterStateCommandKind.SetPresentation,
                current.Revision, PosterId, desired) { ReadSet = prepared.ReadSet })) throw new ArgumentException("原生发布准备失败，未提交。");
            return commands.Execute(prepared);
        }
        finally { host.Restore(PosterId); PreviewId = null; }
    }
    public bool WasCommitted(string requestId) => state.Refresh().PostersById.GetValueOrDefault(PosterId)?
        .Presentation.Parameters.Canvas?.RequestId == requestId;
    public void Dispose() { if (disposed) return; disposed = true; PreviewId = null; prepared = null; host.Restore(PosterId); }
}
