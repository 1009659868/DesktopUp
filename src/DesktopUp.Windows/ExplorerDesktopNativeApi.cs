using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using DesktopUp.Application;

namespace DesktopUp.Windows;

internal interface IExplorerDesktopApi
{
    bool TryFindExistingHost(out DesktopShellHostIdentity identity);
    bool IsValid(DesktopShellHostIdentity identity);
}

internal sealed class ExplorerDesktopNativeApi : IExplorerDesktopApi
{
    private const string DesktopIconViewClass = "SHELLDLL_DefView";
    private static readonly HashSet<string> ContainerClasses =
        new(StringComparer.Ordinal)
        {
            "Progman",
            "WorkerW"
        };

    public bool TryFindExistingHost(out DesktopShellHostIdentity identity)
    {
        DesktopShellHostIdentity found = default;
        _ = EnumWindows((container, parameter) =>
        {
            nint iconView = FindWindowEx(container, 0, DesktopIconViewClass, null);
            if (iconView == 0)
            {
                return true;
            }

            _ = GetWindowThreadProcessId(iconView, out uint processId);
            var candidate = new DesktopShellHostIdentity(iconView, container, processId);
            if (!IsValid(candidate))
            {
                return true;
            }

            found = candidate;
            return false;
        }, 0);

        identity = found;
        return !identity.IsEmpty;
    }

    public bool IsValid(DesktopShellHostIdentity identity)
    {
        if (identity.IsEmpty ||
            !IsWindow(identity.IconViewHandle) ||
            !IsWindow(identity.ContainerHandle) ||
            GetParent(identity.IconViewHandle) != identity.ContainerHandle ||
            !string.Equals(GetWindowClass(identity.IconViewHandle), DesktopIconViewClass, StringComparison.Ordinal) ||
            !ContainerClasses.Contains(GetWindowClass(identity.ContainerHandle)))
        {
            return false;
        }

        _ = GetWindowThreadProcessId(identity.IconViewHandle, out uint iconViewProcessId);
        _ = GetWindowThreadProcessId(identity.ContainerHandle, out uint containerProcessId);
        if (iconViewProcessId == 0 ||
            iconViewProcessId != identity.ProcessId ||
            containerProcessId != identity.ProcessId)
        {
            return false;
        }

        try
        {
            using Process process = Process.GetProcessById(checked((int)identity.ProcessId));
            return string.Equals(process.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string GetWindowClass(nint handle)
    {
        var buffer = new StringBuilder(256);
        return GetClassName(handle, buffer, buffer.Capacity) > 0
            ? buffer.ToString()
            : string.Empty;
    }

    private delegate bool EnumWindowsCallback(nint window, nint parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

    [DllImport("user32.dll", EntryPoint = "FindWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindWindowEx(
        nint parent,
        nint childAfter,
        string? className,
        string? windowName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetParent(nint window);

    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(nint window, StringBuilder className, int maximumCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
