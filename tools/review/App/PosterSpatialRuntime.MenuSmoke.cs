using DesktopUp.Windows;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

internal sealed partial class PosterSpatialRuntime
{
    internal void RunMenuSmoke(DispatcherQueue dispatcher)
    {
        var timer = dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(350);
        var deadline = DateTime.UtcNow.AddSeconds(12);
        PosterVisualWindow[]? windows = null;
        int step = 0, sample = 0;
        void Finish(bool passed, string? error)
        {
            timer.Stop(); timer.Tick -= Tick;
            DesktopMenuInteraction.CancelActive();
            log.Write(passed ? "info" : "error", "menu.smoke_completed", new { passed, error, cases = sample });
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            try
            {
                if (DateTime.UtcNow > deadline) { Finish(false, "timeout"); return; }
                if (_suspended || _windows.Count != 3) return;
                if (windows is null)
                {
                    windows = _windows.Values.OrderBy(w => w.PosterId, StringComparer.Ordinal).ToArray();
                    for (int i = 0; i < windows.Length; i++)
                        if (!windows[i].ArrangeForMenuSmoke(i)) { Finish(false, "arrange-failed"); return; }
                    return;
                }
                var target = windows[sample / 2];
                if (step == 0)
                {
                    if (!target.SetEditing(sample % 2 == 1)) { Finish(false, "mode-failed"); return; }
                    target.OpenForMenuSmoke(); step = 1; return;
                }
                if (step == 1)
                {
                    var probe = target.ReadMenuProbe();
                    var commands = target.ReadPosterMenuCommandsForProbe();
                    bool compactCommands = commands.Length == 4 &&
                        commands[0] is "锁定" or "解除锁定" &&
                        commands[1] is "调整海报外框" or "结束外框调整" &&
                        commands[2] == "打开控制中心" && commands[3] == "删除海报…";
                    bool passed = probe is { Ordered: true, InputsTransparent: true } &&
                        probe.Samples.Count == 15 && probe.Samples.All(p => p.HitPopup) && compactCommands;
                    log.Write(passed ? "info" : "error", "menu.smoke_readback", new
                    { target.PosterId, editing = target.IsEditing, passed, probe, commands, compactCommands, note = "native-query-not-mouse-click" });
                    if (!passed)
                    {
                        bool occluded = probe is { Ordered: true, InputsTransparent: true } &&
                            probe.Samples.Count > 0 && probe.Samples.All(p => !p.HitPopup && !p.ReceiverOwnProcess);
                        Finish(false, occluded ? "probe-area-occluded-by-another-process" : "popup-input-or-layer-readback"); return;
                    }
                    target.CloseForMenuSmoke(); step = 2; return;
                }
                if (windows.Any(w => !w.MenuInputRestored())) { Finish(false, "input-not-restored"); return; }
                if (++sample == 6) { Finish(true, null); return; }
                step = 0;
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }
        timer.Tick += Tick; timer.Start();
    }
}
