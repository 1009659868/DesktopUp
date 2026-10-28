using DesktopUp.Domain;
using DesktopUp.Windows;

namespace DesktopUp.Contracts.Tests;

public sealed class SingleInstanceTests
{
    [Fact]
    public async Task SecondaryInstanceForwardsOneIntentToPrimary()
    {
        string suffix = Guid.NewGuid().ToString("N");
        var names = new SingleInstanceNames($@"Local\DesktopUp.Test.{suffix}", $"DesktopUp.Test.{suffix}");
        using var primary = new SingleInstanceCoordinator(names);
        var received = new TaskCompletionSource<StartupIntent>(TaskCreationOptions.RunContinuationsAsynchronously);
        primary.IntentReceived += intent => received.TrySetResult(intent);
        Assert.True(primary.TryAcquireOrForward(StartupIntent.Background([])));

        using var secondary = new SingleInstanceCoordinator(names);
        var expected = new StartupIntent(StartupIntentKind.ShowControlCenter, [], DateTimeOffset.UtcNow);
        Assert.False(secondary.TryAcquireOrForward(expected));

        StartupIntent actual = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(StartupIntentKind.ShowControlCenter, actual.Kind);
    }
}
