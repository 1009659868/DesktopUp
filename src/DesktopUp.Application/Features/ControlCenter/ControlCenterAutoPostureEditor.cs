using System.Globalization;
using DesktopUp.Domain;

namespace DesktopUp.Application;

// Window-session draft only. The frozen basis owns the target/CAS; projections never
// rebase user edits. No timer, repository, or second settings authority lives here.
public sealed class ControlCenterAutoPostureEditor
{
    private PosterSurfaceProjection? _latest;
    private PosterAutoPostureDraft? _basis;
    private string? _pendingRequest;
    public string? PosterId => _basis?.Basis.Poster.PosterId;
    public bool Enabled { get; private set; }
    public string DelayText { get; private set; } = "";
    public string? Error { get; private set; }
    public bool IsPending => _pendingRequest is not null;
    public bool IsDirty => _basis is not null && (Enabled != _basis.Enabled ||
        DelayText != _basis.LeaveDelaySeconds.ToString(CultureInfo.InvariantCulture));
    public bool IsStale => _basis is not null && _latest?.EntityVersion != _basis.Basis.EntityVersion;
    public bool ProtectsInteraction => IsDirty || IsPending;
    public string? ValidationError => _basis is null || TryDelay(out _) ? null : "请输入 1～300 的整数秒数。";
    public bool CanApply => IsDirty && !IsPending && ValidationError is null;
    public bool CanCancel => _basis is not null && !IsPending && (IsDirty || IsStale || Error is not null);

    public void Synchronize(PosterSurfaceProjection? selected)
    {
        if (selected?.Poster.PosterId != PosterId)
        {
            _latest = selected;
            Reset(); // Switching/deletion discards only this window's unapplied draft.
            return;
        }
        if (selected is not null && _latest is not null && selected.Revision < _latest.Revision) return;
        _latest = selected;
        if (!IsDirty && !IsPending) Reset();
    }

    public void Edit(bool enabled, string delayText)
    {
        if (_basis is null || IsPending) return;
        Enabled = enabled; DelayText = delayText; Error = null;
        // Editing back to the original values releases protection and reloads latest.
        if (!IsDirty) Reset();
    }

    public void Cancel() { if (!IsPending) Reset(); }

    public PosterStateCommand? BeginApply(string requestId)
    {
        if (!CanApply || !TryDelay(out int delay)) return null;
        if (string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("A request ID is required.", nameof(requestId));
        _pendingRequest = requestId; Error = null;
        return (_basis! with { Enabled = Enabled, LeaveDelaySeconds = delay }).ToCommand(requestId);
    }

    public bool Complete(StateCommandResult result)
    {
        if (_pendingRequest != result.RequestId) return false; // A late A reply cannot reset B's draft.
        _pendingRequest = null;
        if (result.Projection?.PostersById.TryGetValue(PosterId!, out var committed) == true &&
            (_latest is null || committed.Revision >= _latest.Revision)) _latest = committed;
        if (result.Succeeded && result.Projection is not null) Reset();
        else Error = result.Succeeded ? "未取得已提交状态，请取消并重新载入。" :
            $"未应用（{result.Status}）：{result.Error ?? "目标已变化。"} 请取消以载入最新值，再重新编辑。";
        return true;
    }

    private bool TryDelay(out int delay) => int.TryParse(DelayText, NumberStyles.None,
        CultureInfo.InvariantCulture, out delay) && PosterAutoPosturePolicy.ValidateDelay(delay) is null;

    private void Reset()
    {
        _basis = _latest is null ? null : PosterAutoPostureDraft.Open(_latest);
        Enabled = _basis?.Enabled ?? false;
        DelayText = _basis?.LeaveDelaySeconds.ToString(CultureInfo.InvariantCulture) ?? "";
        Error = null; _pendingRequest = null;
    }
}
