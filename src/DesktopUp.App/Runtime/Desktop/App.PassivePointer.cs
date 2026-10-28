using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp;

public partial class App
{
    private PassivePosterPointerObserver? _passivePointer;

    // Acquisition stays read-only. App.AutoPosture owns the shared dispatcher subscription,
    // environment epoch and sequence. Never feed queued target lists or issue commands here.
    private PosterPointerSnapshot ReadPassivePointer(long sequence, long environmentEpoch)
    {
        _passivePointer ??= new(_inputRouter);
        var protection = _spatialRuntime?.PointerProtection ?? AutoPostureProtection.Recovery;
        if (_controlCenter?.IsRemovalInteractionActive == true) protection |= AutoPostureProtection.Confirmation;
        if (_controlCenter?.IsSettingsInteractionActive == true) protection |= AutoPostureProtection.Editing;
        return _passivePointer.Sample(sequence, environmentEpoch,
            _spatialRuntime?.ReadPointerTargets() ?? [], protection);
    }
}
