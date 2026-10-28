using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class RealContentCompatibilityTests
{
    [Fact]
    public void MissingOpeningSemanticsCannotAcquireDefaults()
    {
        var json = ContentCatalogCodec.Write(RealContentFixtures.Catalog());
        foreach (var raw in new[]
        {
            json.Replace("\"arguments\":\"\",", ""),
            json.Replace(",\"workingDirectory\":\"\"", ""),
            json.Replace("\"kind\":0,", "")
        })
        {
            Assert.NotEqual(json, raw);
            var read = ContentCatalogCodec.Read(raw);
            Assert.NotEqual(ContentCatalogReadStatus.Supported, read.Status);
            Assert.Equal(raw, read.Original);
            Assert.Null(read.Catalog);
        }
    }

    [Fact]
    public void WriterCannotEmitAContractThatExceedsReaderBudget()
    {
        var item = RealContentFixtures.Item() with
        {
            Target = RealContentFixtures.Item().Target with { Arguments = new string('x', 32768) }
        };
        var catalog = RealContentFixtures.Catalog() with
        {
            Items = Enumerable.Range(0, 150).Select(_ => item with { Id = Guid.NewGuid() })
                .Prepend(RealContentFixtures.Item()).ToArray()
        };
        Assert.Throws<ArgumentException>(() => ContentCatalogCodec.Write(catalog));
        Assert.Equal(ContentCatalogReadStatus.Invalid,
            ContentCatalogCodec.Read(new string(' ', ContentCatalogCodec.MaximumBytes + 1)).Status);
    }

    [Fact]
    public void MissingAvailabilityOrImportedComputedIdentityCannotBeSilentlyDefaulted()
    {
        var json = ContentCatalogCodec.Write(RealContentFixtures.Catalog());
        foreach (var raw in new[]
        {
            json.Replace("\"availability\":0,", ""),
            json.Replace("\"membershipId\":", "\"layoutItemId\":\"foreign:forged\",\"membershipId\":")
        })
        {
            var result = ContentCatalogCodec.Read(raw);
            Assert.NotEqual(ContentCatalogReadStatus.Supported, result.Status);
            Assert.Equal(raw, result.Original);
            Assert.Null(result.Catalog);
        }
    }

    [Fact]
    public void ContractRoundTripPreservesIdentityTargetArgumentsAndLocalRelationships()
    {
        var item = RealContentFixtures.Item() with { DisplayName = "中文 «文档»",
            Target = RealContentFixtures.Item().Target with { Arguments = "\"--literal=a b\" ", WorkingDirectory = @"Z:\工作" } };
        var json = ContentCatalogCodec.Write(RealContentFixtures.Catalog(item));
        var read = ContentCatalogCodec.Read(json);
        Assert.Equal(ContentCatalogReadStatus.Supported, read.Status);
        Assert.Equal(json, ContentCatalogCodec.Write(read.Catalog!));
        Assert.Equal(item, read.Catalog!.Items[0]);
    }

    [Theory]
    [InlineData("{\"formatVersion\":999,\"unrecognized\":{\"keep\":true}}", ContentCatalogReadStatus.Unsupported)]
    [InlineData("{\"formatVersion\":1,\"futureField\":true}", ContentCatalogReadStatus.Unsupported)]
    [InlineData("{\"formatVersion\":1,\"formatVersion\":1}", ContentCatalogReadStatus.Invalid)]
    [InlineData("null", ContentCatalogReadStatus.Invalid)]
    [InlineData("{}", ContentCatalogReadStatus.Invalid)]
    [InlineData("{\"formatVersion\":\"1\"}", ContentCatalogReadStatus.Invalid)]
    [InlineData("{", ContentCatalogReadStatus.Invalid)]
    public void UnknownOrMalformedInputIsRetainedWithoutWritableCatalog(string raw, ContentCatalogReadStatus expected)
    {
        var read = ContentCatalogCodec.Read(raw);
        Assert.Equal(expected, read.Status);
        Assert.Equal(raw, read.Original);
        Assert.Null(read.Catalog);
    }

    [Fact]
    public void NestedDuplicateUnknownFieldAndInvalidGraphNeverSilentlyDisappear()
    {
        var json = ContentCatalogCodec.Write(RealContentFixtures.Catalog());
        foreach (var raw in new[]
        {
            json.Replace("\"providerId\":\"test\"", "\"providerId\":\"test\",\"providerId\":\"other\""),
            json.Replace("\"providerId\":\"test\"", "\"providerId\":\"test\",\"future\":123"),
            json.Replace("\"capabilities\":", "\"capabilities\":4096,\"discarded\":")
        })
        {
            var read = ContentCatalogCodec.Read(raw);
            Assert.NotEqual(ContentCatalogReadStatus.Supported, read.Status);
            Assert.Null(read.Catalog);
            Assert.Equal(raw, read.Original);
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(8)] [InlineData(12)]
    public void LegacyPreparationPreservesRawLayoutAndNeverSeedsDemoAsRealContent(int sqliteVersion)
    {
        var layout = ContentLayoutPolicy.Serialize(new("A", [new("A:demo", 0)]));
        const string future = "{\"schemaVersion\":999,\"private\":\"retain\"}";
        var preparation = ContentLegacyCompatibility.Inspect(sqliteVersion,
            [new("A", layout), new("B", null), new("C", future)]);
        Assert.Equal(layout, preparation.Posters[0].OriginalLayoutJson);
        Assert.Null(preparation.Posters[1].OriginalLayoutJson);
        Assert.Equal(future, preparation.Posters[2].OriginalLayoutJson);
        Assert.Equal(ContentLegacyDisposition.UnknownLayoutReadOnly, preparation.Posters[2].Disposition);
        Assert.Empty(preparation.EmptyCatalog.Items);
        Assert.Empty(preparation.EmptyCatalog.Collections);
        Assert.Empty(preparation.EmptyCatalog.Memberships);
        Assert.Empty(preparation.EmptyCatalog.Bindings);
        Assert.Empty(preparation.EmptyCatalog.PosterItems);
    }

    [Fact]
    public void UnknownDatabaseVersionDoesNotProduceMigrationCandidate() =>
        Assert.Throws<ArgumentException>(() => ContentLegacyCompatibility.Inspect(13, []));

    [Fact]
    public void MissingListsAndUnknownTypeValuesDoNotCreateDefaults()
    {
        var missing = ContentCatalogCodec.Read("{\"formatVersion\":1}");
        Assert.NotEqual(ContentCatalogReadStatus.Supported, missing.Status);
        var json = ContentCatalogCodec.Write(RealContentFixtures.Catalog());
        Assert.NotEqual(ContentCatalogReadStatus.Supported, ContentCatalogCodec.Read(
            json.Replace("\"kind\":0", "\"kind\":777")).Status);
    }
}
