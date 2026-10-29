#requires -Version 5.1

# Advisory only. Never changes tokens, UAC, message filters, or launch identity.
function Compare-StudioLaunchPermission {
    param($Application, $Shell)
    if ($null -eq $Application -or $null -eq $Shell -or
        [string]::IsNullOrWhiteSpace($Application.UserSid) -or [string]::IsNullOrWhiteSpace($Shell.UserSid) -or
        $Application.IntegrityRid -le 0 -or $Shell.IntegrityRid -le 0) {
        return [pscustomobject]@{ Status='Unknown'; Reason='Incomplete token snapshot' }
    }
    $differences = @()
    if ($Application.UserSid -ne $Shell.UserSid) { $differences += '用户不同' }
    if ($Application.SessionId -ne $Shell.SessionId) { $differences += '会话不同' }
    if ($Application.IntegrityRid -ne $Shell.IntegrityRid) {
        $differences += ('完整性级别 DesktopUp=0x{0:X}，桌面=0x{1:X}' -f $Application.IntegrityRid, $Shell.IntegrityRid)
    }
    if ($differences.Count) { return [pscustomobject]@{ Status='Mismatch'; Reason=($differences -join '；') } }
    return [pscustomobject]@{ Status='Matched'; Reason='Same user, session and integrity' }
}

function Get-StudioLaunchPermission {
    param([Parameter(Mandatory)][int]$ProcessId)
    try {
        if (-not ('DesktopUp.StudioReview.TokenReader' -as [type])) {
            Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace DesktopUp.StudioReview {
    public sealed class TokenSnapshot {
        public string UserSid;
        public int SessionId;
        public int IntegrityRid;
    }
    public static class TokenReader {
        public static int ShellProcessId() {
            IntPtr shell = GetShellWindow(); uint id;
            if (shell == IntPtr.Zero || GetWindowThreadProcessId(shell, out id) == 0 || id == 0)
                throw new InvalidOperationException("Desktop shell unavailable");
            return checked((int)id);
        }
        public static TokenSnapshot Read(int id) {
            IntPtr process = OpenProcess(0x1000, false, id); // QUERY_LIMITED_INFORMATION
            if (process == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            IntPtr token = IntPtr.Zero;
            try {
                if (!OpenProcessToken(process, 8, out token)) // TOKEN_QUERY only
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                var result = new TokenSnapshot();
                result.UserSid = ReadInfo(token, 1, p => new SecurityIdentifier(Marshal.ReadIntPtr(p)).Value);
                result.SessionId = ReadInfo(token, 12, Marshal.ReadInt32);
                result.IntegrityRid = ReadInfo(token, 25, p => {
                    IntPtr sid = Marshal.ReadIntPtr(p);
                    if (!IsValidSid(sid)) throw new InvalidOperationException("Invalid integrity SID");
                    byte count = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
                    if (count == 0) throw new InvalidOperationException("Missing integrity RID");
                    return Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1)));
                });
                return result;
            } finally { if (token != IntPtr.Zero) CloseHandle(token); CloseHandle(process); }
        }
        private static T ReadInfo<T>(IntPtr token, int kind, Func<IntPtr, T> read) {
            bool scalar = kind == 12;
            int length = sizeof(int);
            if (!scalar) {
                GetTokenInformation(token, kind, IntPtr.Zero, 0, out length);
                int error = Marshal.GetLastWin32Error();
                if (error != 122 || length < IntPtr.Size || length > 65536)
                    throw new Win32Exception(error, "Cannot determine token information size");
            }
            IntPtr buffer = Marshal.AllocHGlobal(length);
            try {
                int returned;
                if (!GetTokenInformation(token, kind, buffer, length, out returned))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (returned > length || returned < (scalar ? sizeof(int) : IntPtr.Size))
                    throw new InvalidOperationException("Invalid token information size");
                return read(buffer);
            } finally { Marshal.FreeHGlobal(buffer); }
        }
        [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint id);
        [DllImport("kernel32.dll", SetLastError=true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int id);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        [DllImport("advapi32.dll", SetLastError=true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("advapi32.dll", SetLastError=true)] private static extern bool GetTokenInformation(IntPtr token, int kind, IntPtr data, int size, out int returned);
        [DllImport("advapi32.dll")] private static extern bool IsValidSid(IntPtr sid);
        [DllImport("advapi32.dll")] private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
        [DllImport("advapi32.dll")] private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);
    }
}
'@
        }
        $desktopShellPid = [DesktopUp.StudioReview.TokenReader]::ShellProcessId()
        $application = [DesktopUp.StudioReview.TokenReader]::Read($ProcessId)
        $shell = [DesktopUp.StudioReview.TokenReader]::Read($desktopShellPid)
        if ([DesktopUp.StudioReview.TokenReader]::ShellProcessId() -ne $desktopShellPid) {
            return [pscustomobject]@{ Status='Unknown'; Reason='Desktop shell changed during query' }
        }
        return Compare-StudioLaunchPermission -Application $application -Shell $shell
    } catch {
        # A diagnostic failure must not stop the already-started review or claim a mismatch.
        return [pscustomobject]@{ Status='Unknown'; Reason=$_.Exception.Message }
    }
}
