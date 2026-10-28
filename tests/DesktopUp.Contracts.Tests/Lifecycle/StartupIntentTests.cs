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
    public void SecondPlainLaunchPreservesIntentForCurrentCatalog()
    {
        StartupIntent intent = new StartupIntentParser().Parse([]).ForExistingInstance();
        Assert.Equal(StartupIntentKind.StartBackground, intent.Kind);
    }

    [Theory]
    [InlineData(StartupIntentKind.StartBackground, false, true)]
    [InlineData(StartupIntentKind.StartBackground, true, false)]
    [InlineData(StartupIntentKind.ShowControlCenter, false, true)]
    [InlineData(StartupIntentKind.ShowControlCenter, true, true)]
    [InlineData(StartupIntentKind.Exit, false, false)]
    [InlineData(StartupIntentKind.Exit, true, false)]
    [InlineData(StartupIntentKind.AutoStart, false, false)]
    [InlineData(StartupIntentKind.AutoStart, true, false)]
    public void VisibilityUsesIntentAndCurrentPosterCatalog(StartupIntentKind kind, bool hasPosters, bool expected)
    {
        var intent = new StartupIntent(kind, [], DateTimeOffset.UtcNow);
        Assert.Equal(expected, intent.ShouldShowControlCenter(hasPosters));
        Assert.Equal(expected, intent.ForExistingInstance().ShouldShowControlCenter(hasPosters));
    }

    [Fact]
    public void ExplicitControlCenterIntentIsStable()
    {
        StartupIntent intent = new StartupIntentParser().Parse(["--show-control-center"]);
        Assert.Equal(StartupIntentKind.ShowControlCenter, intent.Kind);
        Assert.Equal(intent, intent.ForExistingInstance());
    }

    [Theory]
    [InlineData("--autostart", StartupIntentKind.AutoStart)]
    [InlineData("--AUTOSTART", StartupIntentKind.AutoStart)]
    [InlineData("--autostart-fake", StartupIntentKind.StartBackground)]
    public void AutoStartIsAnExactIntent(string argument, StartupIntentKind expected) =>
        Assert.Equal(expected, new StartupIntentParser().Parse([argument]).Kind);

    [Fact] public void ExplicitCommandsStillTakePriority() =>
        Assert.Equal(StartupIntentKind.ShowControlCenter, new StartupIntentParser().Parse(["--autostart", "--show-control-center"]).Kind);
}
