using DesktopUp.Application;
using DesktopUp.Infrastructure;

namespace DesktopUp.Testing;

// Test setup/checkpoint adapter only; product startup composes the use case directly.
internal sealed class ContentRenameFixture(SqliteStateStore store)
{
    internal Action<string>? Checkpoint { get; set; }


    private ContentRenameUseCase UseCase => new(new SqliteContentRenameJournal(store), new LocalContentActionFiles(), Checkpoint);
    public void Rename(string posterId, string itemId, Guid contentId, long version, string name,
        CancellationToken token = default, string? expectedIdentity = null) =>
        UseCase.Rename(posterId, itemId, contentId, version, name, token, expectedIdentity);
    public int Reconcile() => UseCase.Reconcile();
}
