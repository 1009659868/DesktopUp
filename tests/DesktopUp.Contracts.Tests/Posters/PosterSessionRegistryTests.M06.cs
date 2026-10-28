using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Testing;

namespace DesktopUp.Contracts.Tests;

public sealed partial class PosterSessionRegistryTests
{
    [Fact]
    public void M06SharedThreeUseCasesHaveIndependentIdentityAndPresentationAfterReopen()
    {
        using var f = new Fixture(multiInstance: true);
        Assert.True(f.Recover());
        Assert.Equal(3, f.Current.Posters.Select(p => p.Poster.ThemeInstanceId).Distinct().Count());
        Assert.Equal(3, f.Current.Posters.Select(p => p.Poster.LayoutParametersJson).Distinct().Count());
        for (int index = 0; index < 3; index++)
        {
            var actual = f.Current.Posters[index]; var expected = MultiPosterFixtureData.Create(index);
            Assert.Equal(expected.Title, actual.Poster.Title);
            Assert.Equal(expected.LayoutParametersJson, actual.Poster.LayoutParametersJson);
            Assert.Equal(expected.ExpandedHeightDip, actual.Poster.ExpandedHeightDip);
            Assert.Equal(actual.Presentation, f.Factory.For(actual.Poster.PosterId).Projection!.Presentation);
            Assert.False(actual.Presentation.IsFallback);
        }
        Assert.Equal(f.Current.Posters.Select(p => p.Poster), f.Reopen().Posters.Select(p => p.Poster));
        Assert.Equal(f.Current.Revision, f.Current.ControlCenter.Revision);
        Assert.Equal(f.Current.Revision, f.Current.Tray.Revision);
        Assert.Equal(3, f.Current.Tray.PosterCount);
    }

    [Fact]
    public void M06HiddenParameterEditCreatesNoWindowUntilShowAndDoesNotResetNeighbors()
    {
        using var f = new Fixture(multiInstance: true); Assert.True(f.Recover());
        string b = f.Current.Posters[1].Poster.PosterId, c = f.Current.Posters[2].Poster.PosterId;
        var aWindow = f.Factory.For(Main); var cWindow = f.Factory.For(c); var oldB = f.Factory.For(b);
        long epoch = f.Registry.Sessions.Single(p => p.PosterId == b).Epoch;
        Assert.True(f.Set(b, p => p with { IsVisible = false }, PosterStateCommandKind.SetVisible).Succeeded);
        var draft = PosterPresentationDraft.Open(f.Current.PostersById[b]);
        var edited = draft with { Title = "B hidden changed", Parameters = draft.Parameters with { Content = draft.Parameters.Content with { IconSizeDip = 16 } } };
        Assert.True(f.Coordinator.Execute(edited.ToCommand(Id())).Succeeded);
        Assert.Null(f.Registry.Find(b)); Assert.True(oldB.Token.IsCancellationRequested); Assert.True(oldB.Disposed);
        Assert.Equal(3, f.Factory.Created.Count);
        Assert.True(f.Set(b, p => p with { IsVisible = true }, PosterStateCommandKind.SetVisible).Succeeded);
        Assert.True(f.Registry.Sessions.Single(p => p.PosterId == b).Epoch > epoch);
        Assert.Equal(16, f.Factory.For(b).Projection!.Presentation.Parameters.Content.IconSizeDip);
        Assert.Equal("B hidden changed", f.Factory.For(b).Projection!.Poster.Title);
        Assert.Same(aWindow, f.Registry.Find(Main)); Assert.Same(cWindow, f.Registry.Find(c));
    }

    [Theory]
    [InlineData("start")]
    [InlineData("apply")]
    [InlineData("recover")]
    public void M06PartialFailureWithDifferentParametersRetainsHealthyInstancesAndLatestCommittedState(string stage)
    {
        using var f = new Fixture(multiInstance: true);
        string b = f.Current.Posters[1].Poster.PosterId, c = f.Current.Posters[2].Poster.PosterId;
        if (stage == "start") { f.Factory.FailStart.Add(b); Assert.False(f.Recover()); }
        else
        {
            Assert.True(f.Recover());
            if (stage == "apply")
            {
                f.Factory.FailApply.Add(b);
                var draft = PosterPresentationDraft.Open(f.Current.PostersById[b]) with { Title = "B committed despite local render failure" };
                Assert.True(f.Coordinator.Execute(draft.ToCommand(Id())).Succeeded);
            }
            else { f.Factory.For(b).FailRecover = true; Assert.False(f.Recover(2)); }
        }
        var a = f.Factory.For(Main); var third = f.Factory.For(c); var failed = f.Factory.For(b);
        Assert.Equal(2, f.Registry.Sessions.Count(p => p.Status == PosterSessionStatus.Ready));
        Assert.Equal(3, f.Current.Posters.Count);
        Assert.True(failed.Disposed); Assert.True(failed.Token.IsCancellationRequested);
        long revision = f.Current.Revision;
        f.Factory.FailStart.Clear(); f.Factory.FailApply.Clear();
        Assert.True(f.Recover(stage == "recover" ? 2 : 1));
        Assert.Same(a, f.Registry.Find(Main)); Assert.Same(third, f.Registry.Find(c));
        Assert.Equal(f.Current.PostersById[b].Poster, f.Factory.For(b).Projection!.Poster);
        Assert.Equal(revision, f.Current.Revision);
        Assert.Equal(4, f.Factory.Created.Count);
    }

    [Fact]
    public void M06UnsupportedOnePosterRetainsRawConfigAndOnlyThatPosterUsesFallback()
    {
        using var f = new Fixture(multiInstance: true, unknownSecond: true); Assert.True(f.Recover());
        var b = f.Current.Posters[1];
        Assert.True(f.Factory.For(b.Poster.PosterId).Projection!.Presentation.IsFallback);
        Assert.False(f.Factory.For(Main).Projection!.Presentation.IsFallback);
        Assert.False(f.Factory.For(f.Current.Posters[2].Poster.PosterId).Projection!.Presentation.IsFallback);
        Assert.Equal(b.Poster.LayoutParametersJson, f.Reopen().PostersById[b.Poster.PosterId].Poster.LayoutParametersJson);
        Assert.Throws<ArgumentException>(() => PosterPresentationDraft.Open(b));
        Assert.Equal(3, f.Registry.Sessions.Count);
    }

    [Fact]
    public void M06RemovalCancelAndStaleTicketPreserveCatalogAndConfirmedTargetDoesNotResurrect()
    {
        using var f = new Fixture(multiInstance: true); Assert.True(f.Recover());
        var c = f.Current.Posters[2]; string id = c.Poster.PosterId;
        var confirmation = new PosterRemovalConfirmation();
        var request = new PosterRemovalRequest(id, c.EntityVersion, StateCommandSource.ControlCenter);
        var ticket = confirmation.Begin(request, f.Coordinator.PrepareRemoval(id)!)!;
        long revision = f.Current.Revision;
        Assert.Null(confirmation.Complete(ticket.Id, false)); Assert.Equal(revision, f.Current.Revision);
        ticket = confirmation.Begin(request, f.Coordinator.PrepareRemoval(id)!)!;
        Assert.True(f.Set(id, p => p with { IsLocked = true }, PosterStateCommandKind.SetLocked).Succeeded);
        Assert.False(confirmation.IsCurrent(f.Current.ControlCenter));
        Assert.Equal(StateCommandStatus.Conflict, f.Coordinator.Execute(confirmation.Complete(ticket.Id, true)!).Status);
        var oldProjection = f.Current; var removedWindow = f.Factory.For(id);
        f.Remove(id);
        f.Registry.ApplyProjection(Change(oldProjection)); Assert.True(f.Recover(2));
        Assert.Null(f.Registry.Find(id)); Assert.True(removedWindow.Token.IsCancellationRequested);
        Assert.Equal(2, f.Reopen().Posters.Count);
        Assert.Equal(StateCommandStatus.NotFound, f.Coordinator.Execute((PosterPresentationDraft.Open(c) with { Title = "late" }).ToCommand(Id())).Status);
    }

    [Fact]
    public void M06ReopenPreservesHiddenPeerDeletedPeerAndEditedParametersWithoutExtraDefault()
    {
        using var f = new Fixture(multiInstance: true); Assert.True(f.Recover());
        string b = f.Current.Posters[1].Poster.PosterId, c = f.Current.Posters[2].Poster.PosterId;
        var draft = PosterPresentationDraft.Open(f.Current.PostersById[Main]);
        Assert.True(f.Coordinator.Execute((draft with { Parameters = draft.Parameters with { Content = draft.Parameters.Content with { GapDip = 20 } } }).ToCommand(Id())).Succeeded);
        f.Set(b, p => p with { IsVisible = false }, PosterStateCommandKind.SetVisible); f.Remove(c);
        var restored = f.Reopen();
        var factory = new Factory(); using var registry = new PosterSessionRegistry(factory, new FakeLog());
        registry.ApplyProjection(Change(restored)); Assert.True(registry.Recover(Environment(1), 1, () => true));
        Assert.Equal(f.Current.Posters.Select(p => p.Poster), restored.Posters.Select(p => p.Poster));
        Assert.Equal(2, restored.Tray.PosterCount); Assert.Equal(1, restored.Tray.VisiblePosterCount);
        Assert.Equal(Main, Assert.Single(registry.Sessions).PosterId);
        Assert.Equal(20, factory.For(Main).Projection!.Presentation.Parameters.Content.GapDip);
    }

    [Fact]
    public void M06ZeroAfterRemovingThreeDifferentPostersStaysZeroOnReopen()
    {
        using var f = new Fixture(multiInstance: true); Assert.True(f.Recover());
        foreach (var target in f.Current.Posters.ToArray()) f.Remove(target.Poster.PosterId);
        Assert.Empty(f.Reopen().Posters); Assert.Empty(f.Registry.Sessions);
        Assert.Equal(0, f.Current.Tray.PosterCount);
        Assert.All(f.Factory.Created, session => Assert.True(session.Disposed && session.Token.IsCancellationRequested));
    }

    [Fact]
    public void M06PendingEnvironmentUsesNewestVisibilityAndHiddenParametersNotCapturedSnapshot()
    {
        using var f = new Fixture(multiInstance: true); Assert.True(f.Recover());
        string b = f.Current.Posters[1].Poster.PosterId, c = f.Current.Posters[2].Poster.PosterId;
        var captured = f.Current; f.Registry.SuspendEnvironment(4);
        f.Set(b, p => p with { IsVisible = false }, PosterStateCommandKind.SetVisible); f.Remove(c);
        var draft = PosterPresentationDraft.Open(f.Current.PostersById[b]) with { Title = "hidden while waiting" };
        Assert.True(f.Coordinator.Execute(draft.ToCommand(Id())).Succeeded);
        f.Registry.ApplyProjection(Change(captured));
        Assert.False(f.Recover(3)); Assert.True(f.Recover(4));
        Assert.Equal(Main, Assert.Single(f.Registry.Sessions).PosterId);
        Assert.Equal("hidden while waiting", f.Reopen().PostersById[b].Poster.Title);
    }
}
