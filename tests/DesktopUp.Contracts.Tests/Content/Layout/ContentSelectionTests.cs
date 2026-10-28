using DesktopUp.Application;
namespace DesktopUp.Contracts.Tests;
public sealed class ContentSelectionTests
{
    private static readonly string[] Order=["a","b","c","d"];
    [Theory] [InlineData(false,false,"c")] [InlineData(false,true,"a,c")] [InlineData(true,false,"a,c")]
    public void MarqueeUsesStablePressSelection(bool control,bool shift,string expected)
    {
        var before=ContentSelection.Empty.Select(Order,"a");
        var result=before.Marquee(Order,["c","c","missing"],control,shift);
        Assert.Equal(expected.Split(','),result.Ids);Assert.True(result.ShowHighlight);
        Assert.Equal(result.Ids,before.Marquee(Order,["c"],control,shift).Ids);
        Assert.Equal(new[]{"a"},before.Ids); // cancellation restores this snapshot
    }
    [Fact] public void MarqueeControlTogglesOnlyOnceAndEmptyPlainBoxClears()
    {
        var before=ContentSelection.Empty.Select(Order,"a");
        Assert.Empty(before.Marquee(Order,["a","a"],true,false).Ids);
        Assert.Empty(before.Marquee(Order,[],false,false).Ids);
        Assert.Equal(new[]{"a"},before.Marquee(Order,[],false,true).Ids);
    }
    [Fact] public void DragPixelsAreBoundedAndKeepAlphaAndMultiCount()
    {
        var single=ContentDragPixels.Build(new byte[]{30,60,90,255},1,1,48,1);
        Assert.Equal(48,single.Width);Assert.Equal(30,single.Pixels[0]);
        var multiple=ContentDragPixels.Build([],0,0,500,100);
        Assert.Equal(172,multiple.Width);Assert.Equal(172*172*4,multiple.Pixels.Length);
        Assert.Contains((byte)255,multiple.Pixels);
    }
    [Fact] public void ControlTogglesWithoutDuplicateLoopCopies()
    { var s=ContentSelection.Empty.Select(Order,"b").Select(Order,"d",true); Assert.Equal(new[]{"b","d"},s.Ids); Assert.Equal(new[]{"d"},s.Select(Order,"b",true).Ids); }
    [Fact] public void ShiftUsesReadingOrderAndRetainsAnchor()
    { var s=ContentSelection.Empty.Select(Order,"d").Select(Order,"b",shift:true); Assert.Equal(new[]{"b","c","d"},s.Ids); Assert.Equal("d",s.Anchor); }
    [Fact] public void ControlShiftUnionsRangeAndPlainSelectionReturnsToOne()
    { var s=ContentSelection.Empty.Select(Order,"a").Select(Order,"c",true).Select(Order,"d",true,true); Assert.Equal(new[]{"a","c","d"},s.Ids);Assert.Single(s.Select(Order,"b").Ids); }
    [Fact] public void PruningAndClearCannotKeepRemovedIdentity()
    { var s=ContentSelection.Empty.Select(Order,"a").Select(Order,"d",shift:true).Prune(["b","c"]);Assert.Equal(new[]{"b","c"},s.Ids);Assert.Empty(s.Select(Order,null).Ids); }
}
