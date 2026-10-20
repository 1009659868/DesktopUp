using DesktopUp.Application;
using DesktopUp.Infrastructure;

namespace DesktopUp.Testing;

// Compatibility for existing fixtures; not a second implementation.
internal sealed class ContentCreationFixture(SqliteStateStore store)
{
    internal Action<string, string>? Checkpoint { get; set; }
    public static string SuggestName(string directory, NewContentKind kind) =>
        ContentCreationUseCase.SuggestName(new LocalContentActionFiles(), directory, kind);
    public string Create(string posterId, NewContentLocation location, NewContentKind kind, string name,
        string? shortcutTarget = null, CancellationToken token = default) =>
        new ContentCreationUseCase(new SqliteContentCreationStorage(store), new LocalContentActionFiles(),
            new MappedContentCreationUseCase(new SqliteMappedDirectoryStorage(store),
                new LocalContentObservationFiles(), new LocalOwnedContentCreationFiles()), Checkpoint)
            .Create(posterId, location, kind, name, shortcutTarget, token);
}
