using DesktopUp.Domain;
using Xunit.Abstractions;

namespace DesktopUp.Contracts.Tests;

// Small attribution sample, not a performance certification or a new global benchmark suite.
public sealed class ContentBehaviorModelCostTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void Measure_model_and_host_projection_separately(int count)
    {
        var author = ContentLayoutSamples.Equal("cost", count) with { AllowHeatDrivenSize = true };
        var now = DateTimeOffset.Parse("2026-10-26T00:00:00Z");
        var input = new ContentModelInput(author, author.Placements.Select((p, i) => new ContentActivityInput(p.ItemId, i,
            new(Guid.NewGuid(), now.AddDays(-80), i + 1, now.AddHours(-i)))).ToArray(), new(900, 700), now, 1);
        foreach (var model in new IContentBehaviorModel[] { new ActivityContentModel(), new RecentStripContentModel() })
        {
            var modelMs = new List<double>(); var hostMs = new List<double>(); long allocated = 0;
            for (int iteration = -2; iteration < 10; iteration++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                var proposal = ContentModelProtocol.Adapt(model.Descriptor, input, model.Evaluate(input, default));
                double computed = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                start = System.Diagnostics.Stopwatch.GetTimestamp();
                var projected = ContentHeatSizing.Project(author, proposal, input.Viewport);
                var layout = ContentLayoutSolver.Resolve(projected, input.Viewport);
                double hosted = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                Assert.False(layout.IsLayoutReadOnly); Assert.Equal(count, layout.Items.Count);
                if (iteration < 0) continue;
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
                modelMs.Add(computed); hostMs.Add(hosted);
            }
            output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { model = model.Descriptor.Id, count,
                samples = modelMs.Count, modelMedianMs = modelMs.Order().ElementAt(5), modelMaxMs = modelMs.Max(),
                hostMedianMs = hostMs.Order().ElementAt(5), hostMaxMs = hostMs.Max(), allocatedBytesPerSample = allocated / 10,
                scope = "pure model + projection/layout; no storage, windows or FPS claim" }));
        }
    }
}
