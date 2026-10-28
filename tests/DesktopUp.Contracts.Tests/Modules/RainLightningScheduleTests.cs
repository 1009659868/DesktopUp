using DesktopUp.Application;
namespace DesktopUp.Contracts.Tests;

public sealed class RainLightningScheduleTests
{
    [Theory] [InlineData(8)] [InlineData(23)] [InlineData(90)]
    public void IntervalControlsFrequencyWithoutStrobe(double interval)
    {
        var clock=new RainLightningSchedule(42); uint last=0; var times=new List<double>();
        for(int i=0;i<40000;i++)
        {
            double t=i*.01; var frame=clock.Step(t,true,interval);
            Assert.InRange(frame.Opacity,0,.24);
            if(frame.StrokeSeed!=last) { times.Add(t); last=frame.StrokeSeed; }
        }
        Assert.True(times.Count>=3);
        for(int i=1;i<times.Count;i++) Assert.InRange(times[i]-times[i-1],interval-.001,interval+.021);
    }
    [Fact]
    public void EachStrikeChangesPositionButSamePulseAndParameterEditDoNotTeleport()
    {
        var clock=new RainLightningSchedule(42); clock.Step(0,true,23);
        var first=clock.Step(17,true,23); var mid=clock.Step(17.35,true,8);
        Assert.Equal(first.StrokeSeed,mid.StrokeSeed); Assert.True(mid.Opacity>.23);
        Assert.Equal(first.StrokeSeed,clock.Step(24,true,8).StrokeSeed);
        Assert.NotEqual(first.StrokeSeed,clock.Step(25.36,true,8).StrokeSeed);
        Assert.Equal(0,clock.Step(26,false,8).Opacity);
        Assert.Equal(0,clock.Step(400,true,8).Opacity);
    }
    [Fact]
    public void IndependentSeedsProduceDifferentBoundedConnectedBolts()
    {
        var area=new RainSceneEnvelope(12,0,276,64,558);
        var starts=new HashSet<double>();
        for(uint seed=1;seed<=100;seed++)
        {
            var paths=RainLightningGeometry.Resolve(area,seed); starts.Add(paths[0][0].X);
            Assert.Equal(paths.SelectMany(p=>p),RainLightningGeometry.Resolve(area,seed).SelectMany(p=>p));
            Assert.Equal(paths[0][2],paths[1][0]); Assert.Equal(paths[0][4],paths[2][0]);
            Assert.Equal(59,paths[0][0].Y); Assert.Equal(558,paths[0][^1].Y);
            Assert.All(paths.SelectMany(p=>p),p=>Assert.InRange(p.X,area.Left+area.Width*.025,area.Left+area.Width*.975));
        }
        Assert.True(starts.Count>95); Assert.True(starts.Max()-starts.Min()>100);
    }
    [Fact]
    public void InvalidTimeAndIntervalFailSafe()
    {
        var clock=new RainLightningSchedule(1);
        Assert.Equal(0,clock.Step(double.NaN,true,8).Opacity);
        Assert.Equal(23,RainLightningSchedule.Interval(double.NaN));
        Assert.Equal(8,RainLightningSchedule.Interval(-1)); Assert.Equal(90,RainLightningSchedule.Interval(999));
    }
}
