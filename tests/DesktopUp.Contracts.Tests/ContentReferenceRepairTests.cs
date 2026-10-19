using DesktopUp.Windows;
using DesktopUp.Application;
using DesktopUp.Infrastructure;
using static DesktopUp.Contracts.Tests.ContentReferencePersistenceTests;

namespace DesktopUp.Contracts.Tests;

public sealed class ContentReferenceRepairTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("source-changed")]
    [InlineData("source-missing")]
    [InlineData("host-busy")]
    [InlineData("state-stale")]
    [InlineData("cancelled")]
    public async Task RepairRechecksSourceAndHostBeforeOneExistingTransaction(string scenario)
    {
        using var f = new Fixture();
        string original = Path.Combine(f.Paths.Root, "original.txt"), replacement = Path.Combine(f.Paths.Root, "replacement.txt");
        File.WriteAllText(original, "owned original");
        File.WriteAllText(replacement, "owned replacement");
        f.Commit(f.AddCommand(f.Main, new ContentReferenceEntry(LocalContentReferenceProbe.Inspect(original).Item!, ContentReferenceChoice.CreateIndependent)));
        var before = f.Current;
        var relation = before.ContentCatalog.PosterItems.Single();
        var useCase = new ContentReferenceRepairUseCase(new LocalContentReferenceSource(), f.Coordinator);
        var target = ContentReferenceRepairUseCase.Capture(before, f.Main, relation.LayoutItemId)!;
        Assert.Single(target.AffectedPosters);
        var preview = await useCase.PreviewAsync(target, replacement);
        Assert.NotNull(preview.Source.Item);
        if (scenario == "source-changed") File.AppendAllText(replacement, " changed");
        if (scenario == "source-missing") File.Delete(replacement);
        if (scenario == "state-stale")
        {
            var changed = f.Coordinator.Execute(new PosterStateCommand("repair-stale",
                StateCommandSource.ControlCenter, PosterStateCommandKind.Rename, before.Revision, f.Main,
                before.PostersById[f.Main].Poster with { Title = "changed" })
                { ReadSet = StateReadSet.ForPoster(f.Main, before.PostersById[f.Main].EntityVersion) });
            Assert.True(changed.Succeeded, changed.Error);
        }
        long revision = f.Current.Revision;
        int guardCalls = 0;
        ContentRepairResult? result = null;
        var error = await Record.ExceptionAsync(async () => result = await useCase.ApplyAsync(preview,
            () => { guardCalls++; return scenario != "host-busy"; }, new CancellationToken(scenario == "cancelled")));
        if (scenario == "cancelled") Assert.IsAssignableFrom<OperationCanceledException>(error);
        else Assert.Null(error);
        Assert.Equal(scenario is "source-changed" or "source-missing" or "cancelled" ? 0 : 1, guardCalls);
        bool applied = scenario == "success";
        Assert.Equal(applied, result?.Command?.Succeeded == true);
        Assert.Equal(revision + (applied ? 1 : 0), f.Current.Revision);
        Assert.Equal("owned original", File.ReadAllText(original));
        Assert.Equal(relation, f.Current.ContentCatalog.PosterItems.Single());
        Assert.Equal(before.PostersById[f.Main].Poster.ContentLayoutJson, f.Current.PostersById[f.Main].Poster.ContentLayoutJson);
        var item = f.Current.ContentCatalog.Items.Single();
        Assert.Equal(applied ? replacement : original, item.Target.Locator);
        if (scenario is "source-changed" or "source-missing") Assert.Equal(ContentRepairStatus.SourceChanged, result!.Status);
        if (scenario == "host-busy") Assert.Equal(ContentRepairStatus.HostUnavailable, result!.Status);
        if (scenario == "state-stale") Assert.Equal(StateCommandStatus.Conflict, result!.Command!.Status);
        if (applied)
        {
            var repeated = await useCase.ApplyAsync(preview, () => true);
            Assert.False(repeated.Command!.Succeeded);
            Assert.Equal(revision + 1, f.Current.Revision);
            Assert.Equal("owned replacement", File.ReadAllText(replacement));
        }
    }

    [Fact]
    public async Task MissingTargetOrSourceCannotBecomeARepairCommand()
    {
        using var f = new Fixture();
        Assert.Null(ContentReferenceRepairUseCase.Capture(f.Current, f.Main, null));
        Assert.Null(ContentReferenceRepairUseCase.Capture(f.Current, "absent", "item"));
        f.Commit(f.AddCommand(f.Main, Entry()));
        var target = ContentReferenceRepairUseCase.Capture(f.Current, f.Main, f.Current.ContentCatalog.PosterItems.Single().LayoutItemId)!;
        var useCase = new ContentReferenceRepairUseCase(new LocalContentReferenceSource(), f.Coordinator);
        var preview = await useCase.PreviewAsync(target, Path.Combine(f.Paths.Root, "missing.txt"));
        Assert.Null(preview.Source.Item);
        var result = await useCase.ApplyAsync(preview, () => throw new InvalidOperationException("Must not reach host."));
        Assert.Equal(ContentRepairStatus.SourceChanged, result.Status);
        Assert.Null(result.Command);
    }
}
