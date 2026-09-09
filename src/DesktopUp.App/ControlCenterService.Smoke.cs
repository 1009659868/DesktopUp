using DesktopUp.Application;
using Microsoft.UI.Dispatching;

namespace DesktopUp;

internal sealed partial class ControlCenterService
{
    internal bool VerifyProjection(DesktopUpProjection expected) => _projection?.Revision == expected.Revision &&
        _window?.VerifyProjection(expected) == true;
    internal void CloseForContractProbe() => _window?.Close();

    // Explicit isolated CLI fixture only. Exercises real XAML show/cancel/close, never auto-confirms deletion.
    public void RunIsolatedConfirmationSmoke(DispatcherQueue dispatcher, bool exitWithDialog = false)
    {
        var timer = dispatcher.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(200);
        DateTime deadline = DateTime.UtcNow.AddSeconds(16);
        int step = 0, initialCount = 0; long revision = -1;
        PosterRemovalRequest? request = null;
        Guid firstTicket = default;
        timer.Tick += Tick; timer.Start();
        void Finish(bool passed, string? error)
        {
            timer.Stop(); timer.Tick -= Tick;
            log.Write(passed ? "info" : "error", "input_confirmation.smoke_completed", new
                { passed, error, step, initialCount, readyInputClusters = inputRouter.ReadyCount, revision, exitWithDialog });
        }
        void Tick(DispatcherQueueTimer sender, object args)
        {
            try
            {
                if (_disposed) { Finish(false, "disposed"); return; }
                if (DateTime.UtcNow > deadline) { Finish(false, "timeout"); return; }
                if (!state.TryGetCurrent(out var current) || current is null || _environmentPending) return;
                switch (step)
                {
                    case 0:
                        if (inputRouter.ReadyCount != current.Posters.Count(p => p.Poster.IsVisible) || current.Posters.Count < 2) return;
                        revision = current.Revision; initialCount = current.Posters.Count;
                        var target = current.Posters[1];
                        request = new(target.Poster.PosterId, target.EntityVersion, StateCommandSource.ControlCenter);
                        RequestRemoval(request); step++; break;
                    case 1:
                        if (_window?.IsRemovalDialogLoaded != true) return;
                        firstTicket = _removal.Pending!.Id;
                        var other = current.Posters[0];
                        RequestRemoval(new(other.Poster.PosterId, other.EntityVersion, StateCommandSource.ControlCenter));
                        if (_removal.Pending?.Id != firstTicket) { Finish(false, "duplicate-retargeted"); return; }
                        _window.CancelRemovalDialog(); step++; break;
                    case 2:
                        if (_removalShowing) return;
                        if (_removal.Pending is not null || current.Revision != revision) { Finish(false, "cancel-mutated"); return; }
                        RequestRemoval(request!); step++; break;
                    case 3:
                        if (_window?.IsRemovalDialogLoaded != true) return;
                        _window.Close(); step++; break;
                    case 4:
                        if (_removalShowing) return;
                        if (_removal.Pending is not null || _window is not null) { Finish(false, "close-left-dialog"); return; }
                        Show(); step++; break;
                    case 5:
                        bool passed = _window is not null && current.Revision == revision && current.Posters.Count == initialCount &&
                            inputRouter.Capture is null && inputRouter.ReadyCount == current.Posters.Count(p => p.Poster.IsVisible);
                        if (passed && exitWithDialog) { RequestRemoval(request!); step++; }
                        else Finish(passed, passed ? null : "state-or-input-changed");
                        break;
                    case 6:
                        if (_window?.IsRemovalDialogLoaded != true) return;
                        Finish(true, null); // The normal application exit must cancel/drain this dialog.
                        break;
                }
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }
    }
}
