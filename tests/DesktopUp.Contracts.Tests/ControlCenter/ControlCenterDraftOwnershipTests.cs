using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed partial class ControlCenterEditTransactionTests
{
    [Fact]
    public void Ownership_second_setting_preserves_first_draft_without_finishing_or_queuing()
    {
        using var f = new Fixture();
        var session = new ControlCenterSettingsSession();
        var first = new PosterSettingEdit(f.Current.PostersById["A"], PosterSetting.Scale, 1.2);
        Assert.True(session.Update(first));
        Assert.False(session.Update(first with { Setting = PosterSetting.Spacing, Number = 1.4 }));
        Assert.Equal(first, session.Preview);
        Assert.Null(session.TakeNext());
        Assert.Equal(0, session.QueuedCount);
        session.Finish();
        var command = session.TakeNext()!;
        var result = f.Coordinator.Execute(command);
        Assert.True(result.Succeeded, result.Error);
        Assert.True(session.Complete(result));
        Assert.Equal(new PosterContentPreferences(1.2, 1), f.Current.PostersById["A"].Presentation.Parameters.Preferences);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ownership_completed_or_inflight_same_poster_cannot_queue_a_conflicting_operation(bool dispatched)
    {
        using var f = new Fixture();
        var session = new ControlCenterSettingsSession();
        var edit = new PosterSettingEdit(f.Current.PostersById["A"], PosterSetting.Scale, 1.2);
        session.Update(edit); session.Finish();
        var pending = dispatched ? session.TakeNext() : null;
        Assert.False(session.Update(edit with { Setting = PosterSetting.Spacing, Number = 1.4 }));
        Assert.Null(session.Preview);
        Assert.Equal(dispatched ? 0 : 1, session.QueuedCount);
        var result = f.Coordinator.Execute(pending ?? session.TakeNext()!);
        Assert.True(session.Complete(result));
        Assert.Null(session.TakeNext());
        Assert.Equal(1, f.Current.PostersById["A"].Presentation.Parameters.Preferences!.Spacing);
    }

    [Fact]
    public void Ownership_invalid_input_stays_a_cancelable_draft_and_never_commits()
    {
        using var f = new Fixture();
        var session = new ControlCenterSettingsSession();
        var edit = new PosterSettingEdit(f.Current.PostersById["A"], PosterSetting.AutoDelay, Text: "abc");
        Assert.False(session.Update(edit));
        Assert.Equal(edit, session.Preview);
        Assert.True(session.ProtectsInteraction);
        session.Finish();
        Assert.Null(session.TakeNext());
        Assert.Equal(edit, session.Preview);
        session.CancelPreview();
        Assert.False(session.ProtectsInteraction);
    }
}
