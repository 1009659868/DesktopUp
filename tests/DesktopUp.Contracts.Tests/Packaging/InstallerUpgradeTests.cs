using System.Text;
using System.Text.Json;
using DesktopUp.Setup;

namespace DesktopUp.Contracts.Tests;

public sealed class InstallerUpgradeTests
{
    [Fact]
    public void Partial_fixed_registration_write_restores_the_exact_snapshot()
    {
        using var f = new Fixture();
        var original = f.Registry.Read(Fixture.Old)!;
        f.Registry.Remove(Fixture.Old); f.Registry.Write(WindowsInstallRegistry.StableId, original);
        var files = f.Files();
        f.Registry.FailNextWrite = true;
        Assert.Throws<IOException>(() => f.Transaction.Execute(f.Source, Path.Combine(f.Root, "partial-registry"), true));
        Assert.Equal(files, f.Files());
        Assert.True(InstallationMetadata.Equal(original, f.Registry.Read(WindowsInstallRegistry.StableId)));
        Assert.False(File.Exists(f.Locations.Pending));
    }
    [Fact]
    public void Actual_cross_volume_program_migration_keeps_old_unknown_data()
    {
        using var f = new Fixture();
        string repo = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(repo, "Directory.Build.props"))) repo = Path.GetDirectoryName(repo) ?? throw new IOException("repository missing");
        string target = Path.Combine(repo, "out", "single-install", "volume-" + Guid.NewGuid().ToString("N"));
        Assert.NotEqual(Path.GetPathRoot(f.Install), Path.GetPathRoot(target));
        try
        {
            f.Transaction.Execute(f.Source, target, true);
            Assert.Equal(target, InstallationIdentity.Discover(f.Registry)!.Root);
            Assert.False(File.Exists(Path.Combine(f.Install, "DesktopUp.exe")));
            Assert.Equal("user file", File.ReadAllText(Path.Combine(f.Install, "app/unknown.txt")));
            Assert.Equal("new program", File.ReadAllText(Path.Combine(target, "DesktopUp.exe")));
        }
        finally { if (Directory.Exists(target)) Directory.Delete(target, recursive: true); }
    }

    [Fact]
    public void Committed_migration_is_finished_not_rolled_back_after_interruption()
    {
        using var f = new Fixture();
        string target = Path.Combine(f.Root, "relocated");
        Assert.Throws<PendingUpgradeException>(() => f.Create(point => { if (point == "committed") throw new IOException("stop"); })
            .Execute(f.Source, target, true));
        Assert.True(File.Exists(f.Locations.Pending));
        Assert.False(File.Exists(Path.Combine(f.Install, "DesktopUp.exe")));
        f.Transaction.Recover(target);
        Assert.False(File.Exists(f.Locations.Pending));
        Assert.Equal(target, InstallationIdentity.Discover(f.Registry)!.Root);
        Assert.Empty(Directory.EnumerateDirectories(target, InstallationFiles.WorkPrefix + "*"));
    }

    [Fact]
    public void External_startup_change_is_not_overwritten_during_preparation()
    {
        using var f = new Fixture();
        var before = f.Files();
        Assert.Throws<IOException>(() => f.Create(point => { if (point == "prepared") f.Startup.Value = "external owner"; })
            .Execute(f.Source, Path.Combine(f.Root, "target"), true));
        Assert.Equal(before, f.Files());
        Assert.Equal("external owner", f.Startup.Value);
        Assert.Equal(new[] { Fixture.Old }, f.Registry.Ids());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Relocation_preserves_content_and_migrates_only_owned_startup(bool enabled)
    {
        using var f = new Fixture();
        string target = Path.Combine(f.Root, "new location 中文");
        f.Startup.Value = enabled ? $"\"{f.Install}\\DesktopUp.exe\" --autostart" : null;
        f.Transaction.Execute(f.Source, target, true);
        Assert.Equal(new[] { WindowsInstallRegistry.StableId }, f.Registry.Ids());
        Assert.Equal(target, InstallationIdentity.Discover(f.Registry)!.Root);
        Assert.False(File.Exists(Path.Combine(f.Install, "DesktopUp.exe")));
        Assert.False(File.Exists(Path.Combine(f.Install, "Uninstall.exe")));
        Assert.Equal("user file", File.ReadAllText(Path.Combine(f.Install, "app/unknown.txt")));
        Assert.Equal("profile stays", File.ReadAllText(Path.Combine(f.Root, "profile.txt")));
        Assert.Equal(enabled ? $"\"{target}\\DesktopUp.exe\" --autostart" : null, f.Startup.Value);
        Assert.False(File.Exists(f.Locations.Pending));
        Assert.Equal(Path.Combine(target, "DesktopUp.exe"), File.ReadAllText(f.NewLink));
        // Same-version relocation still uses the one fixed registration.
        string next = Path.Combine(f.Root, "moved-again");
        f.Transaction.Execute(f.Source, next, false);
        Assert.Equal(next, InstallationIdentity.Discover(f.Registry)!.Root);
        Assert.False(File.Exists(Path.Combine(target, "DesktopUp.exe")));
        Assert.Throws<IOException>(() => f.Transaction.Execute(f.Source, next, false));
    }

    [Theory]
    [InlineData("prepared")]
    [InlineData("old:0")]
    [InlineData("new:0")]
    [InlineData("startup-updated")]
    [InlineData("registered-new")]
    [InlineData("removed-old-registration")]
    public void Relocation_failure_rolls_back_files_identity_and_startup(string at)
    {
        using var f = new Fixture();
        f.Startup.Value = $"\"{f.Install}\\DesktopUp.exe\" --autostart";
        string initialStartup = f.Startup.Value;
        var initialFiles = f.Files();
        var t = f.Create(point => { if (point == at) throw new IOException("controlled failure"); });
        Assert.Throws<IOException>(() => t.Execute(f.Source, Path.Combine(f.Root, "relocated"), true));
        Assert.Equal(initialFiles, f.Files());
        Assert.Equal(initialStartup, f.Startup.Value);
        Assert.Equal(new[] { Fixture.Old }, f.Registry.Ids());
    }

    [Fact]
    public void Fresh_install_and_fixed_identity_upgrade_share_the_transaction()
    {
        using var f = new Fixture();
        f.Registry.Remove(Fixture.Old);
        // Remove only fixture-owned links; the unregistered old files must not be touched.
        File.Delete(f.OldLink);
        string target = Path.Combine(f.Root, "fresh");
        f.Transaction.Execute(f.Source, target, false);
        Assert.Equal(new[] { WindowsInstallRegistry.StableId }, f.Registry.Ids());
        Assert.True(File.Exists(Path.Combine(f.Install, "DesktopUp.exe")));
        Assert.Throws<IOException>(() => f.Transaction.Execute(f.Source, target, false));
        Assert.Throws<IOException>(() => f.Transaction.Execute(f.Install, target, false)); // downgrade
    }

    [Fact]
    public void Fixed_registration_failure_restores_original_key_not_a_legacy_key()
    {
        using var f = new Fixture();
        var old = f.Registry.Read(Fixture.Old)!;
        f.Registry.Remove(Fixture.Old); f.Registry.Write(WindowsInstallRegistry.StableId, old);
        var files = f.Files();
        Assert.Throws<IOException>(() => f.Create(point => { if (point == "registered-new") throw new IOException("test"); })
            .Execute(f.Source, f.Install, true));
        Assert.Equal(files, f.Files());
        Assert.True(InstallationMetadata.Equal(old, f.Registry.Read(WindowsInstallRegistry.StableId)));
        Assert.Equal(new[] { WindowsInstallRegistry.StableId }, f.Registry.Ids());
    }

    [Fact]
    public void Multiple_registrations_and_nested_destinations_are_rejected_without_mutation()
    {
        using var f = new Fixture();
        var before = f.Files();
        Assert.Throws<IOException>(() => f.Transaction.Execute(f.Source, Path.Combine(f.Install, "nested"), true));
        f.Registry.Write(WindowsInstallRegistry.StableId, f.Registry.Read(Fixture.Old)!);
        Assert.Throws<IOException>(() => f.Transaction.Execute(f.Source, Path.Combine(f.Root, "another"), true));
        Assert.Equal(before, f.Files());
    }

    [Fact]
    public void Windows_links_read_and_replace_only_fixture_shortcuts()
    {
        using var fixture = new Fixture();
        var links = new WindowsInstallationLinks();
        File.Delete(fixture.OldLink);
        links.Create(fixture.OldLink, Path.Combine(fixture.Install, "DesktopUp.exe"));
        var transaction = new UpgradeTransaction(fixture.Registry, links, fixture.Locations, fixture.Startup);
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
        Assert.NotNull(fixture.Registry.Read(WindowsInstallRegistry.StableId));
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
        Assert.Null(fixture.Registry.Read(WindowsInstallRegistry.StableId));
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
        public InstallationLocations Locations => new(Path.Combine(Root, "desktop"), Path.Combine(Root, "programs"), Path.Combine(Root, "state"));
        public MemoryStartup Startup { get; } = new();
        public string OldLink => Path.Combine(Locations.Desktop, "DesktopUp Preview " + Old + ".lnk");
        public string NewLink => Path.Combine(Locations.Desktop, "DesktopUp Preview " + New + ".lnk");
        public UpgradeTransaction Transaction => Create(null);
        public UpgradeTransaction Create(Action<string>? action) => new(Registry, new TextLinks(), Locations, Startup) { Checkpoint = action };
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
        public bool FailNextWrite { get; set; }
        public IReadOnlyList<string> Ids() => entries.Keys.Order(StringComparer.Ordinal).ToArray();
        public List<RegistryValue>? Read(string release) => entries.GetValueOrDefault(release);
        public void Write(string release, List<RegistryValue> values)
        {
            if (FailNextWrite)
            {
                FailNextWrite = false;
                var merged = (entries.GetValueOrDefault(release) ?? []).ToDictionary(v => v.Name);
                foreach (var value in values.Take(2)) merged[value.Name] = value;
                entries[release] = merged.Values.OrderBy(v => v.Name, StringComparer.Ordinal).ToList();
                throw new IOException("controlled partial registry write");
            }
            entries[release] = values;
        }
        public void Remove(string release) => entries.Remove(release);
    }
    private sealed class MemoryStartup : IInstallationStartup
    {
        public string? Value { get; set; }
        public string? Read() => Value;
        public void Replace(string? expected, string? next)
        {
            if (Value != expected) throw new IOException("changed startup");
            Value = next;
        }
    }
    private sealed class TextLinks : IInstallationLinks
    {
        public string? Target(string path) => File.Exists(path) ? File.ReadAllText(path) : null;
        public void Create(string path, string target) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, target); }
    }
}
