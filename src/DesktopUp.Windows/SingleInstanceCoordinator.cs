using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using DesktopUp.Domain;

namespace DesktopUp.Windows;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly SingleInstanceNames _names;
    private readonly CancellationTokenSource _shutdown = new();
    private Mutex? _mutex;
    private Task? _listener;
    private bool _ownsMutex;
    private int _disposed;

    public SingleInstanceCoordinator(SingleInstanceNames names)
    {
        _names = names;
    }

    public event Action<StartupIntent>? IntentReceived;

    public bool TryAcquireOrForward(StartupIntent intent, TimeSpan? timeout = null)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        _mutex = new Mutex(initiallyOwned: true, _names.MutexName, out bool createdNew);
        if (createdNew)
        {
            _ownsMutex = true;
            _listener = Task.Run(ListenAsync);
            return true;
        }

        _mutex.Dispose();
        _mutex = null;
        Forward(intent, timeout ?? TimeSpan.FromSeconds(3));
        return false;
    }

    private void Forward(StartupIntent intent, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        Exception? lastFailure = null;
        string payload = JsonSerializer.Serialize(intent);

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var client = new NamedPipeClientStream(
                    ".",
                    _names.PipeName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous);
                client.Connect(250);
                using var writer = new StreamWriter(client, new UTF8Encoding(false), leaveOpen: false)
                {
                    AutoFlush = true
                };
                writer.WriteLine(payload);
                return;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException)
            {
                lastFailure = ex;
                Thread.Sleep(50);
            }
        }

        throw new TimeoutException("The primary DesktopUp instance did not accept the startup intent.", lastFailure);
    }

    private async Task ListenAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _names.PipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(_shutdown.Token).ConfigureAwait(false);
                using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                string? line = await reader.ReadLineAsync(_shutdown.Token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(line) &&
                    JsonSerializer.Deserialize<StartupIntent>(line) is { } intent)
                {
                    IntentReceived?.Invoke(intent);
                }
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
                break;
            }
            catch (IOException) when (!_shutdown.IsCancellationRequested)
            {
                await Task.Delay(50, _shutdown.Token).ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _shutdown.Cancel();
        if (_ownsMutex)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }
        }

        _mutex?.Dispose();
        _shutdown.Dispose();
    }
}
