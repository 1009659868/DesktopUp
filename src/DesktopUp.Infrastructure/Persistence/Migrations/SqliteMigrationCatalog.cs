using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace DesktopUp.Infrastructure;

// SQL identity is an embedded, byte-checked resource; C# checkout/formatting never defines it.
internal static class SqliteMigrationCatalog
{
    private const string Prefix = "DesktopUp.Migrations.";

    internal static SqliteMigration[] Load(int expectedVersion)
    {
        using var manifest = Open("catalog.xml");
        XElement root = XDocument.Load(manifest).Root
            ?? throw new InvalidDataException("Missing migration catalog.");
        var result = new List<SqliteMigration>();
        foreach (XElement entry in root.Elements("migration"))
        {
            int version = (int?)entry.Attribute("version") ?? 0;
            string name = (string?)entry.Attribute("name") ?? "";
            string file = (string?)entry.Attribute("file") ?? "";
            if (version != result.Count + 1 || file != $"{version:000}.sql" || name.Length == 0)
                throw new InvalidDataException("Invalid migration catalog identity/order.");
            using var stream = Open(file);
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            string[] legacy = entry.Elements("legacy").Select(item =>
                (string?)item.Attribute("sha256") ?? "").ToArray();
            result.Add(SqliteMigration.FromBytes(version, name, bytes.ToArray(),
                (string?)entry.Attribute("sha256") ?? "", legacy));
        }
        if (root.Name != "migrations" || result.Count != expectedVersion)
            throw new InvalidDataException("Migration catalog version mismatch.");
        return result.ToArray();
    }

    private static Stream Open(string file) =>
        typeof(SqliteMigrationCatalog).Assembly.GetManifestResourceStream(Prefix + file)
        ?? throw new InvalidDataException($"Missing migration resource: {file}.");
}

internal sealed class SqliteMigration
{
    private SqliteMigration(int version, string name, string sql, string checksum, string[] legacy)
    {
        Version = version;
        Name = name;
        Sql = sql;
        Checksum = checksum;
        LegacyChecksums = Array.AsReadOnly(legacy);
    }

    public int Version { get; }
    public string Name { get; }
    public string Sql { get; }
    public string Checksum { get; }
    internal ReadOnlyCollection<string> LegacyChecksums { get; }

    internal bool MatchesChecksum(string checksum) =>
        checksum == Checksum || LegacyChecksums.Contains(checksum, StringComparer.Ordinal);

    internal static SqliteMigration FromBytes(
        int version, string name, byte[] bytes, string checksum, string[] legacy)
    {
        // No trimming, BOM removal, newline folding or replacement decoding at runtime.
        string sql = new UTF8Encoding(false, true).GetString(bytes);
        if (sql.Length == 0 || sql[0] == '\uFEFF' || sql.Contains('\r') || sql.EndsWith('\n') ||
            Convert.ToHexString(SHA256.HashData(bytes)) != checksum ||
            legacy.Any(hash => hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigitUpper(c))))
            throw new InvalidDataException($"Frozen migration bytes/checksum mismatch at version {version}.");
        return new SqliteMigration(version, name, sql, checksum, (string[])legacy.Clone());
    }
}
