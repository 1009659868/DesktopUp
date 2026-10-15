using DesktopUp.ContentWorker;

internal static class ShellNewProbe
{
    // Selection injection exists only in this test executable, never a worker CLI flag.
    internal static int Run(string kind) => kind is "folder" or "text" ?
        ShellUiHost.Run(testSelection: commands => commands.GetValueOrDefault(kind=="folder" ? "NewFolder" : ".txt")) : 2;
}
