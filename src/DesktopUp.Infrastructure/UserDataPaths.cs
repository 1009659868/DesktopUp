using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

public sealed class UserDataPaths : IUserDataPaths
{
    public UserDataPaths(string? rootOverride = null)
    {
        Root = Path.GetFullPath(rootOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopUp",
            "V3"));
        Logs = Path.Combine(Root, "logs");
        State = Path.Combine(Root, "state");
        Backups = Path.Combine(State, "backups");
        Temp = Path.Combine(Root, "temp");
    }

    public string Root { get; }
    public string Logs { get; }
    public string State { get; }
    public string Backups { get; }
    public string Temp { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(State);
        Directory.CreateDirectory(Backups);
        Directory.CreateDirectory(Temp);
    }
}
