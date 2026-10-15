using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using DesktopUp.Application;
using DesktopUp.Infrastructure;
using F = DesktopUp.Contracts.Tests.ManagedTransferPersistenceTests.Fixture;

namespace DesktopUp.Contracts.Tests;

// Native integration: only fixture-owned files/folders and this test's helper process.
// Reads window visibility; never synthesizes input or touches Explorer/user windows.
[Collection("Native Shell UI")]
public sealed class ShellPropertyLifetimeTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task PropertySheetIsVisibleAndHelperStaysAliveUntilCancelled(bool directory)
    {
        using var f = new F(); string path = directory ? f.Source : f.File("property-lifetime.txt");
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "content-test-helpers", "DesktopUp.ContentWorker.exe"), "--shell-ui-v1")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using var process = Process.Start(start)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try
        {
            var request = new ShellUiRequest(Guid.NewGuid(), ShellUiAction.Properties, path, 0, 0) { Identity = LocalTransferFiles.Identity(path) };
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(request)); process.StandardInput.Close();
            Assert.Equal("ready", await process.StandardOutput.ReadLineAsync(timeout.Token));
            bool Visible()
            {
                bool seen = false;
                EnumWindows((window, _) => { GetWindowThreadProcessId(window, out uint pid);
                    if (pid == process.Id && IsWindowVisible(window)) seen = true; return true; }, 0);
                return seen;
            }
            for (int i = 0; i < 50 && !process.HasExited && !Visible(); i++) await Task.Delay(100, timeout.Token);
            Assert.False(process.HasExited, "SHObjectProperties returned but the helper exited before showing a property sheet.");
            Assert.True(Visible(), "No visible property sheet belongs to the isolated helper.");
            await Task.Delay(500, timeout.Token);
            Assert.False(process.HasExited); Assert.True(Visible());
        }
        finally
        {
            // The test owns this isolated helper. Closing it cannot terminate Explorer or
            // another application; this is also the app's shutdown/cancellation boundary.
            if (!process.HasExited) process.Kill(false);
            await process.WaitForExitAsync();
        }
    }
    private delegate bool EnumWindow(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, nint parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
}
