using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;
public sealed class DirectDesktopDropPolicyTests
{
    [Theory]
    [InlineData(PosterInputEventKind.GesturePreview,350,true,true,true,DirectDesktopDropDecision.Preview)]
    [InlineData(PosterInputEventKind.GestureCompleted,350,true,true,true,DirectDesktopDropDecision.Commit)]
    [InlineData(PosterInputEventKind.GestureCancelled,350,true,true,true,DirectDesktopDropDecision.Internal)]
    [InlineData(PosterInputEventKind.Invoked,350,true,true,true,DirectDesktopDropDecision.Internal)]
    [InlineData(PosterInputEventKind.GestureCompleted,200,true,true,true,DirectDesktopDropDecision.Internal)]
    [InlineData(PosterInputEventKind.GestureCompleted,350,false,true,true,DirectDesktopDropDecision.Reject)]
    [InlineData(PosterInputEventKind.GestureCompleted,350,true,false,true,DirectDesktopDropDecision.Reject)]
    [InlineData(PosterInputEventKind.GestureCompleted,350,true,true,false,DirectDesktopDropDecision.Reject)]
    public void OnlyValidDesktopReleaseCanCommit(PosterInputEventKind kind,int x,bool desktop,bool move,bool ready,DirectDesktopDropDecision expected)
        =>Assert.Equal(expected,DirectDesktopDropPolicy.Resolve(kind,new(x,150),new(100,100,200,300),true,true,desktop,move,ready));
    [Fact] public void LeavingReturningAndLeavingAgainDoesNotCommitUntilRelease()
    {
        var frame=new DesktopPixelRect(100,100,200,300);
        foreach(int x in new[]{350,200,400,201,330})
            Assert.NotEqual(DirectDesktopDropDecision.Commit,DirectDesktopDropPolicy.Resolve(PosterInputEventKind.GesturePreview,new(x,150),frame,true,true,true,true,true));
        Assert.Equal(DirectDesktopDropDecision.Commit,DirectDesktopDropPolicy.Resolve(PosterInputEventKind.GestureCompleted,new(330,150),frame,true,true,true,true,true));
    }
}
