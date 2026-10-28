using DesktopUp.Infrastructure;

namespace DesktopUp.Contracts.Tests;

public sealed class PortableProfileFileTests
{
    [Fact]
    public void AtomicSaveReadsExactlyAndRequiresOverwriteConsent()
    {
        using var fixture = new Fixture();
        var files = new PortableProfileFile(fixture.Paths);
        files.WriteAtomic(fixture.Target, "{\"version\":1}"u8.ToArray(), false);
        Assert.Equal("{\"version\":1}"u8.ToArray(), files.Read(fixture.Target));
        Assert.Throws<IOException>(() => files.WriteAtomic(fixture.Target, "new"u8.ToArray(), false));
        Assert.Equal("{\"version\":1}"u8.ToArray(), files.Read(fixture.Target));
        files.WriteAtomic(fixture.Target, "new"u8.ToArray(), true);
        Assert.Equal("new"u8.ToArray(), files.Read(fixture.Target));
        Assert.Empty(Directory.EnumerateFiles(fixture.Root, "*.tmp"));
    }

    [Fact]
    public void FailureBeforeReplacePreservesDestinationAndRemovesOwnedTemporary()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.Target, "original");
        var files = new PortableProfileFile(fixture.Paths, _ => throw new IOException("injected"));
        Assert.Throws<IOException>(() => files.WriteAtomic(fixture.Target, "new"u8.ToArray(), true));
        Assert.Equal("original", File.ReadAllText(fixture.Target));
        Assert.Empty(Directory.EnumerateFiles(fixture.Root, "*.tmp"));
    }

    [Fact]
    public void ConcurrentNewDestinationIsNotOverwrittenWithoutConsent()
    {
        using var fixture = new Fixture();
        var files = new PortableProfileFile(fixture.Paths, _ => File.WriteAllText(fixture.Target, "other-writer"));
        Assert.Throws<IOException>(() => files.WriteAtomic(fixture.Target, "new"u8.ToArray(), false));
        Assert.Equal("other-writer", File.ReadAllText(fixture.Target));
    }

    [Fact]
    public void ByteLimitAppliesBeforeReadOrWrite()
    {
        using var fixture = new Fixture();
        File.WriteAllBytes(fixture.Target, new byte[21]);
        var files = new PortableProfileFile(fixture.Paths);
        Assert.Throws<InvalidDataException>(() => files.Read(fixture.Target, 20));
        Assert.Throws<ArgumentOutOfRangeException>(() => files.Read(fixture.Target, 0));
        Assert.Throws<InvalidDataException>(() => files.WriteAtomic(fixture.Target,
            new byte[PortableProfileFile.DefaultMaxBytes + 1], true));
        Assert.Equal(21, new FileInfo(fixture.Target).Length);
    }

    [Theory]
    [InlineData("state")]
    [InlineData("backups")]
    [InlineData("logs")]
    [InlineData("temp")]
    public void ApplicationOwnedLocationsCannotBeReadOrOverwritten(string area)
    {
        using var fixture = new Fixture();
        string folder = area switch
        {
            "state" => fixture.Paths.State, "backups" => fixture.Paths.Backups,
            "logs" => fixture.Paths.Logs, _ => fixture.Paths.Temp
        };
        string file = Path.Combine(folder, "portable.json");
        File.WriteAllText(file, "keep");
        var files = new PortableProfileFile(fixture.Paths);
        Assert.Throws<InvalidDataException>(() => files.Read(file));
        Assert.Throws<InvalidDataException>(() => files.WriteAtomic(file, "new"u8.ToArray(), true));
        Assert.Equal("keep", File.ReadAllText(file));
    }

    [Theory]
    [InlineData("relative.json")]
    [InlineData(@"\\server\share\profile.json")]
    [InlineData(@"\\?\C:\profile.json")]
    [InlineData(@"C:\profile.json:stream")]
    [InlineData(@"C:\CON.json")]
    [InlineData(@"C:\folder.\profile.json")]
    public void NonLocalOrAmbiguousPathsAreRejected(string path)
    {
        using var fixture = new Fixture();
        var files = new PortableProfileFile(fixture.Paths);
        Assert.Throws<InvalidDataException>(() => files.Read(path));
    }

    [Theory]
    [InlineData("a.sqlite3")]
    [InlineData("a.sqlite")]
    [InlineData("a.db")]
    [InlineData("a.sqlite3-wal")]
    [InlineData("a.sqlite3-shm")]
    public void SQLiteFilesAreNotPortableProfileDestinations(string name)
    {
        using var fixture = new Fixture();
        var files = new PortableProfileFile(fixture.Paths);
        Assert.Throws<InvalidDataException>(() => files.WriteAtomic(Path.Combine(fixture.Root, name), Array.Empty<byte>(), false));
    }

    [Fact]
    public void DirectoryIsNotARegularFile()
    {
        using var fixture = new Fixture();
        Assert.Throws<InvalidDataException>(() => new PortableProfileFile(fixture.Paths).Read(fixture.Root));
    }

    [Fact]
    public void ExportCannotOverwriteProgramConfiguration()
    {
        using var fixture = new Fixture();
        Assert.Throws<InvalidDataException>(() => new PortableProfileFile(fixture.Paths).WriteAtomic(
            Path.Combine(AppContext.BaseDirectory, "config", "export.json"), "{}"u8.ToArray(), true));
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "desktopup-portable-file-" + Guid.NewGuid().ToString("N"));
            Paths = new UserDataPaths(Path.Combine(Root, "profile"));
            Paths.EnsureCreated();
        }
        public string Root { get; }
        public UserDataPaths Paths { get; }
        public string Target => Path.Combine(Root, "export.json");
        public void Dispose() => Directory.Delete(Root, true);
    }
}
