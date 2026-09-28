using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentActivationSessionTests
{
    private static readonly ContentActivationTarget Target = new("poster", "item", 3, 7, "geometry-A");
    private static readonly ContentDoubleClickSettings Settings = new(500, 4, 6);
    private static readonly PointerScreenPoint Point = new(100, 200);

    [Fact]
    public void Single_click_only_pairs_and_second_click_opens_once_while_third_starts_a_new_pair()
    {
        var session = new ContentActivationSession();
        Assert.Null(session.Click(Target, Guid.NewGuid(), 100, Point, Settings));
        var opened = session.Click(Target, Guid.NewGuid(), 200, Point, Settings);
        Assert.NotNull(opened); Assert.Equal(Target, opened.Target);
        Assert.Equal(ContentOpenOrigin.DoubleClick, opened.Origin); Assert.NotEqual(Guid.Empty, opened.ActivationId);
        Assert.Null(session.Click(Target, Guid.NewGuid(), 300, Point, Settings));
        var next = session.Click(Target, Guid.NewGuid(), 400, Point, Settings);
        Assert.NotNull(next); Assert.NotEqual(opened.ActivationId, next.ActivationId);
    }

    [Fact]
    public void Duplicate_capture_completion_neither_forms_nor_replays_an_activation()
    {
        var session = new ContentActivationSession();
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        Assert.Null(session.Click(Target, first, 100, Point, Settings));
        Assert.Null(session.Click(Target, first, 100, Point, Settings));
        Assert.NotNull(session.Click(Target, second, 200, Point, Settings));
        Assert.Null(session.Click(Target, first, 100, Point, Settings));
        Assert.Null(session.Click(Target, second, 200, Point, Settings));
        Assert.Null(session.Click(Target, Guid.NewGuid(), 300, Point, Settings));
    }

    [Theory]
    [InlineData("poster")]
    [InlineData("item")]
    [InlineData("membership")]
    [InlineData("document")]
    [InlineData("presentation")]
    public void Both_clicks_require_the_same_complete_effective_identity(string difference)
    {
        var session = new ContentActivationSession();
        var changed = difference switch
        {
            "poster" => Target with { PosterId = "other" },
            "item" => Target with { ItemId = "other" },
            "membership" => Target with { ItemSetVersion = 4 },
            "document" => Target with { DocumentVersion = 8 },
            _ => Target with { PresentationWitness = "geometry-B" }
        };
        Assert.Null(session.Click(Target, Guid.NewGuid(), 100, Point, Settings));
        Assert.Null(session.Click(changed, Guid.NewGuid(), 200, Point, Settings));
        var opened = session.Click(changed, Guid.NewGuid(), 300, Point, Settings);
        Assert.NotNull(opened); Assert.Equal(changed, opened.Target);
    }

    [Theory]
    [InlineData(500, 4, 6, true)]
    [InlineData(500, -4, -6, true)]
    [InlineData(501, 0, 0, false)]
    [InlineData(200, 5, 0, false)]
    [InlineData(200, 0, -7, false)]
    public void System_supplied_time_and_rectangular_position_boundaries_are_inclusive(long elapsed, int x, int y, bool opens)
    {
        var session = new ContentActivationSession();
        session.Click(Target, Guid.NewGuid(), 100, Point, Settings);
        var opened = session.Click(Target, Guid.NewGuid(), 100 + elapsed, new(Point.X + x, Point.Y + y), Settings);
        Assert.Equal(opens, opened is not null);
    }

    [Fact]
    public void Stale_timestamp_cannot_replace_the_latest_pending_identity()
    {
        var session = new ContentActivationSession();
        session.Click(Target, Guid.NewGuid(), 100, Point, Settings);
        Assert.Null(session.Click(Target with { ItemId = "stale" }, Guid.NewGuid(), 90, Point, Settings));
        Assert.NotNull(session.Click(Target, Guid.NewGuid(), 200, Point, Settings));
    }

    [Fact]
    public void Extreme_physical_coordinates_and_timestamps_do_not_overflow_into_a_match()
    {
        var session = new ContentActivationSession();
        session.Click(Target, Guid.NewGuid(), 0, new(int.MinValue, int.MinValue), Settings);
        Assert.Null(session.Click(Target, Guid.NewGuid(), long.MaxValue, new(int.MaxValue, int.MaxValue), Settings));
    }

    [Fact]
    public void Reset_for_drag_cancel_or_invalidation_discards_pair_and_does_not_revive_duplicate_release()
    {
        var session = new ContentActivationSession(); var first = Guid.NewGuid();
        session.Click(Target, first, 100, Point, Settings);
        session.Reset();
        Assert.Null(session.Click(Target, first, 100, Point, Settings));
        Assert.Null(session.Click(Target, Guid.NewGuid(), 200, Point, Settings));
        Assert.NotNull(session.Click(Target, Guid.NewGuid(), 300, Point, Settings));
    }

    [Fact]
    public void Enter_opens_once_per_real_key_press_and_cannot_switch_target_while_held()
    {
        var session = new ContentActivationSession();
        var first = session.KeyDown(Target, true);
        Assert.NotNull(first); Assert.Equal(ContentOpenOrigin.Enter, first.Origin);
        Assert.Null(session.KeyDown(Target, true, true));
        Assert.Null(session.KeyDown(Target with { ItemId = "other" }, true));
        session.KeyUp();
        var second = session.KeyDown(Target with { ItemId = "other" }, true);
        Assert.NotNull(second); Assert.Equal("other", second.Target.ItemId);
        Assert.NotEqual(first.ActivationId, second.ActivationId);
    }

    [Fact]
    public void No_focus_repeat_after_focus_loss_and_orphan_repeat_never_open()
    {
        var session = new ContentActivationSession();
        Assert.Null(session.KeyDown(Target, false));
        Assert.Null(session.KeyDown(Target, true, true));
        Assert.Null(session.KeyDown(Target with { ItemId = "other" }, true));
        session.KeyUp(); Assert.NotNull(session.KeyDown(Target, true));
        session.LostFocus(); Assert.Null(session.KeyDown(Target, true, true));
        session.KeyUp(); Assert.NotNull(session.KeyDown(Target, true));
    }

    [Fact]
    public void Enter_or_focus_loss_does_not_complete_an_older_mouse_pair()
    {
        var session = new ContentActivationSession();
        session.Click(Target, Guid.NewGuid(), 100, Point, Settings);
        Assert.NotNull(session.KeyDown(Target, true));
        Assert.Null(session.Click(Target, Guid.NewGuid(), 200, Point, Settings));
        session.LostFocus();
        Assert.Null(session.Click(Target, Guid.NewGuid(), 300, Point, Settings));
    }

    [Theory]
    [InlineData("target")]
    [InlineData("capture")]
    [InlineData("time")]
    [InlineData("settings")]
    public void Invalid_click_input_throws_without_leaving_the_previous_pair_armed(string invalid)
    {
        var session = new ContentActivationSession();
        session.Click(Target, Guid.NewGuid(), 100, Point, Settings);
        Assert.ThrowsAny<ArgumentException>(() => session.Click(
            invalid == "target" ? Target with { PresentationWitness = "" } : Target,
            invalid == "capture" ? Guid.Empty : Guid.NewGuid(), invalid == "time" ? -1 : 150, Point,
            invalid == "settings" ? Settings with { HorizontalTolerancePixels = -1 } : Settings));
        Assert.Null(session.Click(Target, Guid.NewGuid(), 200, Point, Settings));
    }

    [Fact]
    public void Invalid_enter_identity_cannot_fall_through_to_another_target_on_the_same_press()
    {
        var session = new ContentActivationSession();
        Assert.Throws<ArgumentException>(() => session.KeyDown(Target with { ItemSetVersion = 0 }, true));
        Assert.Null(session.KeyDown(Target, true));
        session.KeyUp(); Assert.NotNull(session.KeyDown(Target, true));
    }
}
