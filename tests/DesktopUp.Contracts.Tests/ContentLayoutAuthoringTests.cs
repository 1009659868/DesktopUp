using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentLayoutAuthoringTests
{
    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Canonical_authoring_has_the_same_cross_capacity_on_both_axes(ContentFlowAxis axis)
    {
        var document = ContentLayoutSamples.Equal("A", 1000) with
        {
            FlowAxis = axis, MaximumColumns = 32, BaseCellDip = 96, BaseGapDip = 40,
            Placements = ContentLayoutSamples.Equal("A", 1000).Placements
                .Select(p => p with { ColumnSpan = 32, RowSpan = 32 }).ToArray()
        };
        string before = ContentLayoutPolicy.Serialize(document);
        ContentLayoutCommandPolicy.ValidateAuthoring(document);
        Assert.Equal(before, ContentLayoutPolicy.Serialize(document));
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Read_only_space_shortage_cannot_be_accepted_as_authored_intent(ContentFlowAxis axis)
    {
        var document = ContentLayoutSamples.Equal("A", 1) with { FlowAxis = axis, MaximumColumns = 2 };
        string before = ContentLayoutPolicy.Serialize(document);
        Assert.ThrowsAny<ArgumentException>(() => ContentLayoutCommandPolicy.ValidateAuthoring(document));
        Assert.Equal(before, ContentLayoutPolicy.Serialize(document));
    }

    [Theory]
    [InlineData(ContentFlowAxis.Vertical)]
    [InlineData(ContentFlowAxis.HorizontalLoop)]
    public void Read_only_legacy_overflow_cannot_be_saved_as_a_valid_new_layout(ContentFlowAxis axis)
    {
        var document = ContentLayoutSamples.Equal("A", 1) with
        {
            FlowAxis = axis, MaximumColumns = 8,
            Placements = [new ContentPlacement("A:item-0000", 0, 4, 4)
            { Column = axis == ContentFlowAxis.Vertical ? 7 : 0,
              Row = axis == ContentFlowAxis.Vertical ? 0 : 7, FixedPosition = true }]
        };
        string before = ContentLayoutPolicy.Serialize(document);
        Assert.ThrowsAny<ArgumentException>(() => ContentLayoutCommandPolicy.ValidateAuthoring(document));
        Assert.Equal(before, ContentLayoutPolicy.Serialize(document));
    }
}
