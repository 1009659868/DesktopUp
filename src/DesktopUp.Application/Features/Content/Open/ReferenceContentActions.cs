namespace DesktopUp.Application;

public interface IAsyncContentActions
{
    ValueTask<ContentOperationFeedback> OpenAsync(ContentOpenIntent intent, CancellationToken cancellation);
    ValueTask<ContentOperationFeedback> RevealAsync(string itemId, CancellationToken cancellation);
    void Refresh();
}

// Per-poster presentation adapter. Execution and durable facts belong to the
// application service; this adapter only applies guarded metadata observations.
public sealed class ReferenceContentActions(IContentActionService service, ReferenceContentRuntime views)
    : IContentOpenHandler, IAsyncContentActions
{
    public void Open(ContentOpenIntent intent) =>
        throw new InvalidOperationException("Real opening must await its result.");

    public void Refresh() => views.Refresh();

    public async ValueTask<ContentOperationFeedback> OpenAsync(ContentOpenIntent intent, CancellationToken cancellation) =>
        Observe(await service.OpenAsync(intent, cancellation).ConfigureAwait(false));

    public async ValueTask<ContentOperationFeedback> RevealAsync(string itemId, CancellationToken cancellation) =>
        Observe(await service.RevealAsync(itemId, cancellation).ConfigureAwait(false));

    private ContentOperationFeedback Observe(ContentActionResult result)
    {
        if (result.Observation is { } observation)
            views.Observe(observation.ItemId, observation.Response, observation.Expected);
        return result.Feedback;
    }
}
