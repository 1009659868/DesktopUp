using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class StartupIntentTests
{
    [Fact]
    public void FirstPlainLaunchStartsInBackground()
    {
        StartupIntent intent = new StartupIntentParser().Parse([]);
        Assert.Equal(StartupIntentKind.StartBackground, intent.Kind);
    }

    [Fact]
    public void SecondPlainLaunchRequestsControlCenter()
    {
        StartupIntent intent = new StartupIntentParser().Parse([]).ForExistingInstance();
        Assert.Equal(StartupIntentKind.ShowControlCenter, intent.Kind);
    }

    [Fact]
    public void ExplicitControlCenterIntentIsStable()
    {
        StartupIntent intent = new StartupIntentParser().Parse(["--show-control-center"]);
        Assert.Equal(StartupIntentKind.ShowControlCenter, intent.Kind);
        Assert.Equal(intent, intent.ForExistingInstance());
    }
}
