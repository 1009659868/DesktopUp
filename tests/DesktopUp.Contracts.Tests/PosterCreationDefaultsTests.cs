using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class PosterCreationDefaultsTests
{
    [Fact]
    public void NewLampIsTwentyFivePercentOpaqueWithoutChangingGeometryOrGlobalReset()
    {
        var config = PosterCreationDefaults.Parse("""{"schemaVersion":1,"templateKey":"lamp","backgroundOpacityPercent":25}""");
        var original = PosterConfigurationFactory.NewBasicBox("new", "DISPLAY");
        var poster = config.ApplyToNew(original);
        Assert.Equal("lamp", poster.TemplateKey);
        Assert.Equal(25, PosterPresentationPolicy.Resolve(poster).TemplateParameters.BackgroundOpacity);
        Assert.Equal(original, poster with { TemplateKey = original.TemplateKey, LayoutParametersJson = original.LayoutParametersJson });
        Assert.Equal(100, LampTemplatePolicy.Defaults.BackgroundOpacity);
        Assert.Equal("basic-box", original.TemplateKey);
    }

    [Theory]
    [InlineData(0)] [InlineData(25)] [InlineData(75)] [InlineData(100)]
    public void OpacityIsNotInverted(double percent)
    {
        var poster = new PosterCreationDefaults("lamp", percent).ApplyToNew(PosterConfigurationFactory.NewBasicBox("x", "D"));
        Assert.Equal(percent, PosterPresentationPolicy.Resolve(poster).TemplateParameters.BackgroundOpacity);
    }

    [Theory]
    [InlineData("basic-box")] [InlineData("night-ops")]
    public void OtherBuiltInsRequireNoUnsupportedOpacity(string template)
    {
        var seed = PosterConfigurationFactory.NewBasicBox("x", "D");
        Assert.Equal(template, new PosterCreationDefaults(template, null).ApplyToNew(seed).TemplateKey);
        Assert.Throws<ArgumentException>(() => new PosterCreationDefaults(template, 25).ApplyToNew(seed));
    }

    [Theory]
    [InlineData("{}")] [InlineData("[]")]
    [InlineData("{\"schemaVersion\":2,\"templateKey\":\"lamp\",\"backgroundOpacityPercent\":25}")]
    [InlineData("{\"schemaVersion\":1,\"templateKey\":\"script\",\"backgroundOpacityPercent\":25}")]
    [InlineData("{\"schemaVersion\":1,\"templateKey\":\"lamp\",\"backgroundOpacityPercent\":-1}")]
    [InlineData("{\"schemaVersion\":1,\"templateKey\":\"lamp\",\"backgroundOpacityPercent\":101}")]
    [InlineData("{\"schemaVersion\":1,\"templateKey\":\"lamp\",\"backgroundOpacityPercent\":null}")]
    [InlineData("{\"schemaVersion\":1,\"templateKey\":\"lamp\",\"backgroundOpacityPercent\":25,\"script\":\"bad\"}")]
    [InlineData("{\"schemaVersion\":1,\"templateKey\":\"lamp\",\"backgroundOpacityPercent\":25,\"templateKey\":\"basic-box\"}")]
    public void UnsupportedOrAmbiguousPackageDataIsRejected(string json) =>
        Assert.Throws<ArgumentException>(() => PosterCreationDefaults.Parse(json));

    [Fact]
    public void OversizedAndNonfiniteDataIsRejected()
    {
        Assert.Throws<ArgumentException>(() => PosterCreationDefaults.Parse(new string(' ', 4097)));
        Assert.Throws<ArgumentException>(() => new PosterCreationDefaults("lamp", double.NaN)
            .ApplyToNew(PosterConfigurationFactory.NewBasicBox("x", "D")));
    }
}
