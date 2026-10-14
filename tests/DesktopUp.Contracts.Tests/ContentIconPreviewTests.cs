using DesktopUp.Application;
using static DesktopUp.Contracts.Tests.ContentReferencePersistenceTests;
using static DesktopUp.Contracts.Tests.ContentIconPersistenceTests;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentIconPreviewTests
{
    [Fact]
    public void PreviewIsReadOnlyAndConfirmationChangesOnlyTheCapturedCard()
    {
        using var f = new Fixture(); f.Commit(f.AddCommand(f.Main, Entry()));
        var before = f.Current;
        string item = before.ContentCatalog.PosterItems.Single().LayoutItemId;
        var preview = ContentIconPreview.Capture(before, f.Main, item, [item])!;
        Assert.NotNull(preview); Assert.False(preview.HasCustomIcon);
        Assert.Equal(before.ContentCatalog.Items.Single().Target.Locator, preview.SourcePath);
        Assert.Equal(before.Revision, f.Current.Revision);
        Assert.Equal(0L, f.Sql("SELECT count(*) FROM content_icon_assets"));
        Assert.True(preview.IsCurrent(f.Current, f.Main, item, [item]));
        Assert.True(f.Coordinator.Execute(preview.Confirm(Icon())).Succeeded);
        Assert.Equal(before.ContentCatalog.Items, f.Current.ContentCatalog.Items);
        Assert.Equal(before.PostersById[f.Main].Poster, f.Current.PostersById[f.Main].Poster);
        Assert.False(preview.IsCurrent(f.Current, f.Main, item, [item]));
        var restore = ContentIconPreview.Capture(f.Current, f.Main, item, [item])!;
        Assert.True(restore.HasCustomIcon);
        Assert.True(f.Coordinator.Execute(restore.Confirm(null)).Succeeded);
        Assert.Null(f.Current.ContentCatalog.PosterItems.Single().IconAssetId);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("multiple")]
    [InlineData("item")]
    [InlineData("poster")]
    [InlineData("locked")]
    [InlineData("source")]
    public void ChangedTargetCannotAcceptALatePreview(string change)
    {
        using var f = new Fixture(); var entry = Entry(); f.Commit(f.AddCommand(f.Main, entry));
        string item = f.Current.ContentCatalog.PosterItems.Single().LayoutItemId;
        var preview = ContentIconPreview.Capture(f.Current, f.Main, item, [item])!;
        string? selectedItem = item, poster = f.Main;
        string[] selection = [item];
        switch (change)
        {
            case "none": selection = []; break;
            case "multiple": selection = [item, "another"]; break;
            case "item": selectedItem = "another"; selection = ["another"]; break;
            case "poster": poster = "other-poster"; break;
            case "locked":
                Assert.True(f.Coordinator.Execute(new PosterStateCommand(Guid.NewGuid().ToString(), StateCommandSource.ControlCenter,
                    PosterStateCommandKind.SetLocked, f.Current.Revision, f.Main,
                    f.Current.PostersById[f.Main].Poster with { IsLocked = true })).Succeeded);
                break;
            case "source": f.Commit(f.AddCommand(f.Main, Entry("repaired.txt")) with { RepairContentId = entry.Item.Id }); break;
        }
        Assert.False(preview.IsCurrent(f.Current, poster, selectedItem, selection));
        if (change != "source") Assert.Null(ContentIconPreview.Capture(f.Current, poster, selectedItem, selection));
        Assert.Null(f.Current.ContentCatalog.PosterItems.Single().IconAssetId);
        Assert.Equal(0L, f.Sql("SELECT count(*) FROM content_icon_assets"));
    }
}
