namespace DesktopUp.Application;

// Application execution boundary: no view ownership or rendering callbacks.
public interface IContentActionService
{
    ValueTask<ContentActionResult> OpenAsync(ContentOpenIntent intent, CancellationToken cancellation);
    ValueTask<ContentActionResult> RevealAsync(string itemId, CancellationToken cancellation);
}
