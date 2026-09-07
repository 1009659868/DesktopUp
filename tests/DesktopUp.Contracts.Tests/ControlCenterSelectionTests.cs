using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class ControlCenterSelectionTests
{
    [Fact]
    public void ListsWholeCatalogIncludingHiddenAndDuplicateTitles()
    {
        using var f = new Fixture();
        string b = f.Add("same title");
        string c = f.Add("same title");
        f.Select(c);
        Assert.True(f.Execute(PosterStateCommandKind.SetVisible).Succeeded);
        ControlCenterView view = f.View;
        Assert.Equal(3, view.Posters.Count);
        Assert.Equal(c, view.SelectedPosterId);
        Assert.Equal("已隐藏", view.Posters.Single(p => p.PosterId == c).RuntimeStatus);
        Assert.NotEqual(b, c);
        Assert.Equal(2, view.Posters.Count(p => p.Title == "same title"));
    }

    [Fact]
    public void SelectionIsUiOnlyAndDoesNotChangeCatalogAliasOrRevision()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        long revision = f.Current.Revision;
        f.Select(b);
        Assert.Equal(b, f.Selection.SelectedPosterId);
        Assert.Equal(Main, f.Current.ControlCenter.SelectedPosterId);
        Assert.Equal(revision, f.Current.Revision);
        Assert.False(f.Selection.Select("missing"));
        Assert.Equal(b, f.Selection.SelectedPosterId);
    }

    [Theory]
    [InlineData(PosterStateCommandKind.SetLocked)]
    [InlineData(PosterStateCommandKind.SetCollapsed)]
    [InlineData(PosterStateCommandKind.SetVisible)]
    public void CommandsMutateOnlyDisplayedTargetWithEntityCas(PosterStateCommandKind kind)
    {
        using var f = new Fixture();
        string b = f.Add("B");
        string c = f.Add("C");
        var before = f.Current;
        f.Select(b);
        var command = f.View.CreatePosterCommand(kind, Id())!;
        Assert.Equal(b, command.PosterId);
        Assert.Equal(before.PostersById[b].EntityVersion, command.ReadSet!.Posters[b]);
        Assert.Single(command.ReadSet.Posters);
        Assert.Null(command.ReadSet.CatalogVersion);
        Assert.True(f.Coordinator.Execute(command).Succeeded);
        Assert.Equal(before.PostersById[Main].Poster, f.Current.PostersById[Main].Poster);
        Assert.Equal(before.PostersById[c].Poster, f.Current.PostersById[c].Poster);
        Assert.Equal(before.PostersById[b].EntityVersion + 1, f.Current.PostersById[b].EntityVersion);
    }

    [Fact]
    public void FrozenCommandDoesNotRetargetAfterSelectionChanges()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        f.Select(b);
        var frozen = f.View.CreatePosterCommand(PosterStateCommandKind.SetLocked, Id())!;
        f.Select(Main);
        Assert.True(f.Coordinator.Execute(frozen).Succeeded);
        Assert.True(f.Current.PostersById[b].Poster.IsLocked);
        Assert.False(f.Current.PostersById[Main].Poster.IsLocked);
        Assert.Equal(Main, f.Selection.SelectedPosterId);
    }

    [Fact]
    public void UnrelatedWriteDoesNotConflictButSameEntityWriteDoes()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        f.Select(b);
        var frozen = f.View.CreatePosterCommand(PosterStateCommandKind.SetLocked, Id())!;
        f.Select(Main);
        Assert.True(f.Execute(PosterStateCommandKind.SetLocked).Succeeded);
        Assert.Equal(StateCommandStatus.Committed, f.Coordinator.Execute(frozen).Status);
        Assert.Equal(StateCommandStatus.Conflict, f.Coordinator.Execute(frozen with { RequestId = Id() }).Status);
    }

    [Fact]
    public void SelectedRemovalClearsTargetAndNeverAutoRedirectsAction()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        f.Select(b);
        var frozen = f.View.CreatePosterCommand(PosterStateCommandKind.SetLocked, Id())!;
        f.Remove(b);
        Assert.Null(f.Selection.SelectedPosterId);
        Assert.NotNull(f.View.Notice);
        Assert.Single(f.View.Posters);
        Assert.Null(f.View.CreatePosterCommand(PosterStateCommandKind.SetLocked, Id()));
        Assert.Equal(StateCommandStatus.NotFound, f.Coordinator.Execute(frozen).Status);
        Assert.False(f.Current.PostersById[Main].Poster.IsLocked);
        Assert.True(f.Selection.Select(Main));
    }

    [Fact]
    public void EmptyDirectoryRemainsEmptyAndNewCatalogCanBeSelected()
    {
        using var f = new Fixture();
        f.Remove(Main);
        Assert.Empty(f.View.Posters);
        Assert.Null(f.View.SelectedPosterId);
        Assert.Null(f.View.CreatePosterCommand(PosterStateCommandKind.SetCollapsed, Id()));
        string b = f.Add("new");
        Assert.Equal(b, f.View.SelectedPosterId);
        Assert.Single(f.View.Posters);
    }

    [Fact]
    public void OlderProjectionCannotResurrectDeletedSelection()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        f.Select(b);
        var old = f.Current.ControlCenter;
        f.Remove(b);
        Assert.False(f.Selection.ApplyProjection(old));
        Assert.Null(f.View.SelectedPosterId);
        Assert.DoesNotContain(f.View.Posters, p => p.PosterId == b);
    }

    [Fact]
    public void RefreshRenameAndViewRecreationKeepIdentity()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        f.Select(b);
        var p = f.Current.PostersById[b];
        Assert.True(f.Coordinator.Execute(new PosterStateCommand(Id(), StateCommandSource.ControlCenter,
            PosterStateCommandKind.Rename, f.Current.Revision, b, p.Poster with { Title = "renamed" })
            { ReadSet = StateReadSet.ForPoster(b, p.EntityVersion) }).Succeeded);
        f.Selection.ApplyProjection(f.Current.ControlCenter);
        var reopenedView = f.Selection.BuildView(Array.Empty<PosterSessionSnapshot>(), false)!;
        Assert.Equal(b, reopenedView.SelectedPosterId);
        Assert.Equal("renamed", reopenedView.SelectedPoster!.Poster.Title);
    }

    [Fact]
    public void RuntimeReadbackIsSeparateFromCommittedState()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        f.Select(b);
        long revision = f.Current.Revision;
        var sessions = new[]
        {
            new PosterSessionSnapshot(Main, 1, PosterSessionStatus.Ready, "0x1", null, null),
            new PosterSessionSnapshot(b, 2, PosterSessionStatus.Degraded, "0x2", null, "readback-failed")
        };
        var view = f.Selection.BuildView(sessions, false)!;
        Assert.Equal("运行正常", view.Posters.Single(p => p.PosterId == Main).RuntimeStatus);
        Assert.Equal("运行降级", view.Posters.Single(p => p.PosterId == b).RuntimeStatus);
        Assert.Equal("恢复中", f.Selection.BuildView(sessions, true)!.Posters.Single(p => p.PosterId == Main).RuntimeStatus);
        Assert.Equal(revision, f.Current.Revision);
        Assert.Equal(b, view.SelectedPosterId);
    }

    [Fact]
    public void HiddenTargetRemainsSelectableAndCanBeShownAgain()
    {
        using var f = new Fixture();
        string b = f.Add("B");
        f.Select(b);
        Assert.True(f.Execute(PosterStateCommandKind.SetVisible).Succeeded);
        Assert.Equal(b, f.View.SelectedPosterId);
        Assert.False(f.View.SelectedPoster!.Poster.IsVisible);
        Assert.True(f.Execute(PosterStateCommandKind.SetVisible).Succeeded);
        Assert.True(f.View.SelectedPoster!.Poster.IsVisible);
    }

    private const string Main = BuiltInStateIdentities.PrimaryPosterId;
    private static string Id() => Guid.NewGuid().ToString("N");
    private static PosterDefinition Configuration() => new(Main, "A", "M04 fixture", "DISPLAY1",
        0, 0, 300, 480, 0.1, 0.1, 0.2, 0.5, false, false, false, "grid", "{}", "night-ops");

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.V3.Center.Tests", Id());
        private readonly SqliteStateStore _store;
        public StateProjectionCoordinator Coordinator { get; }
        public ControlCenterSelection Selection { get; } = new();
        public DesktopUpProjection Current { get { Assert.True(Coordinator.TryGetCurrent(out var p)); return p!; } }
        public ControlCenterView View => Selection.BuildView(Array.Empty<PosterSessionSnapshot>(), false)!;
        public Fixture()
        {
            _store = new(new UserDataPaths(_root));
            Assert.True(_store.Initialize().Succeeded);
            Coordinator = new(new SqliteStateCommandRepository(_store));
            Coordinator.ProjectionChanged += (_, e) => Selection.ApplyProjection(e.Projection.ControlCenter);
            Selection.ApplyProjection(Coordinator.EnsureInitialized(Configuration()).ControlCenter);
        }
        public void Select(string id) => Assert.True(Selection.Select(id));
        public StateCommandResult Execute(PosterStateCommandKind kind) => Coordinator.Execute(View.CreatePosterCommand(kind, Id())!);
        public string Add(string title)
        {
            var result = Coordinator.Execute(new CreatePosterCommand(Id(), StateCommandSource.ControlCenter,
                Current.Revision, StateReadSet.ForCatalog(Current.CatalogVersion), Configuration() with { Title = title }));
            Assert.True(result.Succeeded, result.Error);
            return result.ResultPosterId!;
        }
        public void Remove(string id)
        {
            var preview = Coordinator.PrepareRemoval(id)!;
            Assert.True(Coordinator.Execute(new RemovePosterCommand(Id(), StateCommandSource.ControlCenter,
                preview.Revision, preview.ReadSet, id, true)).Succeeded);
        }
        public void Dispose()
        {
            _store.Dispose();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
    }
}
