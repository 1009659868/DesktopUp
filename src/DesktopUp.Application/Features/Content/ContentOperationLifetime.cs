namespace DesktopUp.Application;

// Owns host operation lifetimes, not file execution or per-poster interaction state.
// The host stops file admission first, closes views, then drains their full UI tasks.
public sealed class ContentOperationLifetime(Action<Exception> reportFailure) : IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly Dictionary<Task, Task> _operations = [];
    private FileOperation? _fileOperation;
    private bool _stopped;

    public CancellationToken Stopping => _stopping.Token;
    public bool IsStopping { get { lock (_gate) return _stopped; } }
    public bool FileOperationActive { get { lock (_gate) return _fileOperation is not null; } }
    public Task FileOperationCompletion { get { lock (_gate) return _fileOperation?.Completion ?? Task.CompletedTask; } }
    public bool IsActiveFor(string posterId)
    {
        lock (_gate) return _fileOperation is { } active &&
            (active.PosterId == posterId || active.SourcePosterId == posterId);
    }

    public FileOperation BeginFileOperation(string posterId, string? sourcePosterId = null)
    {
        lock (_gate)
        {
            if (_stopped) throw new OperationCanceledException("Content operations are stopping.", Stopping);
            if (_fileOperation is not null) throw new InvalidOperationException("A file operation is already active.");
            return _fileOperation = new FileOperation(this, posterId, sourcePosterId);
        }
    }

    // Registration may finish during stopping: a previously admitted UI callback still
    // owns its cleanup. Never discard that task merely because cancellation has begun.
    public void Track(Task operation)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate) if (!_operations.TryAdd(operation, completed.Task)) return;
        _ = Observe();
        async Task Observe()
        {
            try { await operation.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (Exception error) { Report(error); }
            finally
            {
                lock (_gate) _operations.Remove(operation);
                completed.TrySetResult();
            }
        }
    }

    public Task StopFileOperationsAsync()
    {
        Task completion;
        bool cancel;
        lock (_gate)
        {
            cancel = !_stopped;
            _stopped = true;
            completion = _fileOperation?.Completion ?? Task.CompletedTask;
        }
        if (cancel)
        {
            try { _stopping.Cancel(); }
            catch (AggregateException error) { Report(error); }
        }
        return completion;
    }

    // Called after views have closed. Re-snapshot for children registered by an
    // in-flight callback; no timer, retry of work, or background polling is involved.
    public async Task DrainAsync()
    {
        while (true)
        {
            Task[] pending;
            lock (_gate)
            {
                pending = _operations.Values.ToArray();
                if (pending.Length == 0) return;
            }
            await Task.WhenAll(pending).ConfigureAwait(false);
        }
    }

    private void Report(Exception error)
    {
        try { reportFailure(error); }
        catch (Exception) { /* Diagnostics must not strand an operation or the exit sequence. */ }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (!_stopped || _fileOperation is not null || _operations.Count != 0)
                throw new InvalidOperationException("Stop and drain content operations before disposing.");
            _stopping.Dispose();
        }
    }

    public sealed class FileOperation : IDisposable
    {
        private readonly ContentOperationLifetime _owner;
        private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal FileOperation(ContentOperationLifetime owner, string posterId, string? sourcePosterId)
        { _owner = owner; PosterId = posterId; SourcePosterId = sourcePosterId; }
        internal string PosterId { get; }
        internal string? SourcePosterId { get; }
        public Task Completion => _completed.Task;
        public void Dispose()
        {
            lock (_owner._gate)
            {
                if (!ReferenceEquals(_owner._fileOperation, this)) return;
                _owner._fileOperation = null;
                _completed.TrySetResult();
            }
        }
    }
}
