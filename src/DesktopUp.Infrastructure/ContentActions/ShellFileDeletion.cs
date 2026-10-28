using System.Runtime.InteropServices;
using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

public sealed partial class ShellDirectoryTransfer : IShellFileDeletion
{
    // Native recycle + Explorer undo; never suppress permanent-delete warnings or errors.
    internal const uint RecycleFlags = 0x00080000 | 0x20000000 | 0x0040 | 0x4000 | 0x2000;
    public Task<ShellDeleteResult> Recycle(string path, nint owner, Func<bool> current, CancellationToken token)
    {
        var done = new TaskCompletionSource<ShellDeleteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            int initialized = CoInitializeEx(0, 2); object? instance = null; IShellItem? item = null;
            IFileOperation? operation = null; uint cookie = 0;
            ShellDeleteResult? outcome = null; Exception? failure = null;
            uint threadId = GetCurrentThreadId();
            using var cancellation = new Timer(_ =>
            {
                if (token.IsCancellationRequested)
                    EnumThreadWindows(threadId, (window, _) => { PostMessage(window, 0x0010, 0, 0); return true; }, 0);
            }, null, 200, 200);
            try
            {
                Marshal.ThrowExceptionForHR(initialized); token.ThrowIfCancellationRequested();
                if (!current()) throw new IOException("文件已变化，未删除。");
                instance = Activator.CreateInstance(Type.GetTypeFromCLSID(new("3AD05575-8857-4850-9277-11B85BDB8E09"))!);
                operation = (IFileOperation)instance!;
                var sink = new DeleteSink(current, token);
                Marshal.ThrowExceptionForHR(operation.Advise(sink, out cookie));
                Marshal.ThrowExceptionForHR(operation.SetOperationFlags(RecycleFlags));
                if (owner != 0) Marshal.ThrowExceptionForHR(operation.SetOwnerWindow(owner));
                item = Item(path);
                Marshal.ThrowExceptionForHR(operation.DeleteItem(item, 0));
                int hr = operation.PerformOperations();
                int queried = operation.GetAnyOperationsAborted(out bool aborted);
                if (queried < 0 && hr >= 0) hr = queried;
                outcome = new(aborted || token.IsCancellationRequested, hr);
                GC.KeepAlive(sink);
            }
            catch (Exception e) { failure = e; }
            finally
            {
                if (operation is not null && cookie != 0) try { operation.Unadvise(cookie); } catch { }
                foreach (object? value in new object?[] { item, instance })
                    if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
                if (initialized >= 0) CoUninitialize();
            }
            if (failure is not null) done.TrySetException(failure); else done.TrySetResult(outcome!);
        }) { IsBackground = true, Name = "DesktopUp Shell recycle" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return done.Task;
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class DeleteSink(Func<bool> current, CancellationToken token) : IProgressSink
    {
        private int Check() { try { return token.IsCancellationRequested || !current() ? unchecked((int)0x80004004) : 0; } catch { return unchecked((int)0x80004004); } }
        public int StartOperations() => Check();
        public int FinishOperations(int result) => 0;
        public int PreDeleteItem(uint f, IShellItem i) => Check();
        public int PostDeleteItem(uint f, IShellItem i, int r, IShellItem? x) => 0;
        public int PreRenameItem(uint f, IShellItem i, string? n) => Check();
        public int PostRenameItem(uint f, IShellItem i, string? n, int r, IShellItem? x) => 0;
        public int PreMoveItem(uint f, IShellItem i, IShellItem d, string? n) => Check();
        public int PostMoveItem(uint f, IShellItem i, IShellItem d, string? n, int r, IShellItem? x) => 0;
        public int PreCopyItem(uint f, IShellItem i, IShellItem d, string? n) => Check();
        public int PostCopyItem(uint f, IShellItem i, IShellItem d, string? n, int r, IShellItem? x) => 0;
        public int PreNewItem(uint f, IShellItem d, string? n) => Check();
        public int PostNewItem(uint f, IShellItem d, string? n, string? t, uint a, int r, IShellItem? x) => 0;
        public int UpdateProgress(uint t, uint c) => token.IsCancellationRequested ? unchecked((int)0x80004004) : 0;
        public int ResetTimer() => 0; public int PauseTimer() => 0; public int ResumeTimer() => 0;
    }
}
