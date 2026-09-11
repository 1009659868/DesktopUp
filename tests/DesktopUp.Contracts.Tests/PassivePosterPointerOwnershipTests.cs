using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed partial class PassivePosterPointerTests
{
    [Theory]
    [InlineData(AutoPostureProtection.PointerButton)]
    [InlineData(AutoPostureProtection.PointerCapture)]
    [InlineData(AutoPostureProtection.PointerButton | AutoPostureProtection.PointerCapture)]
    public void ExternalContinuousClicksDoNotResetOrBlockTheOriginalDeadline(AutoPostureProtection raw)
    {
        var f = new Fixture(); f.Native.Point = new(500, 500); f.Native.Receiver = 90;
        f.Targets[0] = f.Targets[0] with { Collapsed = false };
        AutoPostureState state = AutoPostureState.Empty;
        AutoPostureStep Feed(long ms, AutoPostureProtection protection)
        {
            f.Native.Protection = protection; var pointer = f.Sample();
            var observation = pointer.Posters.Single();
            var step = AutoPostureModel.Advance(state, new(pointer.Sequence, ms,
                [new(A, 1, 1, new(true, 1), false, true, true, observation.Pointer)],
                SharedProtection: pointer.SharedProtection));
            state = step.State; return step;
        }
        Feed(0, AutoPostureProtection.None);
        foreach (int ms in new[] { 100, 200, 300, 400, 600, 800, 999 })
        {
            Assert.Null(Feed(ms, ms % 200 == 0 ? AutoPostureProtection.None : raw).Request);
            Assert.Equal(1000, state.Posters["A"].DeadlineMs);
        }
        Assert.True(Feed(1000, raw).Request?.Collapse);
    }

    [Fact]
    public void ForegroundOccluderClicksDoNotProtectPosterUnderneath()
    {
        var f = new Fixture(); f.Native.Receiver = 99; f.Native.Cover.Add(99);
        f.Native.Protection = AutoPostureProtection.PointerButton | AutoPostureProtection.PointerCapture;
        var sample = f.Sample();
        Assert.Equal(AutoPosturePresence.Outside, sample.Posters.Single().Pointer.Presence);
        Assert.Equal(AutoPostureProtection.None, sample.SharedProtection);
    }

    [Fact]
    public void PressOwnedByPosterRemainsProtectedAfterPointerLeavesUntilRelease()
    {
        var f = new Fixture(); f.Native.Protection = AutoPostureProtection.PointerButton;
        Assert.True(f.Sample().SharedProtection.HasFlag(AutoPostureProtection.PointerButton));
        f.Native.Point = new(500, 500); f.Native.Receiver = 90;
        Assert.True(f.Sample().SharedProtection.HasFlag(AutoPostureProtection.PointerButton));
        f.Native.Protection = AutoPostureProtection.None;
        Assert.Equal(AutoPostureProtection.None, f.Sample().SharedProtection);
        f.Native.Protection = AutoPostureProtection.PointerButton;
        Assert.Equal(AutoPostureProtection.None, f.Sample().SharedProtection);
    }

    [Fact]
    public void AutoDisabledPeerInteractionStillProtectsSharedSpace()
    {
        var f = new Fixture(); var b = new PosterInputSessionKey("B", 1);
        f.Router.Register(b); Assert.True(f.Router.Publish(b, new(100, 100, 300, 360), Layout()));
        f.Targets.Add(new(b, 21, 22, f.Targets[0].Shell, false, true, true, true));
        f.Native.Order = [22, 21, 12, 11, 90]; f.Native.Point = new(150, 124); f.Native.Receiver = 22;
        f.Native.Protection = AutoPostureProtection.PointerButton;
        Assert.True(f.Sample().SharedProtection.HasFlag(AutoPostureProtection.PointerButton));
    }

    [Fact]
    public void EnvironmentChangeClearsStaleNativeOwnershipButUnknownStillBlocks()
    {
        var f = new Fixture(); f.Native.Protection = AutoPostureProtection.PointerButton;
        f.Sample(); f.Native.Point = new(500, 500); f.Native.Receiver = 90;
        Assert.Equal(AutoPostureProtection.None, f.Sample(epoch: 2).SharedProtection);
        f.Native.CursorOk = false;
        Assert.True(f.Sample(epoch: 3).SharedProtection.HasFlag(AutoPostureProtection.Recovery));
    }

    [Fact]
    public void ExternalDragEnteringPosterBlocksNewExpansionAndMenuProtectionIsNotStripped()
    {
        var f = new Fixture(); f.Native.Point = new(500, 500); f.Native.Receiver = 90;
        f.Native.Protection = AutoPostureProtection.PointerButton; f.Sample();
        f.Native.Point = new(-250, 124);
        Assert.True(f.Sample().SharedProtection.HasFlag(AutoPostureProtection.PointerButton));
        f.Native.Protection = AutoPostureProtection.Menu; f.Native.Point = new(500, 500);
        Assert.Equal(AutoPostureProtection.Menu, f.Sample().SharedProtection);
    }

    [Fact]
    public void NoDemandClearsOwnerInsteadOfReplayingItWhenAutomaticModeIsEnabledAgain()
    {
        var f = new Fixture(); f.Native.Protection = AutoPostureProtection.PointerButton; f.Sample();
        f.Targets[0] = f.Targets[0] with { Enabled = false }; f.Sample();
        f.Native.Point = new(500, 500); f.Native.Receiver = 90;
        f.Targets[0] = f.Targets[0] with { Enabled = true };
        Assert.Equal(AutoPostureProtection.None, f.Sample().SharedProtection);
    }
}
