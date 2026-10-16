using DesktopUp.Application;
using DesktopUp.Domain;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    internal sealed record PresentationReadback(string PosterId, string WindowHandle, SpatialRect Bounds,
        long EntityVersion, string TemplateKey, PosterContentParameters Content, bool XamlVerified);
    internal IReadOnlyList<PresentationReadback> InspectPresentation(DesktopUpProjection projection)
    {
        var result = new List<PresentationReadback>();
        foreach (var target in projection.Posters.Where(p => p.Poster.IsVisible))
        {
            if (!_windows.TryGetValue(target.Poster.PosterId, out var window) ||
                !window.TryReadSpatial(_runtimeId, out _, out var physical, out long entity))
                throw new InvalidOperationException("Missing native readback: " + target.Poster.PosterId);
            result.Add(new(target.Poster.PosterId, $"0x{window.WindowHandle:X}", physical!.Bounds, entity,
                target.Presentation.EffectiveTemplateKey, target.Presentation.Parameters.Content, window.VerifyPresentation(target)));
        }
        return result;
    }

    // App requires an explicit flag and isolated profile before invoking this fixture.
    public void RunPresentationSmoke(DispatcherQueue dispatcher)
    {
        var timer = dispatcher.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(500);
        timer.IsRepeating = true;
        var deadline = DateTime.UtcNow.AddSeconds(14);
        Dictionary<string, (SpatialRect Bounds, nint Hwnd, string Theme)>? before = null;
        Dictionary<string, PosterPresentationParameters>? expected = null;
        timer.Tick += Tick; timer.Start();
        void Finish(bool passed, string? error)
        {
            timer.Stop(); timer.Tick -= Tick;
            log.Write(passed ? "info" : "error", "presentation.smoke_completed", new { passed, error, posterCount = expected?.Count });
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            try
            {
                if (DateTime.UtcNow > deadline) { Finish(false, "timeout"); return; }
                if (_suspended || !state.TryGetCurrent(out var current) || current is null) return;
                if (before is null)
                {
                    if (current.Posters.Count != 3) { Finish(false, "requires-three-poster-fixture"); return; }
                    var capture = new Dictionary<string, (SpatialRect, nint, string)>();
                    foreach (var target in current.Posters)
                    {
                        var id = target.Poster.PosterId;
                        if (!_windows.TryGetValue(id, out var window) ||
                            !window.TryReadSpatial(_runtimeId, out _, out var physical, out _)) return;
                        capture[id] = (physical!.Bounds, window.WindowHandle, target.Poster.ThemeInstanceId);
                    }
                    before = capture; expected = new();
                    log.Write("info", "presentation.smoke_start", new { persistedVersioned = current.Posters.Count(p => !p.Presentation.IsLegacy) });
                    for (int index = 0; index < current.Posters.Count; index++)
                    {
                        var target = current.Posters[index];
                        string template = index == 0 ? "night-ops" : "basic-box";
                        var defaults = PosterPresentationPolicy.Defaults(template);
                        var parameters = defaults with
                        {
                            Content = defaults.Content with { IconSizeDip = 24 + index * 8, GapDip = 8 + index * 4 },
                            Theme = defaults.Theme with { Background = new[] { "#182B36", "#282B30", "#243329" }[index] }
                        };
                        var draft = PosterPresentationDraft.Open(target) with
                        {
                            Title = new[] { "M05 / Games", "M05 / Documents", "M05 / Applications" }[index],
                            Subtitle = "Six demo placeholders; no files collected", TemplateKey = template, Parameters = parameters
                        };
                        var result = commands.Execute(draft.ToCommand("presentation-smoke-" + Guid.NewGuid().ToString("N")));
                        if (!result.Succeeded) { Finish(false, result.Error); return; }
                        expected[target.Poster.PosterId] = parameters;
                    }
                    return; // Give projection delivery and XAML layout a separate dispatcher turn.
                }
                current = state.Refresh();
                foreach (var target in current.Posters)
                {
                    string id = target.Poster.PosterId;
                    var window = _windows[id];
                    if (!window.TryReadSpatial(_runtimeId, out _, out var physical, out var entity) ||
                        physical!.Bounds != before[id].Bounds || window.WindowHandle != before[id].Hwnd ||
                        target.Poster.ThemeInstanceId != before[id].Theme || entity != target.EntityVersion ||
                        target.Presentation.Parameters != expected![id] || !window.VerifyPresentation(target))
                    { Finish(false, "xaml-state-or-physical-readback-mismatch:" + id); return; }
                }
                var first = current.Posters[0];
                var invalid = PosterPresentationDraft.Open(first) with
                    { Parameters = first.Presentation.Parameters with { Content = first.Presentation.Parameters.Content with { IconSizeDip = -1 } } };
                bool rejected = false;
                try { commands.Execute(invalid.ToCommand("invalid-" + Guid.NewGuid().ToString("N"))); }
                catch (ArgumentException) { rejected = true; }
                if (!rejected || state.Refresh().Revision != current.Revision) { Finish(false, "invalid-draft-wrote-state"); return; }
                log.Write("info", "presentation.smoke_readback", new { sameHwnd = true, sameBounds = true,
                    independentInstances = true, xamlVerified = true, invalidDraftRejected = true, current.Revision });
                Finish(true, null);
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }
    }
}
