using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed partial class PosterSessionRegistryTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void NewRecoveryRequestRebuildsExistingSessionsEvenWhenGenerationsAndProjectionAreUnchanged(int count)
    {
        using var f = new Fixture();
        for (int i = 1; i < count; i++) f.Add("Neighbor " + i);
        Assert.True(f.Recover());
        var initial = f.Registry.Sessions.ToArray();
        var sessions = f.Factory.Created.ToArray();
        long revision = f.Current.Revision;
        Assert.All(sessions, session => Assert.Equal(0, session.Recoveries));

        f.Registry.SuspendEnvironment(2);
        // Production suspension clears input even when Explorer/DPI generations do not change.
        f.Registry.ApplyProjection(Change(f.Current));
        Assert.True(f.Registry.Recover(Environment(1), 2, () => true));

        Assert.Equal(revision, f.Current.Revision);
        Assert.Equal(initial.Select(s => s.Epoch), f.Registry.Sessions.Select(s => s.Epoch));
        Assert.Equal(count, f.Factory.Created.Count);
        Assert.All(sessions, session =>
        {
            Assert.Equal(1, session.Recoveries);
            Assert.Same(session, f.Registry.Find(session.PosterId));
            Assert.Equal(1, session.CurrentGenerationPair!.Value.DesktopShellGeneration);
            Assert.Equal(1, session.CurrentGenerationPair!.Value.DisplayTopologyGeneration);
        });

        f.Registry.ApplyProjection(Change(f.Current));
        Assert.True(f.Registry.Recover(Environment(1), 2, () => true));
        Assert.All(sessions, session => Assert.Equal(1, session.Recoveries));
    }

    [Fact]
    public void NewSessionsStartOnceWithoutRedundantRecoveryAlongsideExistingSuspendedPeers()
    {
        using var f = new Fixture();
        Assert.True(f.Recover());
        var existing = f.Factory.For(Main);
        f.Registry.SuspendEnvironment(2);
        string added = f.Add("Imported neighbor");
        Assert.Single(f.Factory.Created);

        Assert.True(f.Registry.Recover(Environment(1), 2, () => true));
        Assert.Equal(1, existing.Recoveries);
        Assert.Equal(0, f.Factory.For(added).Recoveries);
        Assert.Equal(2, f.Factory.Created.Count);

        Assert.True(f.Registry.Recover(Environment(1), 2, () => true));
        Assert.Equal(1, existing.Recoveries);
        Assert.Equal(0, f.Factory.For(added).Recoveries);
    }

    [Fact]
    public void ExplicitSuspensionInvalidatesCompletedRequestEvenWhenRequestNumberIsReused()
    {
        using var f = new Fixture();
        Assert.True(f.Recover());
        var existing = f.Factory.For(Main);
        f.Registry.SuspendEnvironment(1);
        Assert.True(f.Registry.Recover(Environment(1), 1, () => true));
        Assert.Equal(1, existing.Recoveries);

        Assert.True(f.Registry.Recover(Environment(1), 1, () => true));
        Assert.Equal(1, existing.Recoveries);
        f.Registry.SuspendEnvironment(1);
        Assert.True(f.Registry.Recover(Environment(1), 1, () => true));
        Assert.Equal(2, existing.Recoveries);
        Assert.Single(f.Factory.Created);
    }

    [Fact]
    public void SupersededOrInvalidRequestCannotClaimExistingSessionsRecovered()
    {
        using var f = new Fixture();
        Assert.True(f.Recover());
        var existing = f.Factory.For(Main);
        f.Registry.SuspendEnvironment(3);
        Assert.False(f.Registry.Recover(Environment(1), 2, () => true));
        Assert.False(f.Registry.Recover(Environment(1), 3, () => false));
        Assert.Equal(0, existing.Recoveries);

        Assert.True(f.Registry.Recover(Environment(1), 3, () => true));
        Assert.Equal(1, existing.Recoveries);
        Assert.Same(existing, f.Registry.Find(Main));
    }

    [Fact]
    public void RequestSupersededInsideSessionRecoveryCannotPublishOldCompletion()
    {
        using var f = new Fixture();
        Assert.True(f.Recover());
        var existing = f.Factory.For(Main);
        f.Registry.SuspendEnvironment(2);
        f.Factory.OnRecover = _ => f.Registry.SuspendEnvironment(3);

        Assert.False(f.Registry.Recover(Environment(1), 2, () => true));
        Assert.Equal(1, existing.Recoveries);
        Assert.False(f.Registry.Recover(Environment(1), 2, () => true));
        Assert.Equal(1, existing.Recoveries);

        f.Factory.OnRecover = null;
        Assert.True(f.Registry.Recover(Environment(1), 3, () => true));
        Assert.Equal(2, existing.Recoveries);
        Assert.Same(existing, f.Registry.Find(Main));
        Assert.Single(f.Factory.Created);
    }
}
