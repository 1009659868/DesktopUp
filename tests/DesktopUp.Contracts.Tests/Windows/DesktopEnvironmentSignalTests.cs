using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed class DesktopEnvironmentSignalTests
{
    private const uint TaskbarCreated = 0xC123;

    [Fact]
    public void TaskbarCreatedInvalidatesShellAndRechecksTopology()
    {
        DesktopEnvironmentChangeKind kind =
            DesktopEnvironmentSignalClassifier.Classify(
                TaskbarCreated,
                0,
                null,
                TaskbarCreated);

        Assert.True((kind & DesktopEnvironmentChangeKind.DesktopShell) != 0);
        Assert.True((kind & DesktopEnvironmentChangeKind.DisplayTopology) != 0);
    }

    [Theory]
    [InlineData(DesktopEnvironmentSignalClassifier.WmDisplayChange)]
    [InlineData(DesktopEnvironmentSignalClassifier.WmDpiChanged)]
    public void NativeDisplayMessagesRequestTopologyRecovery(uint message)
    {
        Assert.Equal(
            DesktopEnvironmentChangeKind.DisplayTopology,
            DesktopEnvironmentSignalClassifier.Classify(
                message,
                0,
                null,
                TaskbarCreated));
    }

    [Fact]
    public void OnlyRelevantSettingChangesRequestTopologyRecovery()
    {
        Assert.Equal(
            DesktopEnvironmentChangeKind.DisplayTopology,
            DesktopEnvironmentSignalClassifier.Classify(
                DesktopEnvironmentSignalClassifier.WmSettingChange,
                0,
                "ImmersiveColorSet WorkArea",
                TaskbarCreated));
        Assert.Equal(
            DesktopEnvironmentChangeKind.None,
            DesktopEnvironmentSignalClassifier.Classify(
                DesktopEnvironmentSignalClassifier.WmSettingChange,
                0,
                "UnrelatedSetting",
                TaskbarCreated));
    }

    [Fact]
    public void MouseActivationIsSuppressedWithoutBecomingRecoverySignal()
    {
        Assert.True(DesktopPointerActivationPolicy.ShouldSuppress(
            DesktopPointerActivationPolicy.WmMouseActivate));
        Assert.Equal(3, DesktopPointerActivationPolicy.MaNoActivate);
        Assert.Equal(-1, DesktopPointerActivationPolicy.HtTransparent);
        Assert.Equal(
            DesktopEnvironmentChangeKind.None,
            DesktopEnvironmentSignalClassifier.Classify(
                DesktopPointerActivationPolicy.WmMouseActivate,
                0,
                null,
                TaskbarCreated));
    }

    [Theory]
    [InlineData(DesktopLayerActivationPolicy.WmActivate)]
    [InlineData(DesktopLayerActivationPolicy.WmActivateApp)]
    public void ActivationTransitionsSynchronouslyReassertDesktopBottom(uint message)
    {
        Assert.True(DesktopLayerActivationPolicy.ShouldReassert(message));
        Assert.Equal(
            DesktopEnvironmentChangeKind.None,
            DesktopEnvironmentSignalClassifier.Classify(
                message,
                0,
                null,
                TaskbarCreated));
    }

    [Fact]
    public void PendingExternalZOrderRaiseIsConstrainedBeforeCommit()
    {
        Assert.True(DesktopZOrderPolicy.ShouldConstrain(0, new nint(-1)));
        Assert.True(DesktopZOrderPolicy.ShouldConstrain(0, new nint(-2)));
        Assert.False(DesktopZOrderPolicy.ShouldConstrain(
            DesktopZOrderPolicy.SwpNoZOrder,
            new nint(-1)));
        Assert.False(DesktopZOrderPolicy.ShouldConstrain(
            0,
            DesktopZOrderPolicy.HwndBottom));
    }
}
