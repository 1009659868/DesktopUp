using System.Text;
using System.Text.Json;
using DesktopUp.Setup;

namespace DesktopUp.Contracts.Tests;

public sealed class InstallerUpgradeTests
{
    [Fact]
    public void Windows_links_read_and_replace_only_fixture_shortcuts()
    {
        using var fixture = new Fixture();
        var links = new WindowsInstallationLinks();
        File.Delete(fixture.OldLink);
        links.Create(fixture.OldLink, Path.Combine(fixture.Install, "DesktopUp.exe"));
        var transaction = new UpgradeTransaction(fixture.Registry, links, fixture.Locations);
        transaction.Execute(fixture.Source, fixture.Install, true);
        Assert.Equal(Path.Combine(fixture.Install, "DesktopUp.exe"), links.Target(fixture.NewLink));
        Assert.False(File.Exists(fixture.OldLink));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Upgrade_replaces_only_owned_files_and_entries(bool desktop)
    {
        using var fixture = new Fixture();
        fixture.Transaction.Execute(fixture.Source, fixture.Install, desktop);
        Assert.Null(fixture.Registry.Read(Fixture.Old));
        Assert.NotNull(fixture.Registry.Read(Fixture.New));
        Assert.Equal("new program", File.ReadAllText(Path.Combine(fixture.Install, "app/DesktopUp.exe")));
        Assert.Equal("user file", File.ReadAllText(Path.Combine(fixture.Install, "app/unknown.txt")));
        Assert.False(File.Exists(fixture.OldLink));
        Assert.Equal(desktop, File.Exists(fixture.NewLink));
        Assert.Empty(Directory.EnumerateDirectories(fixture.Install, InstallationFiles.WorkPrefix + "*"));
        Assert.Equal("profile stays", File.ReadAllText(Path.Combine(fixture.Root, "profile.txt")));
    }

    [Theory]
    [InlineData("prepared")]
    [InlineData("old:0")]
    [InlineData("new:0")]
    [InlineData("registered-new")]
    [InlineData("removed-old-registration")]
    public void Failure_restores_old_program_and_registration(string at)
    {
        using var fixture = new Fixture();
        var before = fixture.Files();
        var transaction = fixture.Create(point => { if (point == at) throw new IOException("owned test failure"); });
        Assert.Throws<IOException>(() => transaction.Execute(fixture.Source, fixture.Install, true));
        Assert.Equal(before, fixture.Files());
        Assert.NotNull(fixture.Registry.Read(Fixture.Old));
        Assert.Null(fixture.Registry.Read(Fixture.New));
    }

    [Fact]
    public void Interrupted_replacement_can_be_recovered_from_durable_journal()
    {
        using var fixture = new Fixture();
        var original = fixture.Files();
        Dictionary<string, byte[]>? interrupted = null;
        var transaction = fixture.Create(point =>
        {
            if (point != "new:0") return;
            interrupted = Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories)
                .ToDictionary(path => path, File.ReadAllBytes);
            throw new IOException("capture abrupt-stop boundary");
        });
        Assert.Throws<IOException>(() => transaction.Execute(fixture.Source, fixture.Install, true));
        Assert.NotNull(interrupted);
        // Restore only this fixture's captured disk state, as if the helper had stopped.
        foreach (string file in Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories)) File.Delete(file);
        foreach (var pair in interrupted!) { Directory.CreateDirectory(Path.GetDirectoryName(pair.Key)!); File.WriteAllBytes(pair.Key, pair.Value); }
        fixture.Transaction.Recover(fixture.Install);
        Assert.Equal(original, fixture.Files());
        Assert.NotNull(fixture.Registry.Read(Fixture.Old));
    }

    [Theory]
    [InlineData("changed")]
    [InlineData("unknown-collision")]
    [InlineData("registration")]
    [InlineData("new-version-exists")]
    [InlineData("locked")]
    public void Invalid_or_busy_installation_is_not_modified(string condition)
    {
        using var fixture = new Fixture();
        FileStream? lease = null;
        if (condition == "changed") File.AppendAllText(Path.Combine(fixture.Install, "app/DesktopUp.exe"), "user change");
        if (condition == "unknown-collision") File.WriteAllText(Path.Combine(fixture.Install, "app/new-only.txt"), "mine");
        if (condition == "registration") fixture.Registry.Remove(Fixture.Old);
        if (condition == "new-version-exists") fixture.Registry.Write(Fixture.New, InstallationMetadata.Registration(fixture.Source, Fixture.New, 0));
        var before = fixture.Files();
        if (condition == "locked") lease = new FileStream(Path.Combine(fixture.Install, "app/DesktopUp.exe"), FileMode.Open, FileAccess.Read, FileShare.Read);
        try { Assert.ThrowsAny<IOException>(() => fixture.Transaction.Execute(fixture.Source, fixture.Install, true)); }
        finally { lease?.Dispose(); }
        Assert.Equal(before, fixture.Files());
    }

    [Theory]
    [InlineData("../profile.txt")]
    [InlineData("app/../../profile.txt")]
    [InlineData("app/a:stream")]
    [InlineData("app/a.")]
    public void Manifest_cannot_escape_program_namespace(string path)
    {
        using var fixture = new Fixture();
        Assert.Throws<IOException>(() => InstallationFiles.PayloadPath(fixture.Install, path));
    }

    private sealed class Fixture : IDisposable
    {
        public const string Old = "20990101.1", New = "20990101.2";
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "DesktopUp-upgrade-" + Guid.NewGuid().ToString("N"));
        public string Install => Path.Combine(Root, "installed");
        public string Source => Path.Combine(Root, "new-package");
        public MemoryRegistry Registry { get; } = new();
        public InstallationLocations Locations => new(Path.Combine(Root, "desktop"), Path.Combine(Root, "programs"));
        public string OldLink => Path.Combine(Locations.Desktop, "DesktopUp Preview " + Old + ".lnk");
        public string NewLink => Path.Combine(Locations.Desktop, "DesktopUp Preview " + New + ".lnk");
        public UpgradeTransaction Transaction => Create(null);
        public UpgradeTransaction Create(Action<string>? action) => new(Registry, new TextLinks(), Locations) { Checkpoint = action };
        public Fixture()
        {
            Build(Install, Old, "old program"); Build(Source, New, "new program");
            File.WriteAllText(Path.Combine(Install, "app/preview-install.ini"), $"[Install]\r\nChannel=DesktopUp.Preview\r\nRelease={Old}\r\nPath={Install}\r\nDesktopShortcutCreated=1\r\n", Encoding.Unicode);
            File.WriteAllText(Path.Combine(Install, "app/unknown.txt"), "user file");
            File.WriteAllText(Path.Combine(Root, "profile.txt"), "profile stays");
            Directory.CreateDirectory(Locations.Desktop); Directory.CreateDirectory(Locations.Programs);
            new TextLinks().Create(OldLink, Path.Combine(Install, "DesktopUp.exe"));
            Registry.Write(Old, InstallationMetadata.Registration(Install, Old, 0));
        }
        private static void Build(string root, string version, string text)
        {
            Directory.CreateDirectory(Path.Combine(root, "app"));
            File.WriteAllText(Path.Combine(root, "DesktopUp.exe"), text);
            File.WriteAllText(Path.Combine(root, "Uninstall.exe"), text + " uninstall");
            File.WriteAllText(Path.Combine(root, "app/DesktopUp.exe"), text);
            File.WriteAllText(Path.Combine(root, "PREVIEW-README.txt"), text);
            File.WriteAllText(Path.Combine(root, "app/preview-build.json"), JsonSerializer.Serialize(new { Release = version, Channel = "Preview" }));
            if (version == New) File.WriteAllText(Path.Combine(root, "app/new-only.txt"), "new resource");
            var entries = Directory.GetFiles(root, "*", SearchOption.AllDirectories).Where(p => Path.GetFileName(p) != "Uninstall.exe")
                .Select(p => new PayloadFile(Path.GetRelativePath(root, p).Replace('\\', '/'), new FileInfo(p).Length, InstallationFiles.Digest(p))).ToList();
            File.WriteAllText(Path.Combine(root, "app/payload-manifest.json"), JsonSerializer.Serialize(entries));
        }
        public string[] Files() => Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(Root, path) + "|" + InstallationFiles.Digest(path)).Order(StringComparer.Ordinal).ToArray();
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
    }
    private sealed class MemoryRegistry : IInstallRegistry
    {
        private readonly Dictionary<string, List<RegistryValue>> entries = [];
        public List<RegistryValue>? Read(string release) => entries.GetValueOrDefault(release);
        public void Write(string release, List<RegistryValue> values) => entries[release] = values;
        public void Remove(string release) => entries.Remove(release);
    }
    private sealed class TextLinks : IInstallationLinks
    {
        public string? Target(string path) => File.Exists(path) ? File.ReadAllText(path) : null;
        public void Create(string path, string target) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, target); }
    }
}
