using DesktopUp.Application;
using DesktopUp.Domain;
using DesktopUp.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentLayoutPresentationCompatibilityTests
{
    [Fact]
    public void Readable_minimum_can_make_valid_macro_scale_incompatible_with_manual_positions()
    {
        var document = Tight();
        ContentLayoutCommandPolicy.ValidateAuthoring(document);
        Assert.ThrowsAny<ArgumentException>(() => ContentLayoutCommandPolicy.ValidateAuthoring(document, .8, 1));
        ContentLayoutCommandPolicy.ValidateAuthoring(document, 1.5, 1);
    }

    [Fact]
    public void Presentation_command_rejects_incompatible_scale_without_document_row_revision_or_receipt_changes()
    {
        string root = Path.Combine(Path.GetTempPath(), "DesktopUp.ContentMetrics.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            using var store = new SqliteStateStore(new UserDataPaths(root)); Assert.True(store.Initialize().Succeeded);
            var repository = new SqliteStateCommandRepository(store); var coordinator = new StateProjectionCoordinator(repository);
            var seed = PosterConfigurationFactory.NewBasicBox("A", "DISPLAY1") with
            { PosterId = "A", ThemeInstanceId = "theme-A", ContentLayoutJson = ContentLayoutPolicy.Serialize(Tight()) };
            var initial = coordinator.EnsureInitialized(seed); var basis = initial.PostersById["A"];
            var parameters = PosterPresentationPolicy.Resolve(seed).Parameters with { Preferences = new(.8, 1) };
            var desired = seed with { LayoutParametersJson = PosterPresentationPolicy.Serialize(parameters) };
            var command = new PosterStateCommand("unsafe-scale", StateCommandSource.ControlCenter, PosterStateCommandKind.SetPresentation,
                initial.Revision, "A", desired) { ReadSet = StateReadSet.ForPoster("A", basis.EntityVersion) };
            var rejected = coordinator.Execute(command);
            Assert.Equal(StateCommandStatus.ValidationFailed, rejected.Status);
            Assert.Contains("content layout", rejected.Error);
            Assert.Equal(initial.Revision, repository.Read().Revision);
            Assert.Equal(seed, repository.Read().Catalog.Posters[0].Poster);
            Assert.Equal(basis.EntityVersion, repository.Read().Catalog.Posters[0].EntityVersion);
            var safe = desired with { LayoutParametersJson = PosterPresentationPolicy.Serialize(parameters with { Preferences = new(1.5, 1) }) };
            var accepted = coordinator.Execute(command with { RequestId = "safe-scale", DesiredState = safe });
            Assert.True(accepted.Succeeded, accepted.Error); Assert.Equal(initial.Revision + 1, accepted.CommittedRevision);
            Assert.Equal(seed.ContentLayoutJson, repository.Read().Catalog.Posters[0].Poster.ContentLayoutJson);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Portable_document_validates_layout_and_macro_preferences_together()
    {
        var seed = PosterConfigurationFactory.NewBasicBox("A", "DISPLAY1") with
        { PosterId = "A", ThemeInstanceId = "theme-A", ContentLayoutJson = ContentLayoutPolicy.Serialize(Tight()) };
        var parameters = PosterPresentationPolicy.Resolve(seed).Parameters with { Preferences = new(.8, 1) };
        seed = seed with { LayoutParametersJson = PosterPresentationPolicy.Serialize(parameters) };
        var state = new DesktopStateSnapshot(new(1, 1, true, new[] { new PosterStateSnapshot(1, 1, seed, DateTimeOffset.UnixEpoch, 1) }),
            new(1, 1, new(false, DesktopUpPerformanceMode.Automatic, false), DateTimeOffset.UnixEpoch, 1));
        Assert.ThrowsAny<ArgumentException>(() => PortableProfileCodec.Export(state));
        Assert.Equal(ContentLayoutPolicy.Serialize(Tight()), seed.ContentLayoutJson);
    }

    private static ContentLayoutDocument Tight() => new("A", new[]
    {
        new ContentPlacement("A:item-0", 0, 2, 2) { Column = 0, Row = 0 },
        new ContentPlacement("A:item-1", 1, 2, 2) { Column = 2, Row = 0 }
    }) { Mode = ContentLayoutMode.Manual, BaseCellDip = 24, BaseGapDip = 6 };
}
