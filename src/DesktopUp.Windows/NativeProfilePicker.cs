using System.ComponentModel;
using System.Runtime.InteropServices;
using DesktopUp.Application;

namespace DesktopUp.Windows;

public enum NativePickerPurpose { Profile, ReferenceFile, ReferenceFolder, ManagedStorageFolder, CardIcon }

/// <summary>
/// Owns exactly one common-item dialog and one STA message endpoint. Cancel posts to that
/// apartment and requests WM_CLOSE on that dialog's verified HWND; Completion is published only after Show returns
/// and COM/window/ledger cleanup finishes. It never enumerates or closes another window.
/// This selects a path only; atomic file IO remains with PortableProfileFile.
/// </summary>
public sealed class NativeProfilePicker
{
    private const int Cancelled = unchecked((int)0x800704C7);
    private const uint CancelMessage = 0x8000 + 73, WmTimer = 0x0113;
    private readonly PickerCancellationGate _gate = new();
    private readonly TaskCompletionSource<string?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<nint> _shown = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IStructuredLog _log;
    private readonly IResourceLedger _ledger;
    private readonly string _id = Guid.NewGuid().ToString("N");
    private readonly bool _save;
    private readonly NativePickerPurpose _purpose;
    private readonly nint _owner;
    public Task<string?> Completion => _completion.Task;
    public Task<nint> Shown => _shown.Task;

    public NativeProfilePicker(nint owner, bool save, IResourceLedger ledger, IStructuredLog log,
        bool initiallyCancelled = false, NativePickerPurpose purpose = NativePickerPurpose.Profile)
    {
        if (!IsWindow(owner)) throw new ArgumentException("A live owner window is required.", nameof(owner));
        GetWindowThreadProcessId(owner, out uint process);
        if (process != Environment.ProcessId) throw new ArgumentException("Picker owner must belong to this process.");
        if (!Enum.IsDefined(purpose) || save && purpose != NativePickerPurpose.Profile) throw new ArgumentException("Invalid picker purpose.");
        _owner = owner; _save = save; _ledger = ledger; _log = log; _purpose = purpose;
        if (initiallyCancelled) _gate.Cancel();
        var thread = new Thread(Run) { IsBackground = true, Name = "DesktopUp profile picker" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public void Cancel()
    {
        Trace("cancel_requested");
        _gate.Cancel();
    }

    public static bool IsDialogAlive(nint window) => window != 0 && IsWindow(window);

    private void Trace(string stage, object? detail = null) =>
        _log.Write("info", "portable_picker.lifecycle", new { id = _id, save = _save, stage, detail });

    private void Run()
    {
        Native.IFileDialog? dialog = null;
        IDisposable? lease = null;
        nint endpoint = 0, dialogWindow = 0;
        string className = "DesktopUp.ProfilePicker." + _id;
        string? path = null;
        Exception? failure = null;
        bool com = false, registered = false, closeSent = false;
        uint apartmentThread = GetCurrentThreadId();
        nint module = GetModuleHandle(null);
        WindowProc callback = (hwnd, message, wParam, lParam) =>
        {
            if (message is CancelMessage or WmTimer)
            {
                try
                {
                    if (dialog is not null)
                    {
                        // This callback runs inside Show's modal pump on the dialog's STA.
                        var window = (Native.IOleWindow)dialog;
                        if (window.GetWindow(out nint handle) >= 0 && IsWindowVisible(handle))
                        {
                            uint windowThread = GetWindowThreadProcessId(handle, out uint windowProcess);
                            if (windowProcess != Environment.ProcessId || windowThread != apartmentThread)
                                throw new InvalidOperationException("Picker HWND does not belong to its owning apartment.");
                            dialogWindow = handle;
                            if (_shown.TrySetResult(handle)) Trace("shown");
                            if (_gate.IsCancellationRequested && !closeSent)
                            {
                                // Use the native cancel/close path, not a success HRESULT as completion proof.
                                // Same-STA synchronous delivery cannot race with destruction/HWND reuse.
                                // No enumeration, owner closing, EndDialog or DestroyWindow on the picker.
                                closeSent = true;
                                SendMessage(handle, 0x0010 /* WM_CLOSE */, 0, 0);
                                Trace("close_requested", new { windowProcess, windowThread, apartmentThread });
                            }
                        }
                    }
                }
                catch (Exception error) { _log.Write("error", "portable_picker.close_failed", new { _id, error.Message }); }
                return 0;
            }
            return DefWindowProc(hwnd, message, wParam, lParam);
        };
        try
        {
            lease = _ledger.Acquire("picker.native." + _id, "sta-common-item-dialog");
            if (!_gate.IsCancellationRequested)
            {
                Marshal.ThrowExceptionForHR(CoInitializeEx(0, 2 | 4)); com = true;
                var wc = new WindowClass
                {
                    Size = (uint)Marshal.SizeOf<WindowClass>(), Instance = module,
                    Procedure = Marshal.GetFunctionPointerForDelegate(callback), ClassName = className
                };
                if (RegisterClassEx(ref wc) == 0) throw new Win32Exception();
                registered = true;
                endpoint = CreateWindowEx(0, className, "", 0, 0, 0, 0, 0, new nint(-3), 0, module, 0);
                if (endpoint == 0) throw new Win32Exception();
                nint target = endpoint;
                _gate.Bind(() =>
                {
                    if (!PostMessage(target, CancelMessage, 0, 0))
                        _log.Write("error", "portable_picker.signal_failed", new { _id, error = Marshal.GetLastWin32Error() });
                });
                Guid clsid = new(_save ? "C0B4E2F3-BA21-4773-8DBA-335EC946EB8B" : "DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7");
                dialog = (Native.IFileDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(clsid, throwOnError: true)!)!;
                Marshal.ThrowExceptionForHR(dialog.GetOptions(out uint options));
                // Filesystem only, do not change cwd or write test files. Save retains overwrite prompt.
                Marshal.ThrowExceptionForHR(dialog.SetOptions(options | 0x40 | 0x8 | 0x800 | 0x02000000 |
                    (_save ? 0x2u : 0x1000u) | (_purpose == NativePickerPurpose.Profile ? 0u : 0x100000u) |
                    (_purpose is NativePickerPurpose.ReferenceFolder or NativePickerPurpose.ManagedStorageFolder ? 0x20u : 0u)));
                if (_purpose is not (NativePickerPurpose.ReferenceFolder or NativePickerPurpose.ManagedStorageFolder))
                    Marshal.ThrowExceptionForHR(dialog.SetFileTypes(1, [new(
                        _purpose == NativePickerPurpose.CardIcon ? "静态图标图片（PNG、JPEG、ICO）" : _purpose == NativePickerPurpose.Profile ? "DesktopUp 配置" : "所有文件",
                        _purpose == NativePickerPurpose.CardIcon ? "*.png;*.jpg;*.jpeg;*.ico" : _purpose == NativePickerPurpose.Profile ? "*.json" : "*.*")]));
                if (_purpose == NativePickerPurpose.Profile) Marshal.ThrowExceptionForHR(dialog.SetDefaultExtension("json"));
                Marshal.ThrowExceptionForHR(dialog.SetTitle(_purpose == NativePickerPurpose.CardIcon ? "选择当前卡片的图标（不改变真实来源）" : _purpose == NativePickerPurpose.ManagedStorageFolder ? "选择持久收纳根目录（不移动现有文件）" : _purpose == NativePickerPurpose.ReferenceFolder ? "选择要引用的文件夹" :
                    _purpose == NativePickerPurpose.ReferenceFile ? "选择要引用的文件（不移动原文件）" : _save ? "导出 DesktopUp 习惯配置" : "导入 DesktopUp 习惯配置"));
                if (_save) Marshal.ThrowExceptionForHR(dialog.SetFileName("DesktopUp-habits.desktopup.json"));
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (Directory.Exists(documents))
                {
                    Guid iid = typeof(Native.IShellItem).GUID;
                    if (Native.SHCreateItemFromParsingName(documents, 0, ref iid, out var folder) >= 0)
                    {
                        try { Marshal.ThrowExceptionForHR(dialog.SetDefaultFolder(folder)); }
                        finally { Marshal.FinalReleaseComObject(folder); }
                    }
                }
                if (!_gate.IsCancellationRequested)
                {
                    // Timer also retries an early cancellation until the dialog has its HWND.
                    if (SetTimer(endpoint, 1, 100, 0) == 0) throw new Win32Exception();
                    Trace("show_begin");
                    int hr = dialog.Show(_owner);
                    Trace("show_returned", new { hr });
                    if (hr != Cancelled)
                    {
                        Marshal.ThrowExceptionForHR(hr);
                        if (!_gate.IsCancellationRequested)
                        {
                            Marshal.ThrowExceptionForHR(dialog.GetResult(out var item));
                            try
                            {
                                Marshal.ThrowExceptionForHR(item.GetDisplayName(0x80058000, out nint value));
                                try { path = Marshal.PtrToStringUni(value); }
                                finally { Marshal.FreeCoTaskMem(value); }
                            }
                            finally { Marshal.FinalReleaseComObject(item); }
                        }
                    }
                }
            }
        }
        catch (Exception error) { failure = error; }
        finally
        {
            // No future Cancel may post to an HWND that could later be reused.
            path = _gate.Complete(path);
            try
            {
                if (endpoint != 0) KillTimer(endpoint, 1);
                if (dialog is not null) Marshal.FinalReleaseComObject(dialog);
                if (endpoint != 0 && !DestroyWindow(endpoint)) throw new Win32Exception();
                if (registered && !UnregisterClass(className, module)) throw new Win32Exception();
            }
            catch (Exception error) { failure ??= error; }
            if (com) CoUninitialize();
            GC.KeepAlive(callback);
            try { lease?.Dispose(); } catch (Exception error) { failure ??= error; }
            _shown.TrySetResult(0);
            Trace("completed", new { cancelled = _gate.IsCancellationRequested,
                dialogAlive = IsDialogAlive(dialogWindow), failed = failure is not null });
            if (failure is not null) _completion.TrySetException(failure);
            else _completion.TrySetResult(path);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowProc(nint hwnd, uint message, nuint wParam, nint lParam);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size, Style;
        public nint Procedure;
        public int ClassExtra, WindowExtra;
        public nint Instance, Icon, Cursor, Background;
        public string? MenuName;
        public string ClassName;
        public nint SmallIcon;
    }

    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WindowClass value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterClass(string name, nint module);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowEx(uint ex, string cls, string title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint DefWindowProc(nint hwnd, uint message, nuint wp, nint lp);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint SendMessage(nint hwnd, uint message, nuint wp, nint lp);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(nint hwnd, uint msg, nuint wp, nint lp);
    [DllImport("user32.dll", SetLastError = true)] private static extern nuint SetTimer(nint hwnd, nuint id, uint milliseconds, nint callback);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool KillTimer(nint hwnd, nuint id);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct Filter(string name, string spec)
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string Name = name;
            [MarshalAs(UnmanagedType.LPWStr)] public string Spec = spec;
        }
        [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IFileDialog
        {
            [PreserveSig] int Show(nint owner);
            [PreserveSig] int SetFileTypes(uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] Filter[] filters);
            [PreserveSig] int SetFileTypeIndex(uint index);
            [PreserveSig] int GetFileTypeIndex(out uint index);
            [PreserveSig] int Advise(nint events, out uint cookie);
            [PreserveSig] int Unadvise(uint cookie);
            [PreserveSig] int SetOptions(uint options);
            [PreserveSig] int GetOptions(out uint options);
            [PreserveSig] int SetDefaultFolder(IShellItem folder);
            [PreserveSig] int SetFolder(IShellItem folder);
            [PreserveSig] int GetFolder(out IShellItem folder);
            [PreserveSig] int GetCurrentSelection(out IShellItem item);
            [PreserveSig] int SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            [PreserveSig] int GetFileName(out nint name);
            [PreserveSig] int SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            [PreserveSig] int SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            [PreserveSig] int SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            [PreserveSig] int GetResult(out IShellItem item);
            [PreserveSig] int AddPlace(IShellItem item, int placement);
            [PreserveSig] int SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
            [PreserveSig] int Close(int result);
            [PreserveSig] int SetClientGuid(ref Guid guid);
            [PreserveSig] int ClearClientData();
            [PreserveSig] int SetFilter(nint filter);
        }
        [ComImport, Guid("00000114-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IOleWindow
        {
            [PreserveSig] int GetWindow(out nint hwnd);
            [PreserveSig] int ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool enter);
        }
        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellItem
        {
            [PreserveSig] int BindToHandler(nint ctx, ref Guid handler, ref Guid iid, out nint result);
            [PreserveSig] int GetParent(out IShellItem parent);
            [PreserveSig] int GetDisplayName(uint kind, out nint value);
            [PreserveSig] int GetAttributes(uint mask, out uint attributes);
            [PreserveSig] int Compare(IShellItem other, uint hint, out int order);
        }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        internal static extern int SHCreateItemFromParsingName(string path, nint context, ref Guid iid, out IShellItem item);
    }
}
