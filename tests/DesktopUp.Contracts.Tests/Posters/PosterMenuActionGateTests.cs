using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterMenuActionGateTests
{
    [Fact]
    public void CurrentMenuGrantsExactlyOneAction()
    {
        var gate = new PosterMenuActionGate();
        Assert.True(gate.TryConsume(isCurrent: true));
        Assert.False(gate.TryConsume(isCurrent: true));
    }

    [Fact]
    public void ClosingWithoutAnActionRevokesEveryLaterCallback()
    {
        var gate = new PosterMenuActionGate();
        gate.Close();
        gate.Close();
        Assert.False(gate.TryConsume(isCurrent: true));
        Assert.False(gate.TryConsume(isCurrent: false));
    }

    [Fact]
    public void RejectedStaleAttemptCannotBecomeCurrentAgain()
    {
        var gate = new PosterMenuActionGate();
        Assert.False(gate.TryConsume(isCurrent: false));
        Assert.False(gate.TryConsume(isCurrent: true));
    }

    [Fact]
    public void ReplacementMenuHasItsOwnAuthorityWhileOldMenuIsRejected()
    {
        var oldMenu = new PosterMenuActionGate();
        var replacement = new PosterMenuActionGate();
        Assert.False(oldMenu.TryConsume(isCurrent: false));
        Assert.True(replacement.TryConsume(isCurrent: true));
        Assert.False(oldMenu.TryConsume(isCurrent: true));
    }

    [Fact]
    public void RepeatedOpenCallbackCannotOpenAgainWithoutAnyVersionChange()
    {
        var gate = new PosterMenuActionGate();
        int opens = 0;
        void OnOpen()
        {
            if (gate.TryConsume(isCurrent: true)) opens++;
        }
        OnOpen();
        OnOpen();
        OnOpen();
        Assert.Equal(1, opens);
    }

    [Fact]
    public void HideAndCloseDoNotRevokeAnAlreadyGrantedDeferredDispatch()
    {
        var gate = new PosterMenuActionGate();
        bool dispatchAuthorized = gate.TryConsume(isCurrent: true);
        gate.Close();
        Assert.True(dispatchAuthorized);
        // The deferred action rechecks its own current version/basis, not this consumed gate.
        Assert.False(gate.TryConsume(isCurrent: true));
    }
}
