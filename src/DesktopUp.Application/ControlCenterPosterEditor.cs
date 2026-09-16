using DesktopUp.Domain;

namespace DesktopUp.Application;

// UI-thread/window-session model only. No repository, timer, HWND or geometry solver.
// Advanced atomic groups only. Common settings use ControlCenterSettingsSession.
public sealed class ControlCenterPosterEditor(bool requireActualGeometry = false)
{
    private DesktopUpProjection? _projection;
    private PosterSurfaceProjection? _latest, _basis;
    private PosterPresentationDraft? _presentationBasis;
    private string? _pendingRequest;
    private long _pendingRevision;
    private bool _closed;
    private ControlCenterGeometryBasis? _latestGeometry, _actualGeometry;
    private PosterGeometryText? _geometryTextBasis;
    public bool HasActualGeometry => _actualGeometry is not null && _basis is not null && _actualGeometry.Matches(_basis);

    public string? PosterId => _basis?.Poster.PosterId;
    public ControlCenterEditGroup Group { get; private set; }
    public PosterGeometryText? Geometry { get; private set; }
    public PosterPresentationDraft? Presentation { get; private set; }
    public EffectivePosterLayout? EffectiveLayout { get; private set; }
    public PosterSurfaceProjection? LatestCommitted => _latest;
    public string? Error { get; private set; }
    public bool IsPending => _pendingRequest is not null;
    public bool IsStale => _basis is not null && _latest?.EntityVersion != _basis.EntityVersion;
    public bool IsDirty => Group switch
    {
        ControlCenterEditGroup.Geometry => Geometry != _geometryTextBasis,
        ControlCenterEditGroup.Presentation => Presentation != _presentationBasis,
        _ => false
    };
    public bool ProtectsInteraction => IsDirty || IsPending;
    public bool CanApply => !_closed && IsDirty && !IsPending && ValidationError is null;
    public bool CanCancel => !_closed && _basis is not null && !IsPending && (IsDirty || Error is not null);
    public bool CanRunImmediateAction => !_closed && _basis is not null && !IsPending;

    public string? ValidationError
    {
        get
        {
            if (_basis is null || Group == ControlCenterEditGroup.None) return null;
            if (Group == ControlCenterEditGroup.Geometry)
            {
                if (requireActualGeometry && !HasActualGeometry) return "无法取得当前桌面位置；请等待恢复后重试。";
                if (Geometry?.TryRead(out var geometry) != true || geometry is null)
                    return "请输入有限十进制 DIP 数值（小数点为 .），不接受指数或分组格式。";
                if (_basis.Poster.IsLocked || !_basis.CanMove) return "锁定海报不能移动或缩放。";
                bool resizing = geometry.Width != _basis.Poster.WidthDip ||
                    geometry.ExpandedHeight != _basis.Poster.ExpandedHeightDip;
                if (resizing && !_basis.CanResize) return "当前海报不能缩放；请先展开并解除锁定。";
                // Anchors are deliberately unchanged here: ONLY the spatial adapter may
                // recompute them from a verified physical result.
                return AuthoritativeStateValidationPolicy.Validate(_basis.Poster with
                {
                    FallbackXDip = geometry.X, FallbackYDip = geometry.Y,
                    WidthDip = geometry.Width, ExpandedHeightDip = geometry.ExpandedHeight
                });
            }
            try
            {
                var desired = Presentation!.ToCommand("validation-only").DesiredState;
                return AuthoritativeStateValidationPolicy.Validate(desired) ??
                    PosterPresentationPolicy.ValidateEdit(_basis.Poster, desired);
            }
            catch (ArgumentException e) { return e.Message; }
        }
    }

    public bool Synchronize(DesktopUpProjection projection, string? selectedPosterId,
        EffectivePosterLayout? effective = null, ControlCenterGeometryBasis? actualGeometry = null)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (_closed || _projection is not null && projection.Revision < _projection.Revision) return false;
        _projection = projection;
        var selected = selectedPosterId is null ? null : projection.PostersById.GetValueOrDefault(selectedPosterId);
        bool changedTarget = selected?.Poster.PosterId != PosterId;
        _latest = selected;
        _latestGeometry = selected is not null && actualGeometry?.Matches(selected) == true ? actualGeometry : null;
        EffectiveLayout = selected is not null && effective?.Intent == selected.Poster &&
            effective.Revision == selected.Revision && effective.LayoutGeneration == selected.LayoutGeneration &&
            effective.Environment.IsValid && effective.Bounds.IsValid ? effective : null;
        if (changedTarget || (!IsDirty && !IsPending)) Reset();
        return true;
    }

    public bool EditGeometry(PosterGeometryText value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!Enter(ControlCenterEditGroup.Geometry)) return false;
        Geometry = value;
        if (!IsDirty) Reset();
        return true;
    }

    public bool EditPresentation(string title, string subtitle, string templateKey, PosterPresentationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (_presentationBasis is null || !Enter(ControlCenterEditGroup.Presentation)) return false;
        Presentation = _presentationBasis with { Title = title, Subtitle = subtitle, TemplateKey = templateKey, Parameters = parameters };
        if (!IsDirty) Reset();
        return true;
    }

    private bool Enter(ControlCenterEditGroup group)
    {
        if (_closed || _basis is null || IsPending) return false;
        if (Group != ControlCenterEditGroup.None && Group != group)
        {
            Error = "请先应用或取消当前分区的修改；没有跨分区的全部应用。";
            return false;
        }
        Group = group;
        Error = null;
        return true;
    }

    public PosterStateCommand? BeginPresentationApply()
    {
        if (Group != ControlCenterEditGroup.Presentation || !CanApply) return null;
        var command = Presentation!.ToCommand(NewRequestId());
        Pending(command.RequestId, command.ExpectedRevision);
        return command;
    }

    public ControlCenterGeometryRequest? BeginGeometryApply()
    {
        if (Group != ControlCenterEditGroup.Geometry || !CanApply || !Geometry!.TryRead(out var desired)) return null;
        // Fresh obstacle reads, but NEVER silently rebase this draft's own entity version.
        var versions = _projection!.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion);
        versions[PosterId!] = _basis!.EntityVersion;
        var request = new ControlCenterGeometryRequest(NewRequestId(), _basis, _projection.Revision,
            new StateReadSet(versions, CatalogVersion: _projection.CatalogVersion).Freeze(), desired!)
        { ActualBasis = _actualGeometry, ChangedFields = ControlCenterGeometryBasis.Changed(_geometryTextBasis!, Geometry) };
        Pending(request.RequestId, request.ObservedRevision);
        return request;
    }

    public bool Complete(StateCommandResult result)
    {
        if (_closed || _pendingRequest is null || result.RequestId != _pendingRequest) return false;
        if (result.ExpectedRevision != _pendingRevision) return false;
        // A failed command can be the first authoritative notice of target removal.
        // Do not require a separate projection event to disable the deleted editor.
        if (result.Status == StateCommandStatus.NotFound && result.Projection is { } missing &&
            missing.Revision == result.CurrentRevision && missing.Revision >= _pendingRevision &&
            !missing.PostersById.ContainsKey(PosterId!) && Synchronize(missing, PosterId))
        {
            Error = "目标已删除，未应用修改。";
            return true;
        }
        bool authoritative = result.Projection is { } projection &&
            projection.Revision == result.CurrentRevision && projection.Revision >= _pendingRevision &&
            projection.PostersById.TryGetValue(PosterId!, out var target) &&
            target.EntityVersion >= _basis!.EntityVersion &&
            (!result.Succeeded || result.ResultPosterId == PosterId);
        if (authoritative) Synchronize(result.Projection!, PosterId);
        _pendingRequest = null;
        if (result.Succeeded && authoritative) Reset();
        else Error = result.Succeeded ? "未取得匹配目标的权威提交结果；修改仍保留，请重新载入后核对。" :
            $"未应用（{result.Status}）：{result.Error ?? "目标或空间已变化。"} 请取消载入最新值后重试。";
        return true;
    }

    public void Cancel() { if (!_closed && !IsPending) Reset(); }

    // Discard local drafts; this does NOT undo/cancel an already dispatched transaction.
    public void Close()
    {
        _closed = true; _projection = null; _latest = null; EffectiveLayout = null; Reset();
    }

    private void Reset()
    {
        _basis = _latest;
        _presentationBasis = _basis?.Presentation.CanEdit == true ? PosterPresentationDraft.Open(_basis) : null;
        _actualGeometry = _latestGeometry;
        _geometryTextBasis = _basis is null ? null : _actualGeometry?.Text(_basis.Poster) ??
            (requireActualGeometry ? null : PosterGeometryText.From(_basis.Poster));
        Geometry = _geometryTextBasis;
        Presentation = _presentationBasis;
        Group = ControlCenterEditGroup.None;
        _pendingRequest = null; Error = null;
    }
    private void Pending(string id, long revision) { _pendingRequest = id; _pendingRevision = revision; Error = null; }
    private static string NewRequestId() => "controlcenter-edit-" + Guid.NewGuid().ToString("N");
}
