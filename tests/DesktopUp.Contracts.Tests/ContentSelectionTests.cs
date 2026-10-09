using DesktopUp.Application;
namespace DesktopUp.Contracts.Tests;
public sealed class ContentSelectionTests
{
    private static readonly string[] Order=["a","b","c","d"];
    [Fact] public void ControlTogglesWithoutDuplicateLoopCopies()
    { var s=ContentSelection.Empty.Select(Order,"b").Select(Order,"d",true); Assert.Equal(new[]{"b","d"},s.Ids); Assert.Equal(new[]{"d"},s.Select(Order,"b",true).Ids); }
    [Fact] public void ShiftUsesReadingOrderAndRetainsAnchor()
    { var s=ContentSelection.Empty.Select(Order,"d").Select(Order,"b",shift:true); Assert.Equal(new[]{"b","c","d"},s.Ids); Assert.Equal("d",s.Anchor); }
    [Fact] public void ControlShiftUnionsRangeAndPlainSelectionReturnsToOne()
    { var s=ContentSelection.Empty.Select(Order,"a").Select(Order,"c",true).Select(Order,"d",true,true); Assert.Equal(new[]{"a","c","d"},s.Ids);Assert.Single(s.Select(Order,"b").Ids); }
    [Fact] public void PruningAndClearCannotKeepRemovedIdentity()
    { var s=ContentSelection.Empty.Select(Order,"a").Select(Order,"d",shift:true).Prune(["b","c"]);Assert.Equal(new[]{"b","c"},s.Ids);Assert.Empty(s.Select(Order,null).Ids); }
}
