using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentIconPresentationTests
{
    private static readonly string[] Order = ["a", "b", "c"];
    private static bool Outline(ContentSelection selection, string id, bool editing = false) =>
        ContentCardPresentationPolicy.ShowOutline(selection.Ids, id, selection.ShowHighlight, editing);

    [Fact] public void PlainSingleAndRepeatedClicksKeepTargetWithoutOutline()
    {
        var selection = ContentSelection.Empty.Select(Order, "a").Select(Order, "a");
        Assert.Equal("a", selection.Focus); Assert.False(Outline(selection, "a"));
    }
    [Theory] [InlineData(true, false)] [InlineData(false, true)]
    public void OneModifierSelectedItemStillHasOutline(bool control, bool shift)
    {
        var selection = ContentSelection.Empty.Select(Order, "b", control, shift);
        Assert.Single(selection.Ids); Assert.True(Outline(selection, "b"));
    }
    [Fact] public void ControlBackToOneAndPruningPreserveExplicitOutline()
    {
        var selection = ContentSelection.Empty.Select(Order, "a", true).Select(Order, "c", true).Select(Order, "a", true);
        Assert.Single(selection.Ids); Assert.True(Outline(selection.Prune(Order), "c"));
        Assert.False(Outline(selection.Prune(["a"]), "c"));
    }
    [Fact] public void PlainClickAfterModifiersReturnsToQuietBrowsing()
    {
        var selection = ContentSelection.Empty.Select(Order, "a", true).Select(Order, "c", shift: true).Select(Order, "b");
        Assert.Single(selection.Ids); Assert.False(Outline(selection, "b"));
    }
    [Fact] public void ClearAndToggleLastOffLeaveNoOutline()
    {
        var selection = ContentSelection.Empty.Select(Order, "a", true);
        Assert.False(Outline(selection.Select(Order, null), "a"));
        Assert.False(Outline(selection.Select(Order, "a", true), "a"));
    }
    [Fact] public void EditingRetainsItsOwnFeedbackWithoutChangingSelectionIntent()
    {
        var selection = ContentSelection.Empty.Select(Order, "a");
        Assert.True(Outline(selection, "a", true)); Assert.False(Outline(selection, "b", true));
        Assert.False(Outline(selection, "a"));
    }
}
