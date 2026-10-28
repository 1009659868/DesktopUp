using System.Text.Json;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed class InfrastructureTests
{
    [Fact]
    public void DefaultDataRootIsOutsideInstallDirectory()
    {
        var paths = new UserDataPaths();
        string appBase = Path.GetFullPath(AppContext.BaseDirectory);
        Assert.False(paths.Root.StartsWith(appBase, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("DesktopUp", paths.Root, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LedgerEndsCleanAfterEveryLeaseIsDisposed()
    {
        string root = Path.Combine(Path.GetTempPath(), "DesktopUp.V3.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new UserDataPaths(root);
            var ledger = new ResourceLedger(paths);
            using (ledger.Acquire("test.window", "test"))
            {
                Assert.Single(ledger.ActiveResources);
            }
            Assert.Empty(ledger.ActiveResources);
            using JsonDocument snapshot = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(paths.State, "resource-ledger.json")));
            Assert.True(snapshot.RootElement.GetProperty("clean").GetBoolean());
            Assert.Equal(0, snapshot.RootElement.GetProperty("resources").GetArrayLength());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
