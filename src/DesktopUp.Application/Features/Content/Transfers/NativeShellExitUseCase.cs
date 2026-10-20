namespace DesktopUp.Application;

// OLE is owned by the host. This use case only records and observes its outcome;
// even MOVE/NONE is not proof that a source disappeared.
public sealed class NativeShellExitUseCase(INativeShellExitStore journal)
{
    public ShellExitIntent Begin(string posterId, IReadOnlyList<string> selection) => journal.Begin(posterId, selection);

    public async Task Complete(ShellExitIntent intent, ShellExitCompletion? result,
        INativeExitPresentation presentation, CancellationToken token)
    {
        bool external = result is { HandedToPoster: false, Released: true, ReturnedToOrigin: false };
        journal.End(intent, external);
        if (!external) return;
        for (int attempt = 0; attempt < 6 && !token.IsCancellationRequested; attempt++)
        {
            await Task.Delay(attempt == 0 ? 150 : 500, token);
            int removed = await Task.Run(() => journal.ObserveReleasedMove(intent));
            if (removed > 0) presentation.PublishObservedChanges();
            if (!journal.HasRemainingPresentCards(intent) || result!.Effect is 1 or 4) break;
        }
        if (intent.Items.Any(i => i.Missing) && !token.IsCancellationRequested &&
            await presentation.ConfirmMissingReferences(intent.Items.Where(i => i.Missing).Select(i => i.Path).ToArray(), token))
            await Task.Run(() => journal.RemoveStaleReferencesAfterUserConfirmation(intent));
        journal.Reconcile();
    }
}
