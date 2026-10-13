using System.Globalization;
using DesktopUp.Domain;

namespace DesktopUp.Application;

public enum PosterSetting { Scale, Spacing, Template, Subtitle, Content, Theme, AutoEnabled, AutoDelay, Lamp, TemplateParameters, TitleColors, TitleVisibility, ContentHeadingVisibility, Surface }
public sealed record PosterSettingEdit(PosterSurfaceProjection Basis, PosterSetting Setting,
    double Number = 0, string Text = "", PosterContentParameters? Content = null, PosterThemeParameters? Theme = null,
    LampTemplateParameters? Lamp = null, TemplateRecipe? Recipe = null, TemplateParameterValues? TemplateValues = null,
    PosterTitleColors? TitleColors = null, PosterSurfaceAppearance? Surface = null)
{
    public string PosterId => Basis.Poster.PosterId;
    public PosterStateCommand Command(string requestId)
    {
        if (Setting is PosterSetting.AutoEnabled or PosterSetting.AutoDelay)
        {
            var auto = PosterAutoPostureDraft.Open(Basis);
            if (Setting == PosterSetting.AutoEnabled)
            {
                if (Number is not (0 or 1)) throw new ArgumentException("Invalid toggle value.");
                auto = auto with { Enabled = Number == 1 };
            }
            else if (!int.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out int delay) ||
                PosterAutoPosturePolicy.ValidateDelay(delay) is not null) throw new ArgumentException("请输入1～300的整数秒数。");
            else auto = auto with { LeaveDelaySeconds = delay };
            return auto.ToCommand(requestId);
        }
        var draft = PosterPresentationDraft.Open(Basis);
        var parameters = draft.Parameters;
        var preferences = parameters.Preferences ?? new();
        draft = Setting switch
        {
            PosterSetting.Scale => draft with { Parameters = parameters with { Preferences = preferences with { Scale = Number } } },
            PosterSetting.Spacing => draft with { Parameters = parameters with { Preferences = preferences with { Spacing = Number } } },
            PosterSetting.Template => draft with { TemplateKey = Text, Parameters = Recipe is null ? parameters :
                Recipe.Id == Text ? TemplateRecipeInstancePolicy.Select(parameters, Recipe) : throw new ArgumentException("Recipe target mismatch.") },
            PosterSetting.Subtitle => draft with { Subtitle = Text },
            PosterSetting.ContentHeadingVisibility => draft with { Parameters = parameters with {
                HeadingVisibility = Number is 0 or 1 && Basis.Presentation.CanShowContentHeading
                    ? (parameters.HeadingVisibility ?? new([])).Set(draft.TemplateKey, Number == 1)
                    : throw new ArgumentException("Invalid or unavailable content heading toggle.") } },
            PosterSetting.Content => draft with { Parameters = parameters with { Content = Content ?? throw new ArgumentException("Missing content parameters.") } },
            PosterSetting.Theme => draft with { Parameters = parameters with { Theme = Theme ?? throw new ArgumentException("Missing theme parameters.") } },
            PosterSetting.TitleColors => draft with { Parameters = parameters with { TitleColors = TitleColors ?? throw new ArgumentException("Missing title colors.") } },
            PosterSetting.Surface => draft with { Parameters = parameters with { Surface = Surface ?? throw new ArgumentException("Missing surface appearance.") } },
            PosterSetting.TitleVisibility => draft with { Parameters = parameters with { ShowTitle = Number is 0 or 1
                ? Number == 1 : throw new ArgumentException("Invalid title visibility toggle.") } },
            PosterSetting.Lamp => draft with { Parameters = parameters with { Lamp = Lamp ?? throw new ArgumentException("Missing lamp parameters.") } },
            PosterSetting.TemplateParameters => draft with { Parameters = TemplateValues is { } values
                ? TemplateRecipeInstancePolicy.UpdateValues(parameters, draft.TemplateKey, values)
                : TemplateRecipeInstancePolicy.Update(parameters, draft.TemplateKey, Lamp ?? throw new ArgumentException("Missing template parameters.")) },
            _ => throw new ArgumentException("Unknown setting.")
        };
        var command = draft.ToCommand(requestId);
        if ((AuthoritativeStateValidationPolicy.Validate(command.DesiredState) ??
            PosterPresentationPolicy.ValidateEdit(Basis.Poster, command.DesiredState)) is { } error) throw new ArgumentException(error);
        return command;
    }
}

// Pure bounded ingress: transient gesture -> completed intent -> one in-flight command.
// Only a matching own success may advance queued bases. External revisions are never blindly rebased.
public sealed class ControlCenterSettingsSession
{
    private readonly List<PosterSettingEdit> _queue = new();
    private PosterSettingEdit? _pendingEdit;
    private PosterStateCommand? _pending;
    private bool _closed;
    private string? _previewValidationError;
    public PosterSettingEdit? Preview { get; private set; }
    public bool IsPreviewValid => Preview is not null && _previewValidationError is null;
    public string? Error { get; private set; }
    public string? ErrorPosterId { get; private set; }
    public bool IsPending => _pending is not null;
    public bool ProtectsInteraction => Preview is not null || IsPending || _queue.Count > 0;
    public int QueuedCount => _queue.Count;
    public bool OwnsPoster(string posterId) => Preview?.PosterId == posterId || _pendingEdit?.PosterId == posterId ||
        _queue.Any(edit => edit.PosterId == posterId);

    public bool Update(PosterSettingEdit edit)
    {
        if (_closed) return false;
        if (_pendingEdit?.PosterId == edit.PosterId || _queue.Any(e => e.PosterId == edit.PosterId) ||
            Preview is { } prior && (prior.PosterId != edit.PosterId || prior.Setting != edit.Setting))
        {
            Error = "请先完成或取消当前修改；冲突操作未排队。"; ErrorPosterId = edit.PosterId;
            return false;
        }
        if (Preview is { } previous && previous.PosterId == edit.PosterId && previous.Setting == edit.Setting)
            edit = edit with { Basis = previous.Basis };
        try { edit.Command("validate-only"); }
        catch (ArgumentException e)
        { _previewValidationError = Error = e.Message; ErrorPosterId = edit.PosterId; Preview = edit; return false; }
        Preview = edit; _previewValidationError = Error = ErrorPosterId = null; return true;
    }

    public void Finish()
    {
        if (Preview is not { } edit) return;
        if (_previewValidationError is not null) { Error = _previewValidationError; ErrorPosterId = edit.PosterId; return; }
        Preview = null;
        int existing = _queue.FindIndex(e => e.PosterId == edit.PosterId && e.Setting == edit.Setting);
        if (existing >= 0) _queue[existing] = edit with { Basis = _queue[existing].Basis };
        else if (_queue.Count < 64) _queue.Add(edit);
        else { Error = "待保存操作过多，请稍后重试。"; ErrorPosterId = edit.PosterId; }
    }

    public void CancelPreview() { Preview = null; _previewValidationError = Error = ErrorPosterId = null; }
    public void RejectPreview(string error)
    {
        if (Preview is not { } edit) return;
        _previewValidationError = Error = error; ErrorPosterId = edit.PosterId;
    }
    // Completed valid input stays bound to its original target even across selection/close.
    public void Close() { CancelPreview(); _closed = true; }

    public PosterStateCommand? TakeNext()
    {
        if (_pending is not null || _queue.Count == 0) return null;
        _pendingEdit = _queue[0]; _queue.RemoveAt(0);
        _pending = _pendingEdit.Command("controlcenter-settings-" + Guid.NewGuid().ToString("N"));
        return _pending;
    }

    public bool Complete(StateCommandResult result)
    {
        if (_pending is null || result.RequestId != _pending.RequestId || result.ExpectedRevision != _pending.ExpectedRevision) return false;
        var finished = _pendingEdit!;
        bool valid = result.Succeeded && result.ResultPosterId == finished.PosterId && result.Projection is { } projection &&
            projection.Revision == result.CurrentRevision && projection.Revision >= _pending.ExpectedRevision &&
            projection.PostersById.TryGetValue(finished.PosterId, out var updated) &&
            updated.EntityVersion >= finished.Basis.EntityVersion;
        if (valid)
        {
            var committed = result.Projection!.PostersById[finished.PosterId];
            for (int i = 0; i < _queue.Count; i++)
                if (_queue[i].PosterId == finished.PosterId && _queue[i].Basis.EntityVersion == finished.Basis.EntityVersion)
                    _queue[i] = _queue[i] with { Basis = committed };
            if (Preview is { } active && active.PosterId == finished.PosterId && active.Basis.EntityVersion == finished.Basis.EntityVersion)
                Preview = active with { Basis = committed };
        }
        else
        {
            _queue.RemoveAll(e => e.PosterId == finished.PosterId);
            if (Preview?.PosterId == finished.PosterId) Preview = null;
            Error = "未保存：" + (result.Error ?? result.Status.ToString()); ErrorPosterId = finished.PosterId;
        }
        _pending = null; _pendingEdit = null; return true;
    }
}
