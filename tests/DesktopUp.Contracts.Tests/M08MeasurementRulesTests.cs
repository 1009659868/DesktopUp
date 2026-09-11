namespace DesktopUp.Contracts.Tests;

public sealed class M08MeasurementRulesTests
{
    public static IEnumerable<object[]> Cases() =>
        from count in new[] { 1, 3, 6 }
        from mode in new[] { "off-expanded", "on-expanded", "off-collapsed", "on-collapsed", "off-trigger", "on-trigger" }
        select new object[] { count, mode };
    [Theory, MemberData(nameof(Cases))]
    public void Paired_modes_have_exact_expected_effects(int count, string mode)
    {
        int transitions = mode.EndsWith("-trigger", StringComparison.Ordinal) ? count : 0;
        int automatic = mode == "on-trigger" ? count : 0;
        Assert.True(DesktopUpAutoMeasurementRules.CountsMatch(count, mode, 19, automatic, automatic, transitions, transitions));
    }
    [Theory]
    [InlineData(18,3,3,3,3)] [InlineData(19,2,3,3,3)] [InlineData(19,4,3,3,3)]
    [InlineData(19,3,2,3,3)] [InlineData(19,3,4,3,3)] [InlineData(19,3,3,2,3)]
    [InlineData(19,3,3,4,3)] [InlineData(19,3,3,3,4)]
    public void Missing_samples_duplicate_effects_or_revision_writes_invalidate(int samples, int requests, int commits, int postures, long revision) =>
        Assert.False(DesktopUpAutoMeasurementRules.CountsMatch(3, "on-trigger", samples, requests, commits, postures, revision));
    [Theory] [InlineData(0,"off-expanded")] [InlineData(2,"off-expanded")]
    [InlineData(4,"off-expanded")] [InlineData(1,"unknown")]
    public void Unknown_capacity_or_mode_cannot_be_published(int count, string mode) =>
        Assert.False(DesktopUpAutoMeasurementRules.CountsMatch(count, mode, 19, 0, 0, 0, 0));
}
