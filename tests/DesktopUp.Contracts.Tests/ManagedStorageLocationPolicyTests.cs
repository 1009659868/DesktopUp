using DesktopUp.Application;
using DesktopUp.Domain;

namespace DesktopUp.Contracts.Tests;

public sealed class ManagedStorageLocationPolicyTests
{
    private const string Profile = "volume:physical-profile-directory-1";
    private static ManagedStorageRoot Root => ManagedStorageDropContractTests.Root();
    private static ManagedStorageSelection Selection => new(Profile, 0);

    [Fact]
    public void NewProfileProposesDefaultWithoutInventingRegistration() =>
        Assert.Equal(StorageLocationAction.ProposeDefault, ManagedStorageLocationPolicy.Resolve(Profile, Selection, []).Action);

    [Fact]
    public void SoleExistingRootIsKeptRatherThanMovedToDefault()
    {
        var result = ManagedStorageLocationPolicy.Resolve(Profile, Selection, [new(Root, true)]);
        Assert.Equal(StorageLocationAction.SelectSoleLegacyRoot, result.Action); Assert.Equal(Root, result.Root);
    }

    [Fact]
    public void MultipleLegacyRootsNeedAChoiceUnlessExplicitlySelected()
    {
        StorageRootObservation[] roots = [new(Root, true), new(Root with { Id = Guid.NewGuid(), Locator = "E:/other", ObjectIdentity = "other" }, true)];
        Assert.Equal(StorageLocationAction.ChooseLegacyRoot, ManagedStorageLocationPolicy.Resolve(Profile, Selection, roots).Action);
        var selected = ManagedStorageLocationPolicy.Resolve(Profile, Selection with { ActiveRootId = roots[1].Root.Id }, roots);
        Assert.Equal(StorageLocationAction.UseActive, selected.Action); Assert.Equal(roots[1].Root, selected.Root);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("offline")]
    [InlineData("read-only")]
    [InlineData("foreign-owner")]
    [InlineData("future-root")]
    [InlineData("temporary")]
    public void InvalidActiveRootNeverFallsBackToDefaultOrAnotherRoot(string fault)
    {
        var root = fault switch
        {
            "offline" => Root with { Available = false },
            "read-only" => Root with { Writable = false },
            "future-root" => Root with { FormatVersion = 999 },
            "temporary" => Root with { Location = ManagedRootLocation.Temporary },
            _ => Root
        };
        StorageRootObservation[] observations = fault == "missing" ? [] : [new(root, fault != "foreign-owner")];
        var result = ManagedStorageLocationPolicy.Resolve(Profile, Selection with { ActiveRootId = Root.Id }, observations);
        Assert.Equal(StorageLocationAction.Refuse, result.Action); Assert.Null(result.Root);
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("format")]
    [InlineData("version")]
    [InlineData("duplicate")]
    public void InvalidSelectionCannotChooseOrCreate(string fault)
    {
        var selection = fault switch
        {
            "profile" => Selection with { ProfileIdentity = "copied-other-profile" },
            "format" => Selection with { FormatVersion = 2 },
            "version" => Selection with { Version = -1 },
            _ => Selection
        };
        Assert.Equal(StorageLocationAction.Refuse, ManagedStorageLocationPolicy.Resolve(Profile, selection,
            fault == "duplicate" ? [new(Root, true), new(Root, true)] : []).Action);
    }

    [Fact]
    public void DefaultsAreStableAndDailyPreviewProfilesCannotAlias()
    {
        string PathFor(string profile, StorageProfileKind kind) => ManagedStorageLocationPolicy.ProposeDefaultPath(profile, kind, "C:/Users/test/AppData/Local", null, []);
        string daily = PathFor(Profile, StorageProfileKind.Daily);
        Assert.Equal(daily, PathFor(Profile, StorageProfileKind.Daily));
        Assert.NotEqual(daily, PathFor(Profile, StorageProfileKind.Preview));
        Assert.NotEqual(daily, PathFor("physical-profile-directory-2", StorageProfileKind.Daily));
        Assert.Contains("\\DesktopUp\\Storage\\daily\\", daily);
        Assert.Matches("^[0-9a-f]{64}$", Path.GetFileName(daily));
    }

    [Fact]
    public void IsolatedRunRequiresOverrideAndNeverReadsDailyBase()
    {
        Assert.Throws<ArgumentException>(() => ManagedStorageLocationPolicy.ProposeDefaultPath(Profile, StorageProfileKind.Isolated, "C:/daily", null, []));
        var result = ManagedStorageLocationPolicy.ProposeDefaultPath(Profile, StorageProfileKind.Isolated, "not-used", "G:/fixture/storage", []);
        Assert.StartsWith("G:\\fixture\\storage\\isolated\\", result);
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("C:/")]
    [InlineData("C:/test/../escape")]
    [InlineData("C:/test/file:stream")]
    [InlineData("C:/test./folder")]
    [InlineData("//server/share")]
    public void UnsafeLexicalBaseIsNotNormalizedIntoPermission(string path) =>
        Assert.Throws<ArgumentException>(() => ManagedStorageLocationPolicy.ProposeDefaultPath(Profile, StorageProfileKind.Daily, path, null, []));

    [Theory]
    [InlineData("install")]
    [InlineData("state")]
    [InlineData("temp")]
    [InlineData("cache")]
    public void ProtectedPathsCannotBeUsedAsStorage(string category) =>
        Assert.Throws<ArgumentException>(() => ManagedStorageLocationPolicy.ProposeDefaultPath(Profile, StorageProfileKind.Isolated,
            "C:/daily", "G:/protected/" + category, ["G:/protected"]));

    [Fact]
    public void PrefixSiblingIsNotMistakenForProtectedDescendant()
    {
        string path = ManagedStorageLocationPolicy.ProposeDefaultPath(Profile, StorageProfileKind.Isolated, "unused", "G:/state-files", ["G:/state"]);
        Assert.StartsWith("G:\\state-files\\", path);
    }
}
