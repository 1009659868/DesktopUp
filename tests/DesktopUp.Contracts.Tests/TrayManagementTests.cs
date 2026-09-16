using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed partial class PosterSessionRegistryTests
{
    [Fact]
    public void TrayGlobalRestorePreservesIndividualVisibilityAndRevision()
    {
        using var f = new Fixture();
        string b = f.Add("B"), c = f.Add("C");
        f.Set(c, p => p with { IsVisible = false }, PosterStateCommandKind.SetVisible);
        Assert.True(f.Recover());
        long revision = f.Current.Revision;
        for (int i = 0; i < 3; i++)
        {
            f.Registry.SetGloballyHidden(true);
            Assert.Empty(f.Registry.Sessions);
            Assert.False(f.Registry.NeedsRecovery(Environment(1).Shell));
            Assert.True(f.Recover()); Assert.Empty(f.Registry.Sessions);
            f.Registry.SetGloballyHidden(false);
            Assert.Equal(2, f.Registry.Sessions.Count);
            Assert.NotNull(f.Registry.Find(Main)); Assert.NotNull(f.Registry.Find(b));
            Assert.Null(f.Registry.Find(c)); Assert.Equal(revision, f.Current.Revision);
        }
    }

    [Fact]
    public void TrayGlobalHiddenRestoresLatestCatalogAfterRecovery()
    {
        using var f = new Fixture();
        string b = f.Add("B"); Assert.True(f.Recover());
        f.Registry.SetGloballyHidden(true); f.Remove(Main);
        string c = f.Add("C");
        f.Set(b, p => p with { IsVisible = false }, PosterStateCommandKind.SetVisible);
        Assert.Empty(f.Registry.Sessions);
        f.Registry.SuspendEnvironment(2); f.Registry.SetGloballyHidden(false);
        Assert.Null(f.Registry.Find(c)); Assert.True(f.Recover(2));
        Assert.Equal(c, Assert.Single(f.Registry.Sessions).PosterId);
    }

    [Fact]
    public void TrayHideReentrantCreateAndExitCannotReviveSession()
    {
        using var f = new Fixture();
        f.Factory.OnStart = _ => f.Registry.SetGloballyHidden(true);
        Assert.True(f.Recover()); Assert.Empty(f.Registry.Sessions);
        Assert.True(Assert.Single(f.Factory.Created).Disposed);
        f.Registry.Dispose(); f.Registry.SetGloballyHidden(false);
        Assert.True(f.Registry.GloballyHidden); Assert.False(f.Recover(2));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public void TrayCreateFreezesCatalogAndSource(int count)
    {
        using var f = new Fixture();
        if (count == 0) f.Remove(Main);
        for (int i = 1; i < count; i++) f.Add("same name");
        var request = TrayManagement.Create(f.Current, Id());
        Assert.Equal(StateCommandSource.Tray, request.Source);
        Assert.Equal(count, request.Reads.Posters.Count);
        Assert.Equal(f.Current.CatalogVersion, request.Reads.CatalogVersion);
        Assert.Null(request.CloneSourceId);
        f.Add("later"); Assert.Equal(count, request.Reads.Posters.Count);
        Assert.NotEqual(f.Current.CatalogVersion, request.Reads.CatalogVersion);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public void TrayMenuContainsOnlySixGlobalActions(int count)
    {
        using var f = new Fixture();
        if (count == 0) f.Remove(Main);
        for (int i = 1; i < count; i++) f.Add("same");
        foreach (bool hidden in new[] { false, true })
        foreach (bool pending in new[] { false, true })
        {
            var menu = TrayManagement.Menu(f.Current.Tray, hidden, pending);
            Assert.Equal(Enum.GetValues<TrayAction>(), menu.Select(e => e.Action));
            Assert.Equal(!pending, menu[0].Enabled);
            Assert.Equal(hidden, menu[2].Checked);
            Assert.Equal(count > 0, menu[3].Enabled);
            Assert.Contains(hidden ? "恢复" : "隐藏", menu[2].Text);
            Assert.DoesNotContain(menu, e => e.Text.Contains("编辑") || e.Text.Contains("收起"));
        }
    }

    [Fact]
    public void GlobalHideIsAnEffectiveViewNotAnIndividualPreference()
    {
        using var f = new Fixture(); string b = f.Add("B");
        f.Set(b, p => p with { IsVisible = false }, PosterStateCommandKind.SetVisible);
        var selection = new ControlCenterSelection(); selection.ApplyProjection(f.Current.ControlCenter);
        selection.Select(Main);
        var hidden = selection.BuildView([], false, true)!;
        Assert.True(hidden.GloballyHidden); Assert.Equal(Main, hidden.SelectedPosterId);
        Assert.Equal("全局临时隐藏", hidden.Posters.Single(p => p.PosterId == Main).RuntimeStatus);
        Assert.Equal("已隐藏", hidden.Posters.Single(p => p.PosterId == b).RuntimeStatus);
        Assert.True(hidden.SelectedPoster!.Poster.IsVisible);
        Assert.False(selection.BuildView([], false)!.GloballyHidden);
    }

    [Fact]
    public void GlobalLockAndMotionDoNotOverwriteHiddenIntentOrAutoPolicy()
    {
        using var f = new Fixture(); string b = f.Add("B");
        f.Set(b, p => p with { IsVisible = false }, PosterStateCommandKind.SetVisible);
        f.Registry.SetGloballyHidden(true);
        var before = f.Current;
        var reads = new StateReadSet(before.Posters.ToDictionary(p => p.Poster.PosterId, p => p.EntityVersion), CatalogVersion: before.CatalogVersion);
        var command = new BatchPosterCommand("tray-all", StateCommandSource.Tray, before.Revision, reads,
            before.Posters.Select(p => p.Poster.PosterId).ToArray(), BatchPosterCommandKind.SetLocked, true, AllPosters: true);
        var result = f.Coordinator.Execute(command);
        Assert.True(result.Succeeded, result.Error); Assert.Equal(before.Revision + 1, result.CurrentRevision);
        Assert.All(f.Current.Posters, p => Assert.True(p.Poster.IsLocked));
        Assert.False(f.Current.PostersById[b].Poster.IsVisible); Assert.Empty(f.Registry.Sessions);
        Assert.Equal("解锁全部海报", TrayManagement.Menu(f.Current.Tray, true, false)[3].Text);
        Assert.Equal(StateCommandStatus.Replayed, f.Coordinator.Execute(command).Status);
        var snapshot = f.Current;
        var settings = snapshot.ControlCenter.Settings;
        Assert.True(f.Coordinator.Execute(new SettingsStateCommand("tray-motion", StateCommandSource.Tray,
            SettingsStateCommandKind.SetDynamicEffectsPaused, snapshot.Revision, settings.Settings with { DynamicEffectsPaused = true })
            { ReadSet = StateReadSet.ForSettings(settings.EntityVersion) }).Succeeded);
        Assert.All(f.Current.Posters, p => Assert.Equal(snapshot.PostersById[p.Poster.PosterId], p with { Revision = snapshot.Revision }));
    }

    [Fact]
    public void GlobalLockStaleCatalogCannotPartiallyLock()
    {
        using var f = new Fixture(); var before = f.Current;
        var command = new BatchPosterCommand("tray-stale", StateCommandSource.Tray, before.Revision,
            TrayManagement.Create(before, Id()).Reads, [Main], BatchPosterCommandKind.SetLocked, true, AllPosters: true);
        f.Add("B"); long revision = f.Current.Revision;
        Assert.Equal(StateCommandStatus.Conflict, f.Coordinator.Execute(command).Status);
        Assert.Equal(revision, f.Current.Revision); Assert.All(f.Current.Posters, p => Assert.False(p.Poster.IsLocked));
    }

    [Fact]
    public void TrayGlobalHiddenIsIdempotentForEmptyCatalogAndDoesNotPersistAcrossRuntime()
    {
        using var f = new Fixture(); f.Remove(Main);
        long revision = f.Current.Revision;
        f.Registry.SetGloballyHidden(true); f.Registry.SetGloballyHidden(true);
        Assert.True(f.Recover()); Assert.Empty(f.Registry.Sessions);
        f.Registry.SetGloballyHidden(false); f.Registry.SetGloballyHidden(false);
        Assert.Equal(revision, f.Current.Revision);
        using var fresh = new PosterSessionRegistry(f.Factory, new FakeLog());
        Assert.False(fresh.GloballyHidden);
    }
}
