using DesktopUp.Windows;
namespace DesktopUp.Contracts.Tests;
public sealed class PickerCancellationGateTests
{
    [Fact] public void BeforeBind_CancellationIsDeliveredWhenEndpointArrives()
    {
        var gate = new PickerCancellationGate(); int signals = 0;
        gate.Cancel(); gate.Bind(() => signals++);
        Assert.Equal(1, signals); Assert.Null(gate.Complete("late.json"));
    }
    [Fact] public void RepeatedCancel_SignalsOnceAndDropsLateSelection()
    {
        var gate = new PickerCancellationGate(); int signals = 0;
        gate.Bind(() => signals++); gate.Cancel(); gate.Cancel();
        Assert.Equal(1, signals); Assert.Null(gate.Complete("late.json"));
    }
    [Fact] public void Completion_DisarmsBeforeHandleCanBeReused()
    {
        var gate = new PickerCancellationGate(); int signals = 0;
        gate.Bind(() => signals++);
        Assert.Equal("selected.json", gate.Complete("selected.json"));
        gate.Cancel(); Assert.Equal(0, signals);
        Assert.Throws<InvalidOperationException>(() => gate.Bind(() => signals++));
    }
    [Fact] public void NativeCancelWithoutExit_HasNoResult()
    {
        var gate = new PickerCancellationGate();
        Assert.Null(gate.Complete(null));
    }
    [Fact] public void FailedSetup_CanCompleteWithoutEndpoint()
    {
        var gate = new PickerCancellationGate(); gate.Cancel();
        Assert.Null(gate.Complete(null)); gate.Cancel();
    }
    [Fact] public void DoubleBindingAndCompletion_AreRejected()
    {
        var gate = new PickerCancellationGate(); gate.Bind(() => { });
        Assert.Throws<InvalidOperationException>(() => gate.Bind(() => { }));
        gate.Complete(null);
        Assert.Throws<InvalidOperationException>(() => gate.Complete(null));
    }
    [Fact] public async Task ConcurrentCancelAndComplete_DoNotSignalAfterCompletion()
    {
        for (int i = 0; i < 300; i++)
        {
            var gate = new PickerCancellationGate(); int signals = 0;
            gate.Bind(() => Interlocked.Increment(ref signals));
            var cancel = Task.Run(gate.Cancel);
            var complete = Task.Run(() => gate.Complete("picked.json"));
            await Task.WhenAll(cancel, complete);
            int after = signals; gate.Cancel();
            Assert.InRange(after, 0, 1); Assert.Equal(after, signals);
            Assert.True(await complete is null or "picked.json");
        }
    }
}
