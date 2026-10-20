namespace DesktopUp.Application;

public sealed record StartupEntry(bool Exists, string? Command);
public sealed record InstalledStartupTarget(string LauncherPath)
{
    public string Command => $"\"{LauncherPath}\" --autostart";
}
public enum StartupRegistrationState { Unavailable, Off, Registered, OtherInstallation, Unknown }
public sealed record StartupRegistrationSnapshot(StartupRegistrationState State, string Message)
{
    public bool RegisteredHere => State == StartupRegistrationState.Registered;
    public bool CanChange => State is StartupRegistrationState.Off or StartupRegistrationState.Registered;
}
public sealed record StartupRegistrationChange(bool Succeeded, StartupRegistrationSnapshot Snapshot, string? Error = null);

// Registry/installation IO is isolated from UI and preference persistence. A saved
// preference is never authority to register on launch or after a profile import.
public interface IStartupRegistrationBackend
{
    InstalledStartupTarget ResolveInstallation();
    StartupEntry Read();
    bool TryReplace(StartupEntry expected, string? command);
}

public sealed class StartupRegistrationService(IStartupRegistrationBackend backend)
{
    private readonly object gate = new();
    public StartupRegistrationSnapshot Read()
    {
        lock (gate)
        {
            try { return Describe(Resolve(), backend.Read()); }
            catch (Exception e) when (IsExpected(e)) { return new(StartupRegistrationState.Unavailable, e.Message); }
        }
    }

    public StartupRegistrationChange SetEnabled(bool enabled)
    {
        lock (gate)
        {
            try
            {
                var target = Resolve(); var before = backend.Read();
                var state = Describe(target, before);
                if (!state.CanChange) return new(false, state, state.Message);
                string? desired = enabled ? target.Command : null;
                if (before.Exists != enabled || (enabled && before.Command != desired))
                {
                    if (!backend.TryReplace(before, desired))
                        return new(false, Read(), "启动项已被其他操作更改；未覆盖，请重新查看。");
                }
                // Verify installation and registration again; a write return is not proof.
                var after = Describe(Resolve(), backend.Read());
                bool verified = enabled ? after.RegisteredHere : after.State == StartupRegistrationState.Off;
                return new(verified, after, verified ? null : "启动登记未能确认；以当前读回状态为准。");
            }
            catch (Exception e) when (IsExpected(e)) { return new(false, Read(), "未完成启动项设置：" + e.Message); }
        }
    }

    private InstalledStartupTarget Resolve()
    {
        var target = backend.ResolveInstallation();
        if (!Path.IsPathFullyQualified(target.LauncherPath) || target.LauncherPath.Contains('"') ||
            target.LauncherPath.Any(char.IsControl) || target.Command.Length > 260)
            throw new IOException("安装路径不适用于 Windows 启动登记（命令需不超过260字符）。");
        return target;
    }
    private static StartupRegistrationSnapshot Describe(InstalledStartupTarget target, StartupEntry entry) =>
        !entry.Exists ? new(StartupRegistrationState.Off, "未登记开机自启动。") :
        string.Equals(entry.Command, target.Command, StringComparison.OrdinalIgnoreCase)
            ? new(StartupRegistrationState.Registered,
                "已登记当前安装。Windows 的允许/禁用状态请在“启动应用”中确认；本开关不覆盖系统禁用。")
            : new(StartupRegistrationState.OtherInstallation,
                "DesktopUp 启动项由其他安装或命令占用；请先在对应版本关闭，不会自动覆盖。" );
    private static bool IsExpected(Exception e) => e is IOException or UnauthorizedAccessException or
        System.Security.SecurityException or ArgumentException or InvalidOperationException;
}
