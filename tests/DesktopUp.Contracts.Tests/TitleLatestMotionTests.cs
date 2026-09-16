using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class TitleLatestMotionTests
{
    [Theory]
    [InlineData(120)]
    [InlineData(-60)]
    public void ConfirmedDragConsumesNewestPositionInEachBurst(int latest)
    {
        var scene = new SpatialSnapshot(TitleSpatialTestData.Env, [TitleSpatialTestData.P("C", 100, 100)]);
        var s = new TitleSpatialSession(TitleSpatialTestData.Start(scene), TitleSpatialTestData.Metrics);
        var d = new TitlePresentationDriver(s);
        TitlePointerInput Move(int seq, int dx) => TitleSpatialTestData.Input(s, TitlePointerEventKind.Move, dx).Pointer! with { Sequence = seq };
        d.Queue(Move(2, 10)); d.Tick(() => true, _ => true);
        d.Queue(Move(3, 20)); d.Queue(Move(4, 30)); d.Queue(Move(5, latest));
        d.Tick(() => true, _ => true);
        Assert.Equal(100 + latest, d.Physical["C"].X);
    }
}
