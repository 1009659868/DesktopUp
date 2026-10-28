using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

internal static class TitleTestData
{
    internal static TitlePress Press(TitlePresentedPosture posture = TitlePresentedPosture.Expanding) =>
        new("title-1", 1, 1,
            new(Guid.Parse("00000000-0000-0000-0000-000000000001"),
                new(new("A", 1), 1, new(100, 200, 300, posture == TitlePresentedPosture.Compact ? 60 : 360),
                    new(PosterInputTargetKind.TitleBar, 0, new(12, 8, 240, 40)))),
            new(120, 218), posture, new(AutoPosturePresence.ExpansionZone, 2, 1));

    internal static TitleInteractionState Start(TitlePress? press = null) =>
        Assert.IsType<TitleInteractionState>(TitleInteractionModel.Begin(null, press ?? Press()).State);
    internal static TitlePointerInput Input(TitleInteractionState state, TitlePointerEventKind kind,
        int dx = 0, int dy = 0, bool invoke = true) =>
        new(state.Token, state.Sequence + 1, kind,
            new(state.Press.Position.X + dx, state.Press.Position.Y + dy),
            invoke, new(AutoPosturePresence.ExpansionZone, 2, 1));
    internal static TitleInteractionStep Move(TitleInteractionState state, int x, int y = 0) =>
        TitleInteractionModel.Advance(state, Input(state, TitlePointerEventKind.Move, x, y));
    internal static TitleInteractionStep Release(TitleInteractionState state, int x = 0, int y = 0, bool invoke = true) =>
        TitleInteractionModel.Advance(state, Input(state, TitlePointerEventKind.Release, x, y, invoke));
}
