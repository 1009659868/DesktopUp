using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

// Compatibility for existing fixtures; not a second implementation.
public sealed class ContentCreationService(SqliteStateStore store)
{
    internal Action<string, string>? Checkpoint { get; set; }
    public static string SuggestName(string directory, NewContentKind kind) =>
        ContentCreationUseCase.SuggestName(new LocalContentActionFiles(), directory, kind);
    public string Create(string posterId, NewContentLocation location, NewContentKind kind, string name,
        string? shortcutTarget = null, CancellationToken token = default) =>
        new ContentCreationUseCase(new SqliteContentCreationStorage(store), new LocalContentActionFiles(), Checkpoint)
            .Create(posterId, location, kind, name, shortcutTarget, token);
    internal static void RemoveExactEmptyOrFile(string path, LocalTransferObject expected)
    {
        try
        {
            if (!LocalTransferFiles.Exists(path)) return;
            using var pin = new LocalTransferFiles.PinnedObject(path, delete: true);
            if (pin.Snapshot() != expected || expected.Directory && Directory.EnumerateFileSystemEntries(path).Any()) return;
            pin.Delete();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* Preserve unknown ownership/bytes. */ }
    }
}
