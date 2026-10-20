using DesktopUp.Application;
using DesktopUp.Domain;
using static DesktopUp.Contracts.Tests.ContentLayoutSolverTests;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterContentSessionTests
{
    private static ContentHeatProposal Heat(ContentLayoutDocument doc, long version) =>
        new(doc.PosterId, doc.ItemSetVersion, version, doc.Placements.Select((p, i) =>
            new ContentHeatItem(p.ItemId, doc.Placements.Count - i - 1, .5)).ToArray());

    [Fact]
    public void AllProtectionOwnersKeepLatestProposalAndOnlyExplicitReleaseWakesCenter()
    {
        var doc = Doc(3) with { AllowHeatDrivenSize = true };
        var initial = PosterPresentationPolicy.Defaults();
        using var session = new PosterContentSession(new(doc.PosterId, 1));
        session.SetSuspended(false); session.Reconcile(doc, View());
        long version = 0;
        foreach (var owner in Enum.GetValues<PosterContentInteraction>().Where(x => x != PosterContentInteraction.None))
        {
            session.SetInteraction(owner, true);
            Assert.True(session.OfferHeat(Heat(doc, ++version), doc, true));
            Assert.True(session.OfferHeat(Heat(doc, ++version), doc, true));
            var latest = initial with { Theme = initial.Theme with { Accent = $"#{version:X6}" } };
            Assert.True(session.OfferTheme(new(version, latest), doc, View(), initial, false));
            Assert.True(session.TryQueue(initial, out var work));
            Assert.False(session.TryQueue(initial, out _));
            Assert.False(session.BeginDrain(work));
            Assert.False(session.Drain(doc, View(), initial).Heat.Applied);
            Assert.Equal(owner == PosterContentInteraction.CenterDraft, session.AwaitingCenterRelease);
            if (owner == PosterContentInteraction.CenterDraft) Assert.True(session.SetCenterDraft(false));
            else session.SetInteraction(owner, false);
            Assert.True(session.TryQueue(initial, out work));
            Assert.True(session.BeginDrain(work));
            var applied = session.Drain(doc, View(), initial);
            Assert.True(applied.Heat.Applied);
            Assert.Equal(version, session.ActiveHeatVersion);
            Assert.Equal(latest, applied.Theme);
            initial = latest;
        }
    }

    [Fact]
    public void SuspensionAndDisposalRejectLateCallbacksWithoutClearingNewWork()
    {
        var doc = Doc(2);
        var theme = PosterPresentationPolicy.Defaults();
        using var session = new PosterContentSession(new(doc.PosterId, 4));
        session.SetSuspended(false); session.Reconcile(doc, View());
        Assert.True(session.OfferHeat(Heat(doc, 1), doc, true));
        Assert.True(session.TryQueue(theme, out var old));
        session.SetDraft(doc, doc);
        session.SetSuspended(true);
        Assert.Null(session.Draft);
        Assert.True(session.HasPendingHeat);
        Assert.False(session.TryQueue(theme, out _));
        session.SetSuspended(false);
        Assert.True(session.TryQueue(theme, out var current));
        Assert.False(session.BeginDrain(old));
        session.CancelQueued(old);
        Assert.True(session.BeginDrain(current));
        Assert.True(session.Drain(doc, View(), theme).Heat.Applied);
        session.OfferHeat(Heat(doc, 2), doc, true);
        session.TryQueue(theme, out var closing);
        session.Dispose();
        session.SetSuspended(false); session.SetDraft(doc, doc);
        Assert.False(session.BeginDrain(closing));
        Assert.False(session.OfferHeat(Heat(doc, 3), doc, true));
        Assert.False(session.TryQueue(theme, out _));
        Assert.Null(session.Draft);
        Assert.False(session.HasPendingHeat);
        using var next = new PosterContentSession(new(doc.PosterId, 5));
        next.SetSuspended(false); next.Reconcile(doc, View());
        next.OfferHeat(Heat(doc, 1), doc, true); next.TryQueue(theme, out var nextWork);
        Assert.False(next.BeginDrain(closing));
        Assert.True(next.BeginDrain(nextWork));
    }

    [Fact]
    public void DraftHasPriorityWithoutReplacingAcceptedHeatOrPersistedAuthor()
    {
        var doc = Doc(2);
        var theme = PosterPresentationPolicy.Defaults();
        using var session = new PosterContentSession(new(doc.PosterId, 1));
        session.SetSuspended(false); session.Reconcile(doc, View());
        session.OfferHeat(Heat(doc, 1), doc, true); session.Drain(doc, View(), theme);
        var accepted = session.Effective(doc);
        var draft = doc with { DocumentVersion = doc.DocumentVersion + 1 };
        var projected = session.ProjectEdit(draft);
        session.SetDraft(draft, projected);
        Assert.Same(projected, session.Effective(doc));
        session.ClearDraft();
        Assert.Same(accepted, session.Effective(doc));
        Assert.Equal(1L, session.ActiveHeatVersion);
        session.Reconcile(doc with { ItemSetVersion = doc.ItemSetVersion + 1 }, View());
        Assert.Null(session.ActiveHeatVersion);
        Assert.False(session.OfferHeat(Heat(doc, 2), doc with { ItemSetVersion = doc.ItemSetVersion + 1 }, true));
    }

    [Fact]
    public void SourceNotificationsCoalescePerBindingAndDeferOnlyAtExistingSafePoints()
    {
        using var session = new PosterContentSession(new("a", 1));
        var old = session.BindSource();
        Assert.True(old.TryQueue()); Assert.False(old.TryQueue());
        var next = session.BindSource();
        Assert.True(next.TryQueue());
        Assert.False(old.Complete()); Assert.False(old.TryQueue());
        Assert.True(next.Complete()); // Old callback did not consume this binding's work.
        session.SetInteraction(PosterContentInteraction.Naming, true);
        Assert.False(session.TryAcceptSource(2)); Assert.True(session.SourceRefreshPending);
        Assert.Equal(0L, session.SourceVersion);
        session.SetInteraction(PosterContentInteraction.Naming, false);
        Assert.True(session.TryAcceptSource(2)); Assert.False(session.SourceRefreshPending);
        Assert.False(session.TryAcceptSource(1)); Assert.False(session.TryAcceptSource(2));
        session.Dispose();
        Assert.False(next.TryQueue()); Assert.False(session.TryAcceptSource(3));
    }

    [Fact]
    public void IndependentSessionsDoNotShareDraftProtectionOrQueuedWork()
    {
        var a = Doc(2); var b = ContentLayoutSamples.Equal("b", 2);
        using var first = new PosterContentSession(new(a.PosterId, 1));
        using var second = new PosterContentSession(new(b.PosterId, 2));
        first.SetSuspended(false); second.SetSuspended(false);
        first.Reconcile(a, View()); second.Reconcile(b, View());
        first.SetCenterDraft(true);
        first.OfferHeat(Heat(a, 1), a, true); second.OfferHeat(Heat(b, 1), b, true);
        var theme = PosterPresentationPolicy.Defaults();
        first.TryQueue(theme, out var blocked); second.TryQueue(theme, out var ready);
        Assert.False(first.BeginDrain(blocked));
        Assert.False(second.BeginDrain(blocked));
        Assert.True(second.BeginDrain(ready));
        Assert.True(second.Drain(b, View(), theme).Heat.Applied);
        Assert.True(first.HasPendingHeat);
        Assert.False(second.HasPendingHeat);
    }
}
