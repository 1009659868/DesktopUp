namespace DesktopUp.Windows;

// A cancellation is an intent, never proof that the native dialog/worker has ended.
// Signals are nonblocking PostMessage calls; disarm before destroying their exact HWND.
internal sealed class PickerCancellationGate
{
    private readonly object _sync = new();
    private Action? _signal;
    private bool _cancelled, _completed;
    public bool IsCancellationRequested { get { lock (_sync) return _cancelled; } }
    public void Bind(Action signal)
    {
        lock (_sync)
        {
            if (_completed) throw new InvalidOperationException("Picker already completed.");
            if (_signal is not null) throw new InvalidOperationException("Picker already bound.");
            _signal = signal;
            if (_cancelled) _signal();
        }
    }
    public void Cancel()
    {
        lock (_sync)
        {
            if (_completed || _cancelled) return;
            _cancelled = true;
            _signal?.Invoke();
        }
    }
    public string? Complete(string? path)
    {
        lock (_sync)
        {
            if (_completed) throw new InvalidOperationException("Picker completed twice.");
            _completed = true;
            _signal = null;
            return _cancelled ? null : path;
        }
    }
}
