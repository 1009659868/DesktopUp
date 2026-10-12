using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace DesktopUp.NativeDragProbe;

internal sealed record ProbeProcessToken(int ProcessId, string UserSid, int SessionId,
    int IntegrityRid, bool Elevated, int ElevationType, bool UiAccess);

internal sealed record ProbeEnvironmentResult(bool Allowed, string Kind, string Reason,
    ProbeProcessToken? Probe, ProbeProcessToken? Explorer);

/// <summary>Admission for this no-file-action probe only, not a UIPI override or native test result.</summary>
internal static class ProbeEnvironment
{
    internal static ProbeEnvironmentResult Evaluate(ProbeProcessToken? probe, ProbeProcessToken? explorer,
        bool shellStable = true, string? queryError = null)
    {
        ProbeEnvironmentResult Reject(string reason) => new(false, "Rejected", reason, probe, explorer);
        if (queryError is not null) return Reject("QueryFailed: " + queryError);
        if (probe is null || explorer is null) return Reject("MissingTokenSnapshot");
        if (!shellStable) return Reject("DesktopShellChanged");
        if (probe.ProcessId <= 0 || explorer.ProcessId <= 0 ||
            string.IsNullOrWhiteSpace(probe.UserSid) || string.IsNullOrWhiteSpace(explorer.UserSid) ||
            probe.SessionId <= 0 || explorer.SessionId <= 0 ||
            probe.ElevationType is < 1 or > 3 || explorer.ElevationType is < 1 or > 3)
            return Reject("IncompleteTokenSnapshot");
        if (!string.Equals(probe.UserSid, explorer.UserSid, StringComparison.OrdinalIgnoreCase))
            return Reject("DifferentUser");
        if (probe.SessionId != explorer.SessionId) return Reject("DifferentSession");
        if (probe.IntegrityRid != explorer.IntegrityRid) return Reject("DifferentIntegrity");
        if (probe.UiAccess || explorer.UiAccess) return Reject("UiAccessUnsupported");
        // Low, medium-plus and system tokens are outside this desktop probe's verified scope.
        return probe.IntegrityRid switch
        {
            0x2000 => new(true, "MediumIntegrityMatched", "SameUserSessionIntegrity", probe, explorer),
            0x3000 => new(true, "HighIntegrityMatched", "LocalProbeOnly_NotMediumPrivilegeEvidence", probe, explorer),
            _ => Reject("UnsupportedIntegrity")
        };
    }

    internal static ProbeEnvironmentResult Inspect()
    {
        ProbeProcessToken? probe = null, explorer = null;
        try
        {
            var shell = Native.GetShellWindow();
            if (shell == 0 || Native.GetWindowThreadProcessId(shell, out uint shellPid) == 0 || shellPid == 0)
                return Evaluate(null, null, queryError: "DesktopShellUnavailable");
            probe = ReadToken(Environment.ProcessId, false);
            explorer = ReadToken(checked((int)shellPid), true);
            var currentShell = Native.GetShellWindow();
            bool stable = currentShell == shell &&
                Native.GetWindowThreadProcessId(currentShell, out uint currentPid) != 0 && currentPid == shellPid;
            return Evaluate(probe, explorer, stable);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or
            System.Security.SecurityException or ArgumentException or OverflowException)
        {
            return Evaluate(probe, explorer, queryError: error.Message);
        }
    }

    internal static ProbeProcessToken ReadToken(int pid, bool requireExplorer = false)
    {
        using var process = Native.OpenProcess(0x1000, false, pid); // QUERY_LIMITED_INFORMATION only
        if (process.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (requireExplorer)
        {
            var path = new System.Text.StringBuilder(32768);
            uint length = (uint)path.Capacity;
            if (!Native.QueryFullProcessImageName(process, 0, path, ref length))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            if (!string.Equals(path.ToString(), expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("DesktopShellIsNotWindowsExplorer");
        }
        if (!Native.OpenProcessToken(process, 0x0008, out var token)) // TOKEN_QUERY only
            throw new Win32Exception(Marshal.GetLastWin32Error());
        using (token)
        {
            string sid = ReadInfo(token, 1, data => new SecurityIdentifier(Marshal.ReadIntPtr(data)).Value);
            int integrity = ReadInfo(token, 25, data =>
            {
                var integritySid = Marshal.ReadIntPtr(data);
                if (!Native.IsValidSid(integritySid)) throw new InvalidOperationException("InvalidIntegritySid");
                int count = Marshal.ReadByte(Native.GetSidSubAuthorityCount(integritySid));
                if (count == 0) throw new InvalidOperationException("MissingIntegrityRid");
                return Marshal.ReadInt32(Native.GetSidSubAuthority(integritySid, (uint)(count - 1)));
            });
            return new(pid, sid, ReadInfo(token, 12, Marshal.ReadInt32), integrity,
                ReadInfo(token, 20, Marshal.ReadInt32) != 0, ReadInfo(token, 18, Marshal.ReadInt32),
                ReadInfo(token, 26, Marshal.ReadInt32) != 0);
        }
    }

    private static T ReadInfo<T>(SafeAccessTokenHandle token, int kind, Func<nint, T> read)
    {
        // Fixed DWORD queries (notably TokenElevation) can return ERROR_BAD_LENGTH on a null
        // sizing call. Allocate their documented size directly; variable SID records use sizing.
        int length = sizeof(int);
        bool scalar = kind is 12 or 18 or 20 or 26;
        if (!scalar)
        {
            Native.GetTokenInformation(token, kind, 0, 0, out length);
            int error = Marshal.GetLastWin32Error();
            if (error != 122 || length < IntPtr.Size || length > 65536)
                throw new Win32Exception(error, $"Cannot determine token information size (class={kind}, error={error}, length={length}).");
        }
        var data = Marshal.AllocHGlobal(length);
        try
        {
            if (!Native.GetTokenInformation(token, kind, data, length, out int returned))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (returned > length || returned < (scalar ? sizeof(int) : IntPtr.Size))
                throw new InvalidOperationException("InvalidTokenInformationSize");
            return read(data);
        }
        finally { Marshal.FreeHGlobal(data); }
    }

    private static class Native
    {
        [DllImport("user32.dll")] internal static extern nint GetShellWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint window, out uint pid);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, System.Text.StringBuilder path, ref uint size);
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetTokenInformation(SafeAccessTokenHandle token, int kind, nint data, int size, out int returned);
        [DllImport("advapi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsValidSid(nint sid);
        [DllImport("advapi32.dll")] internal static extern nint GetSidSubAuthorityCount(nint sid);
        [DllImport("advapi32.dll")] internal static extern nint GetSidSubAuthority(nint sid, uint index);
    }
}
