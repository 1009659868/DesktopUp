using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentActivityPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    private static ContentActivityUsage Usage(long count, double age, double idle) =>
        new(Guid.Parse("00000000-0000-0000-0000-000000000001"), Now.AddDays(-age), count,
            count == 0 ? null : Now.AddDays(-idle));

    [Theory]
    [InlineData(3, 2, 1, 2)]
    [InlineData(30, 3, 20, 3)]
    [InlineData(31, 3, 20, 2.791)]
    [InlineData(90, 3, 14, 3)]
    [InlineData(90, 3, 15, 2.791)]
    [InlineData(90, 3, 50, .3)]
    [InlineData(90, 80, 50, 32.813)]
    public void Approved_decay_examples(double age, long count, double idle, double expected) =>
        Assert.Equal(expected, ContentActivityPolicy.Decay(Usage(count, age, idle), Now).EffectiveLaunches, 3);

    [Fact]
    public void Decay_is_continuous_at_both_protection_boundaries_and_bounded_under_clock_changes()
    {
        foreach (var usage in new[] { Usage(3, 30, 20), Usage(3, 90, 14) })
        {
            Assert.Equal(3, ContentActivityPolicy.Decay(usage, Now).EffectiveLaunches);
            Assert.InRange(ContentActivityPolicy.Decay(usage, Now.AddMilliseconds(1)).EffectiveLaunches, 2.99999, 3);
        }
        foreach (long count in new long[] { 1, 3, 80, long.MaxValue })
        {
            var usage = Usage(count, 90, 50);
            var previous = (double)count;
            for (int days = 0; days < 1000; days += 11)
            {
                double effective = ContentActivityPolicy.Decay(usage, Now.AddDays(days)).EffectiveLaunches;
                Assert.InRange(effective, count * .1, previous); previous = effective;
            }
            Assert.Equal((double)count, ContentActivityPolicy.Decay(usage, Now.AddDays(-100)).EffectiveLaunches);
            var reopened = usage with { TotalLaunches = count == long.MaxValue ? count : count + 1, LastLaunchedAt = Now };
            Assert.Equal((double)reopened.TotalLaunches, ContentActivityPolicy.Decay(reopened, Now).EffectiveLaunches);
            Assert.Equal(ContentActivityPolicy.Decay(usage, Now), ContentActivityPolicy.Decay(usage, Now.ToOffset(TimeSpan.FromHours(8))));
        }
        Assert.Equal(0, ContentActivityPolicy.Decay(Usage(0, 90, 50), Now).EffectiveLaunches);
    }

    [Theory]
    [InlineData(0, 1.5, 1.5)]
    [InlineData(1, 1.571429, 1.452381)]
    [InlineData(3, 1.695652, 1.369565)]
    [InlineData(10, 2, 1.166667)]
    [InlineData(20, 2.25, 1)]
    [InlineData(80, 2.7, .7)]
    public void Low_sample_smoothing_preserves_approved_examples(double maximum, double hot, double cold)
    {
        Assert.Equal(hot, ContentActivityPolicy.Scale(maximum, maximum, new()), 6);
        Assert.Equal(cold, ContentActivityPolicy.Scale(0, maximum, new()), 6);
    }

    [Fact]
    public void Configurable_scales_remain_continuous_bounded_and_do_not_mutate_statistics()
    {
        var options = new ContentActivityOptions(.75, 2, 1, 40);
        double previous = 0;
        for (double effective = 0; effective <= 80; effective += .25)
        {
            double scale = ContentActivityPolicy.Scale(effective, 80, options);
            Assert.InRange(scale, Math.Max(.75, previous), 2); previous = scale;
        }
        Assert.Equal(3, ContentActivityPolicy.Scale(1, 1, new(SmoothingStrength: 0)));
        Assert.InRange(ContentActivityPolicy.Scale(1e-20, 1e-20, new()), 1.5, 1.500001);
        Assert.NotNull(new ContentActivityOptions(DefaultScale: double.NaN).Validate());
        Assert.NotNull(new ContentActivityOptions(MinimumScale: 2).Validate());
        Assert.NotNull(new ContentActivityOptions(SmoothingStrength: -1).Validate());
        Assert.Throws<ArgumentException>(() => ContentActivityPolicy.Decay(Usage(2, 3, 1) with { LastLaunchedAt = null }, Now));
        var input = new[] { new ContentActivityInput("A:2", 1, Usage(2, 3, 1)), new ContentActivityInput("A:1", 0, Usage(2, 3, 1)) };
        var before = input.ToArray();
        var first = ContentActivityPolicy.Evaluate(input, new(), Now);
        Assert.Equal("A:1", first[0].ItemId);
        Assert.Equal(first, ContentActivityPolicy.Evaluate(input.Reverse().ToArray(), new(), Now));
        _ = ContentActivityPolicy.Evaluate(input, options, Now);
        Assert.Equal(before, input);
    }

    [Fact]
    public void Settings_edit_roundtrips_and_undo_preserves_manual_author_intent()
    {
        var document = ContentLayoutSamples.Equal("A", 3);
        var viewport = new ContentLayoutViewport(600, 600);
        var manual = ContentLayoutEditing.Apply(document, new(ContentLayoutEditKind.Mode) { Mode = ContentLayoutMode.Manual }, viewport).Document;
        var options = new ContentActivityOptions(.75, 2.5, 1.25, 40);
        var edited = ContentLayoutEditing.Apply(manual, new(ContentLayoutEditKind.Activity) { Activity = options, Enabled = true }, viewport);
        Assert.True(edited.Accepted, edited.Error);
        Assert.Equal(manual.Placements, edited.Document.Placements);
        Assert.Equal(ContentLayoutMode.Manual, edited.Document.Mode);
        Assert.Equal(options, edited.Document.Activity);
        Assert.True(ContentLayoutPolicy.TryRead(ContentLayoutPolicy.Serialize(edited.Document), "A", out var reopened, out var error), error);
        Assert.True(ContentLayoutEditing.SameState(edited.Document, reopened!));
        var history = new ContentLayoutHistory(); history.Record(manual, edited.Document);
        Assert.Equal(manual.Activity, history.ProposeUndo(edited.Document)!.Document.Activity);
        Assert.False(ContentLayoutEditing.Apply(manual, new(ContentLayoutEditKind.Activity)
            { Activity = options with { DefaultScale = 3 }, Enabled = true }, viewport).Accepted);
    }
}
