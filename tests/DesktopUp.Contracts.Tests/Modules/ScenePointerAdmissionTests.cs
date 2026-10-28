using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed partial class PassivePosterPointerTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Press_and_capture_keep_decoration_following_but_retain_posture_protection(bool transparent)
    {
        var f = new Fixture();
        f.Targets[0] = f.Targets[0] with { Collapsed = false };
        f.Native.Point = new(-250, transparent ? 300 : 124);
        f.Native.Receiver = transparent ? 90 : 11;
        f.Native.Protection = AutoPostureProtection.PointerButton | AutoPostureProtection.PointerCapture;
        var sample = f.Sample(AutoPostureProtection.Gesture);
        var target = Assert.Single(sample.Posters);
        Assert.Equal(AutoPosturePresence.Inside, target.Pointer.Presence);
        var protection = sample.SharedProtection | target.Protection;
        Assert.NotEqual(AutoPostureProtection.None, protection & AutoPostureProtection.PointerButton);
        Assert.True(ScenePointerAdmissionPolicy.CanAnimate(true, target.Pointer.Presence, protection));
        Assert.Null(f.Router.Capture); // read-only observer never acquires input
    }

    [Fact]
    public void Decoration_soft_interactions_never_bypass_unknown_recovery_editing_or_global_pause()
    {
        var soft = AutoPostureProtection.PointerButton | AutoPostureProtection.PointerCapture |
            AutoPostureProtection.Gesture | AutoPostureProtection.Menu | AutoPostureProtection.Confirmation | AutoPostureProtection.DragDrop;
        foreach (var presence in Enum.GetValues<AutoPosturePresence>())
        {
            Assert.Equal(presence != AutoPosturePresence.Unknown, ScenePointerAdmissionPolicy.CanAnimate(true, presence, soft));
            Assert.False(ScenePointerAdmissionPolicy.CanAnimate(false, presence, soft));
            foreach (var reason in new[] { AutoPostureProtection.Recovery, AutoPostureProtection.Editing, (AutoPostureProtection)256 })
                Assert.False(ScenePointerAdmissionPolicy.CanAnimate(true, presence, soft | reason));
        }
        var f = new Fixture(); f.Native.CursorOk = false;
        var sample = f.Sample();
        Assert.False(ScenePointerAdmissionPolicy.CanAnimate(true, sample.Posters.Single().Pointer.Presence, sample.SharedProtection));
    }
}
