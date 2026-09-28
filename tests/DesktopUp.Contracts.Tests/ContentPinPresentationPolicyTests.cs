using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentPinPresentationPolicyTests
{
    [Theory]
    [InlineData(3, ContentLayoutMode.Automatic, false, 2, ContentPinAdornment.OrderPin)]
    [InlineData(2, ContentLayoutMode.Anchored, false, 2, ContentPinAdornment.OrderPin)]
    [InlineData(2, ContentLayoutMode.Automatic, false, 2, ContentPinAdornment.None)]
    [InlineData(3, ContentLayoutMode.Manual, false, 2, ContentPinAdornment.None)]
    [InlineData(3, ContentLayoutMode.Automatic, false, null, ContentPinAdornment.None)]
    [InlineData(3, ContentLayoutMode.Automatic, true, 2, ContentPinAdornment.LegacyLock)]
    [InlineData(2, ContentLayoutMode.Manual, true, null, ContentPinAdornment.LegacyLock)]
    public void Legacy_hard_position_is_a_distinct_lock_not_a_releasable_order_pin(int schema,
        ContentLayoutMode mode, bool fixedPosition, int? pin, ContentPinAdornment expected)
    {
        var document = ContentLayoutSamples.Equal("A") with { SchemaVersion = schema, Mode = mode };
        var placement = document.Placements[0] with { FixedPosition = fixedPosition, PinnedIndex = pin };
        Assert.Equal(expected, ContentPinPresentationPolicy.Adornment(document, placement));
    }

    [Fact]
    public void Icon_remains_at_the_authored_tile_corner_and_clips_without_repositioning()
    {
        var tile = new ContentLayoutTile("A:item-0000", 0, 0, 0, 0, 0, new(0, 0, 100, 100), new(0, 0, 85, 100));
        Assert.Equal(new ContentRect(76, 2, 22, 22), ContentPinPresentationPolicy.Bounds(tile.Bounds));
        Assert.Equal(new ContentRect(76, 2, 9, 22), ContentPinPresentationPolicy.VisibleBounds(tile));
        Assert.True(ContentPinPresentationPolicy.VisibleBounds(tile with { Clip = new(0, 30, 85, 70) }).IsEmpty);
        Assert.Equal(tile.Clip.Intersect(ContentPinPresentationPolicy.VisibleBounds(tile)), ContentPinPresentationPolicy.VisibleBounds(tile));
    }

    [Theory]
    [InlineData(80, 10, 81, 11, ContentPinContextAction.ReleasePin)]
    [InlineData(80, 10, 40, 30, ContentPinContextAction.Cancel)]
    [InlineData(40, 30, 80, 10, ContentPinContextAction.Cancel)]
    [InlineData(40, 30, 41, 31, ContentPinContextAction.ItemMenu)]
    [InlineData(80, 10, 98, 10, ContentPinContextAction.Cancel)]
    public void Shortcut_requires_both_right_button_points_inside_the_same_visible_icon(double pressX,
        double pressY, double releaseX, double releaseY, ContentPinContextAction expected)
    {
        var pin = ContentPinPresentationPolicy.Bounds(new(0, 0, 100, 100));
        Assert.Equal(expected, ContentPinPresentationPolicy.ResolveContext([pin], (pressX, pressY), (releaseX, releaseY)));
    }

    [Fact]
    public void Missing_press_witness_retains_item_menu_and_never_releases_a_pin()
    {
        var pin = ContentPinPresentationPolicy.Bounds(new(0, 0, 100, 100));
        Assert.Equal(ContentPinContextAction.ItemMenu, ContentPinPresentationPolicy.ResolveContext([pin], null, (80, 10)));
        Assert.Equal(ContentPinContextAction.ItemMenu, ContentPinPresentationPolicy.ResolveContext([], (80, 10), (81, 11)));
    }

    [Fact]
    public void Two_copies_of_one_loop_item_are_not_one_pin_click()
    {
        var left = new ContentLayoutTile("A:item-0000", 0, 0, -1, -100, 0, new(-90, 0, 100, 100), new(0, 0, 10, 100));
        var right = left with { LoopIndex = 0, Bounds = new(10, 0, 100, 100), Clip = new(10, 0, 90, 100) };
        var pins = new[] { ContentPinPresentationPolicy.VisibleBounds(left), ContentPinPresentationPolicy.VisibleBounds(right) };
        Assert.Equal(ContentPinContextAction.Cancel, ContentPinPresentationPolicy.ResolveContext(pins, (2, 10), (88, 10)));
        Assert.Equal(ContentPinContextAction.ReleasePin, ContentPinPresentationPolicy.ResolveContext(pins, (2, 10), (3, 10)));
        Assert.Equal(ContentPinContextAction.ItemMenu, ContentPinPresentationPolicy.ResolveContext(pins, (8, 10), (9, 10)));
    }

    [Fact]
    public void Formal_unpin_changes_only_selected_author_intent_and_retains_one_step_undo()
    {
        var original = ContentLayoutSamples.Equal("A");
        var before = original with { Placements = original.Placements.Select((p, i) => i == 0 ? p with { PinnedIndex = 2 } : p).ToArray() };
        var result = ContentLayoutEditing.Apply(before, new(ContentLayoutEditKind.Pinned, before.Placements[0].ItemId) { Enabled = false }, new(354, 600));
        Assert.True(result.Accepted, result.Error);
        Assert.Equal(before.DocumentVersion + 1, result.Document.DocumentVersion);
        Assert.Equal(before.Placements[0] with { PinnedIndex = null, Version = before.Placements[0].Version + 1 }, result.Document.Placements[0]);
        Assert.Equal(before.Placements.Skip(1), result.Document.Placements.Skip(1));
        Assert.Equal(before.ItemSetVersion, result.Document.ItemSetVersion);
        Assert.Equal(before.Mode, result.Document.Mode);
        var history = new ContentLayoutHistory(); history.Record(before, result.Document);
        var undo = history.ProposeUndo(result.Document);
        Assert.NotNull(undo);
        Assert.True(ContentLayoutEditing.SameState(before, undo.Document, true));
        Assert.True(history.Confirm(undo, undo.Document));
        Assert.Equal(0, history.UndoCount);
    }
}
