using DesktopUp.Application;
using DesktopUp.Domain;
namespace DesktopUp.Contracts.Tests;

public sealed class TitleCloudMotionTests
{
    [Theory]
    [InlineData(AutoPostureProtection.None,true,true)]
    [InlineData(AutoPostureProtection.PointerButton,false,true)]
    [InlineData(AutoPostureProtection.PointerCapture,false,true)]
    [InlineData(AutoPostureProtection.PointerButton|AutoPostureProtection.PointerCapture,false,true)]
    [InlineData(AutoPostureProtection.Menu,false,false)]
    [InlineData(AutoPostureProtection.Recovery,false,false)]
    [InlineData(AutoPostureProtection.Gesture,false,false)]
    [InlineData((AutoPostureProtection)1024,false,false)]
    public void OnlySizeEditsAllowExternalSliderProtection(AutoPostureProtection protection,bool wind,bool size)
    {
        Assert.Equal(wind,TitleCloudInputGeometry.AllowsProtection(protection,false));
        Assert.Equal(size,TitleCloudInputGeometry.AllowsProtection(protection,true));
    }
    [Theory]
    [InlineData(240, 1, 1)] [InlineData(300, 1.25, 1.5)] [InlineData(800, 2, 2)] [InlineData(1600, 1.5, 1.25)]
    public void MovingGripsStayWithinPaintAndFrame(double width, double sx, double sy)
    {
        foreach (bool collapsed in new[] { false, true })
        foreach(double size in new[]{.6,1,1.1})
        foreach (var requested in new[] { new TitleCloudPose(12, -10, 4.5,Scale:size), new TitleCloudPose(-12, 10, -4.5,Scale:size), new(0,0,0,Scale:size) })
        {
            var pose=requested.Fit(width);
            var bands = TitleCloudInputGeometry.Resolve(width, collapsed, pose, sx, sy);
            Assert.NotEmpty(bands);
            Assert.True(bands.Count < 800);
            foreach (var b in bands)
            {
                Assert.True(b.X >= 0 && b.Y >= 0 && b.Right <= Math.Round(width * sx) && b.Bottom <= Math.Ceiling((collapsed ? 60 : 84) * sy));
                // Every merged strip can contain multiple source rectangles; sample all physical pixel corners.
                for (int x = b.X; x <= b.Right; x++)
                    foreach (int y in new[] { b.Y, b.Bottom })
                    {
                        double c = Math.Cos(pose.Angle * Math.PI / 180), s = Math.Sin(pose.Angle * Math.PI / 180);
                        double dx = (x / sx - width / 2 - pose.X)/pose.Scale, dy = (y / sy - TitleCloudPose.CenterY - pose.Y)/pose.Scale;
                        double px = c * dx + s * dy + width / 2, py = -s * dx + c * dy + TitleCloudPose.CenterY;
                        bool painted = TitleCloudShape.Contains(width,px,py);
                        Assert.True(painted, $"Unpainted grab pixel ({x},{y}) pose={pose}");
                    }
            }
        }
    }

    [Fact]
    public void OppositeDriftMovesGripsWithoutMovingActionSlots()
    {
        var left = TitleCloudInputGeometry.Resolve(300, false, new(-10, -2, -.6), 1, 1);
        var right = TitleCloudInputGeometry.Resolve(300, false, new(10, -2, .6), 1, 1);
        Assert.False(left.SequenceEqual(right));
        Assert.True(left.Min(b=>b.X)<right.Min(b=>b.X));
        Assert.True(left.Max(b=>b.Right)<right.Max(b=>b.Right));
        Assert.Equal(right, TitleCloudInputGeometry.Resolve(300, false, new(10, -2, .6), 1, 1));
    }

    [Fact]
    public void MotionReplacementCannotRewriteContentOrReuseRevision()
    {
        PosterInputRegion grip = new(PosterInputTargetKind.TitleBar, -1, new(20, 25, 230, 30));
        PosterInputRegion item = new(PosterInputTargetKind.ContentItem, 0, new(20, 100, 60, 60));
        var before = new PosterInputLayoutSnapshot(1, 300, 360, new[] { item, grip }, 0);
        var after = before with { Revision = 2, Regions = new[] { item, grip with { Bounds = new(25, 25, 230, 30) } } };
        Assert.True(TitleCloudInputGeometry.IsTitleOnlyReplacement(before, after));
        Assert.False(TitleCloudInputGeometry.IsTitleOnlyReplacement(before, after with { Revision = 1 }));
        Assert.False(TitleCloudInputGeometry.IsTitleOnlyReplacement(before, after with { Regions = new[] { grip } }));
        Assert.False(TitleCloudInputGeometry.IsTitleOnlyReplacement(before, after with { FrameWidth = 301 }));
    }

    [Fact]
    public void InvalidPoseFallsBackAndCloudClockDoesNotCatchUpAfterHold()
    {
        Assert.Equal(new TitleCloudPose(0,0,0), new TitleCloudPose(double.NaN, 0, 0).Bounded());
        Assert.Equal(new TitleCloudPose(12, -10, 4.5), new TitleCloudPose(100, -300, 60).Bounded());
        Assert.Equal(1,default(TitleCloudPose).Bounded().Scale);
        var clock = new SceneActiveTime(); clock.Step(1, true); clock.Step(1.1, true);
        double before = clock.Seconds; clock.Step(100, false); clock.Step(200, true);
        Assert.Equal(before, clock.Seconds); clock.Step(200.033, true);
        Assert.InRange(clock.Seconds - before, .0329, .0331);
    }

    [Theory]
    [InlineData(240)] [InlineData(300)] [InlineData(800)] [InlineData(1600)] [InlineData(3840)]
    public void EntireVisibleOutlineRemainsInsideCompactBudgetThroughoutWindCycle(double width)
    {
        var shape=TitleCloudShape.Resolve(width);
        foreach(double size in new[]{.6,1,1.1})
        for(int i=0;i<=1080;i++)
        {
            var p=RainSceneMotion.Cloud(i*.1,100);
            var pose=new TitleCloudPose(p.X,p.Y,p.Angle,p.Vapor,p.Wisp,size).Fit(width);
            Assert.Equal(size,pose.Scale);
            foreach(var point in shape)
            {
                var transformed=pose.Transform(point.X,point.Y,width);
                Assert.InRange(transformed.X,1.999,width-1.999);
                Assert.InRange(transformed.Y,1.999,58.001);
            }
            Assert.Equal(pose,pose.Fit(width));
        }
    }

    [Fact]
    public void BreezeRisesRocksAndMaterialHasSeparateContinuousPhases()
    {
        var a=RainSceneMotion.Cloud(0,100); var b=RainSceneMotion.Cloud(9.5,100);
        var high=RainSceneMotion.Cloud(4.75,100); var low=RainSceneMotion.Cloud(14.25,100);
        Assert.True(high.Y-low.Y>11); Assert.True(a.Angle-b.Angle>6);
        Assert.True(high.Y-low.Y>high.X-low.X); // lift has more weight than lateral drift
        var c=RainSceneMotion.Cloud(19,100);
        Assert.Equal(a.X,c.X,6); Assert.Equal(a.Y,c.Y,6); Assert.Equal(a.Angle,c.Angle,6);
        Assert.NotEqual(a.Vapor,c.Vapor); Assert.NotEqual(a.Wisp,c.Wisp);
        var before=RainSceneMotion.Cloud(19-.001,100); var after=RainSceneMotion.Cloud(19+.001,100);
        Assert.InRange(Math.Abs(before.Y-after.Y),0,6*Math.Tau/19*.002+1e-9);
        Assert.InRange(Math.Abs(before.Angle-after.Angle),0,.001);
    }

    [Fact]
    public void SizeScalesPaintAndHitButLeavesAuthoringAndWindRequestUnchanged()
    {
        var p=RainSceneMotion.Cloud(4.75,100);
        var small=new TitleCloudPose(p.X,p.Y,p.Angle,Scale:.6).Fit(300);
        var large=new TitleCloudPose(p.X,p.Y,p.Angle,Scale:1.1).Fit(300);
        var a=TitleCloudInputGeometry.Resolve(300,false,small,1,1);
        var b=TitleCloudInputGeometry.Resolve(300,false,large,1,1);
        Assert.True(b.Sum(r=>r.Width*r.Height)>a.Sum(r=>r.Width*r.Height)*2);
        Assert.Equal(1.1,large.Scale); Assert.Equal(p,RainSceneMotion.Cloud(4.75,100));
        var poses=Enumerable.Range(0,191).Select(i=>RainSceneMotion.Cloud(i/10.0,100))
            .Select(v=>new TitleCloudPose(v.X,v.Y,v.Angle).Fit(300)).ToArray();
        Assert.True(poses.Max(v=>v.Y)-poses.Min(v=>v.Y)>8);
        Assert.True(poses.Max(v=>v.Angle)-poses.Min(v=>v.Angle)>4);
    }

    [Theory] [InlineData(240)] [InlineData(300)] [InlineData(800)] [InlineData(3840)]
    public void CloudMaterialDriftRemainsWithinSolidCore(double width)
    {
        foreach(double drift in new[]{-1.0,0,1})
        foreach(var ellipse in new[]{(X:width*.5+drift*5,Y:38.0,Rx:width*.23,Ry:7.0),
            (X:width*.53+drift*6,Y:44.0,Rx:width*.19,Ry:5.0)})
            for(int i=0;i<80;i++)
            {
                double a=i/80.0*Math.Tau;
                Assert.True(TitleCloudShape.Contains(width,ellipse.X+Math.Cos(a)*ellipse.Rx,ellipse.Y+Math.Sin(a)*ellipse.Ry));
            }
    }
}
