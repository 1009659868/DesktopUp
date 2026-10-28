using DesktopUp.Application;
using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterAnimationConfigurationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopUp.Animation.Tests", Guid.NewGuid().ToString("N"));
    private string PathName => Path.Combine(_root, "poster-animation.json");
    private void Write(string json) { Directory.CreateDirectory(_root); File.WriteAllText(PathName, json); }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Fact]
    public void DefaultsAndCustomOptionsAreStartupSnapshots()
    {
        Assert.Equal(new PosterAnimationOptions(), PosterAnimationConfiguration.Read(PathName));
        Assert.Throws<FileNotFoundException>(() => PosterAnimationConfiguration.Read(PathName, true));
        Write("""{"schemaVersion":1,"posture":{"maximumStepPixels":4,"frameIntervalMs":8,"durationMs":400,"easing":"linear"}}""");
        var first = PosterAnimationConfiguration.Read(PathName, true);
        Assert.Equal(new PosterAnimationOptions(4, 8, 400, "linear"), first);
        Write("""{"schemaVersion":1,"posture":{"maximumStepPixels":8}}""");
        Assert.Equal(4, first.MaximumStepPixels);
        Assert.Equal(new PosterAnimationOptions(MaximumStepPixels: 8), PosterAnimationConfiguration.Read(PathName, true));
    }

    [Theory]
    [InlineData("null")] [InlineData("{")] [InlineData("{\"schemaVersion\":2}")]
    [InlineData("{\"posture\":null}")] [InlineData("{\"posture\":{\"maximumStepPixels\":0}}")]
    [InlineData("{\"posture\":{\"maximumStepPixels\":65}}")]
    [InlineData("{\"posture\":{\"frameIntervalMs\":3}}")]
    [InlineData("{\"posture\":{\"frameIntervalMs\":101}}")]
    [InlineData("{\"posture\":{\"durationMs\":79}}")]
    [InlineData("{\"posture\":{\"durationMs\":2001}}")]
    [InlineData("{\"posture\":{\"easing\":\"bounce\"}}")]
    [InlineData("{\"posture\":{\"MaximumStepPixels\":4}}")]
    [InlineData("{\"posture\":{\"maximumStepPixels\":4,\"maximumStepPixels\":8}}")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1}")]
    public void InvalidConfigurationIsExplicitlyRejected(string json)
    {
        Write(json); Assert.ThrowsAny<Exception>(() => PosterAnimationConfiguration.Read(PathName, true));
    }

    [Fact]
    public void OversizedFileIsRejectedBeforeDeserializing()
    {
        Write(new string(' ', 16 * 1024 + 1));
        Assert.Throws<InvalidDataException>(() => PosterAnimationConfiguration.Read(PathName, true));
    }
}
