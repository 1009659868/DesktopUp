using System.Runtime.InteropServices;

namespace DesktopUp.Infrastructure;

public sealed record ShellDirectoryResult(string? Destination, bool Cancelled, int HResult);
public interface IShellDirectoryTransfer
{
    Task<ShellDirectoryResult> Run(string source, string destination, bool copy, nint owner,
        Func<bool> current, CancellationToken token);
}

// Dedicated STA; Windows owns collision UI and physical execution. A returned destination
// comes from PostMove/CopyItem, not from guessing the name supplied to MoveItem.
public sealed class ShellDirectoryTransfer : IShellDirectoryTransfer
{
    public Task<ShellDirectoryResult> Run(string source, string destination, bool copy, nint owner,
        Func<bool> current, CancellationToken token)
    {
        var done = new TaskCompletionSource<ShellDirectoryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            int initialized = CoInitializeEx(0, 2); object? instance = null; IShellItem? from = null, to = null;
            IFileOperation? operation = null; uint cookie = 0;
            ShellDirectoryResult? outcome=null;Exception? failure=null;
            uint threadId=GetCurrentThreadId();
            // Cancel only dialogs owned by this operation's STA, never Explorer/user windows.
            using var cancellation=new Timer(_=>
            {
                if(token.IsCancellationRequested)EnumThreadWindows(threadId,(window,_)=>{PostMessage(window,0x0010,0,0);return true;},0);
            },null,200,200);
            try
            {
                Marshal.ThrowExceptionForHR(initialized); token.ThrowIfCancellationRequested();
                if (!current()) throw new IOException("文件或映射目录已变化，未执行。");
                instance = Activator.CreateInstance(Type.GetTypeFromCLSID(new("3AD05575-8857-4850-9277-11B85BDB8E09"))!);
                operation = (IFileOperation)instance!;
                var sink = new Sink(destination,current, token);
                Marshal.ThrowExceptionForHR(operation.Advise(sink, out cookie));
                // Do not suppress collision/error/permission prompts or request elevation.
                Marshal.ThrowExceptionForHR(operation.SetOperationFlags(0x200));
                if (owner != 0) Marshal.ThrowExceptionForHR(operation.SetOwnerWindow(owner));
                from = Item(source); to = Item(destination);
                Marshal.ThrowExceptionForHR(copy ? operation.CopyItem(from, to, null, 0) : operation.MoveItem(from, to, null, 0));
                int result = operation.PerformOperations();
                int queried = operation.GetAnyOperationsAborted(out bool aborted);
                if (queried < 0 && result >= 0) result = queried;
                outcome=new(sink.Destination, aborted || token.IsCancellationRequested, result);
                GC.KeepAlive(sink);
            }
            catch (Exception e) { failure=e; }
            finally
            {
                if (operation is not null && cookie != 0) { try { operation.Unadvise(cookie); } catch { } }
                foreach (object? value in new object?[] { to, from, instance })
                    if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
                if (initialized >= 0) CoUninitialize();
            }
            if(failure is not null)done.TrySetException(failure);else done.TrySetResult(outcome!);
        }) { IsBackground = true, Name = "DesktopUp mapped Shell transfer" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return done.Task;
    }
    private static IShellItem Item(string path)
    { var id = typeof(IShellItem).GUID; Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, 0, ref id, out var item)); return item; }
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class Sink(string directory,Func<bool> current, CancellationToken token) : IProgressSink
    {
        internal string? Destination;
        private int Check() { try { return token.IsCancellationRequested || !current() ? unchecked((int)0x80004004) : 0; } catch { return unchecked((int)0x80004004); } }
        private int Result(int hr, IShellItem original,IShellItem? item)
        {
            // COPYENGINE success codes need not equal S_OK (e.g. same-volume rename).
            if (hr >= 0 && item is not null)
                try
                {
                    string? Read(IShellItem x){if(x.GetDisplayName(0x80058000,out var p)<0)return null;try{return Marshal.PtrToStringUni(p);}finally{Marshal.FreeCoTaskMem(p);}}
                    string? path=Read(item);
                    // Shell may retarget the original IShellItem after a successful move.
                    // Only one root is queued; child callbacks cannot have this direct parent.
                    if(string.Equals(Path.GetDirectoryName(path),directory,StringComparison.OrdinalIgnoreCase))Destination=path;
                }
                catch { }
            return token.IsCancellationRequested ? unchecked((int)0x80004004) : 0;
        }
        public int StartOperations() => Check(); public int FinishOperations(int result) => 0;
        public int PreRenameItem(uint f, IShellItem i, string? n) => Check();
        public int PostRenameItem(uint f, IShellItem i, string? n, int r, IShellItem? x) => 0;
        public int PreMoveItem(uint f, IShellItem i, IShellItem d, string? n) => Check();
        public int PostMoveItem(uint f, IShellItem i, IShellItem d, string? n, int r, IShellItem? x) => Result(r,i,x);
        public int PreCopyItem(uint f, IShellItem i, IShellItem d, string? n) => Check();
        public int PostCopyItem(uint f, IShellItem i, IShellItem d, string? n, int r, IShellItem? x) => Result(r,i,x);
        public int PreDeleteItem(uint f, IShellItem i) => token.IsCancellationRequested ? unchecked((int)0x80004004) : 0;
        public int PostDeleteItem(uint f, IShellItem i, int r, IShellItem? x) => 0;
        public int PreNewItem(uint f, IShellItem d, string? n) => Check();
        public int PostNewItem(uint f, IShellItem d, string? n, string? t, uint a, int r, IShellItem? x) => 0;
        public int UpdateProgress(uint t, uint c) => token.IsCancellationRequested ? unchecked((int)0x80004004) : 0;
        public int ResetTimer() => 0; public int PauseTimer() => 0; public int ResumeTimer() => 0;
    }
    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem
    {
        [PreserveSig] int BindToHandler(nint b, ref Guid h, ref Guid i, out nint r);
        [PreserveSig] int GetParent(out IShellItem p);
        [PreserveSig] int GetDisplayName(uint kind, out nint name);
        [PreserveSig] int GetAttributes(uint mask, out uint attributes);
        [PreserveSig] int Compare(IShellItem other, uint hint, out int order);
    }
    [ComVisible(true), Guid("04B0F1A7-9490-44BC-96E1-4296A31252E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IProgressSink
    {
        [PreserveSig] int StartOperations(); [PreserveSig] int FinishOperations(int result);
        [PreserveSig] int PreRenameItem(uint f, IShellItem i, [MarshalAs(UnmanagedType.LPWStr)] string? n);
        [PreserveSig] int PostRenameItem(uint f, IShellItem i, [MarshalAs(UnmanagedType.LPWStr)] string? n, int r, IShellItem? x);
        [PreserveSig] int PreMoveItem(uint f, IShellItem i, IShellItem d, [MarshalAs(UnmanagedType.LPWStr)] string? n);
        [PreserveSig] int PostMoveItem(uint f, IShellItem i, IShellItem d, [MarshalAs(UnmanagedType.LPWStr)] string? n, int r, IShellItem? x);
        [PreserveSig] int PreCopyItem(uint f, IShellItem i, IShellItem d, [MarshalAs(UnmanagedType.LPWStr)] string? n);
        [PreserveSig] int PostCopyItem(uint f, IShellItem i, IShellItem d, [MarshalAs(UnmanagedType.LPWStr)] string? n, int r, IShellItem? x);
        [PreserveSig] int PreDeleteItem(uint f, IShellItem i);
        [PreserveSig] int PostDeleteItem(uint f, IShellItem i, int r, IShellItem? x);
        [PreserveSig] int PreNewItem(uint f, IShellItem d, [MarshalAs(UnmanagedType.LPWStr)] string? n);
        [PreserveSig] int PostNewItem(uint f, IShellItem d, [MarshalAs(UnmanagedType.LPWStr)] string? n, [MarshalAs(UnmanagedType.LPWStr)] string? t, uint a, int r, IShellItem? x);
        [PreserveSig] int UpdateProgress(uint t, uint c); [PreserveSig] int ResetTimer(); [PreserveSig] int PauseTimer(); [PreserveSig] int ResumeTimer();
    }
    [ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        [PreserveSig] int Advise(IProgressSink s, out uint cookie); [PreserveSig] int Unadvise(uint cookie);
        [PreserveSig] int SetOperationFlags(uint flags); [PreserveSig] int SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string text);
        [PreserveSig] int SetProgressDialog(nint d); [PreserveSig] int SetProperties(nint p); [PreserveSig] int SetOwnerWindow(nint w);
        [PreserveSig] int ApplyPropertiesToItem(IShellItem i); [PreserveSig] int ApplyPropertiesToItems(nint i);
        [PreserveSig] int RenameItem(IShellItem i, [MarshalAs(UnmanagedType.LPWStr)] string n, nint s);
        [PreserveSig] int RenameItems(nint i, [MarshalAs(UnmanagedType.LPWStr)] string n);
        [PreserveSig] int MoveItem(IShellItem i, IShellItem d, [MarshalAs(UnmanagedType.LPWStr)] string? n, nint s);
        [PreserveSig] int MoveItems(nint i, IShellItem d);
        [PreserveSig] int CopyItem(IShellItem i, IShellItem d, [MarshalAs(UnmanagedType.LPWStr)] string? n, nint s);
        [PreserveSig] int CopyItems(nint i, IShellItem d); [PreserveSig] int DeleteItem(IShellItem i, nint s); [PreserveSig] int DeleteItems(nint i);
        [PreserveSig] int NewItem(IShellItem d, uint a, [MarshalAs(UnmanagedType.LPWStr)] string n, [MarshalAs(UnmanagedType.LPWStr)] string? t, nint s);
        [PreserveSig] int PerformOperations(); [PreserveSig] int GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint mode);
    private delegate bool EnumWindow(nint window,nint parameter);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool EnumThreadWindows(uint threadId,EnumWindow callback,nint parameter);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(nint window,uint message,nint wParam,nint lParam);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHCreateItemFromParsingName(string path, nint context, ref Guid iid, out IShellItem item);
}
