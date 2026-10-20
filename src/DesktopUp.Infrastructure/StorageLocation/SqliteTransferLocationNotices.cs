using DesktopUp.Application;

namespace DesktopUp.Infrastructure;

// A read-only projection, not a recovery workflow or a second journal.
public sealed class SqliteTransferLocationNotices(SqliteStateStore store) : ITransferLocationNotices
{
    public IReadOnlyList<TransferLocationNotice> Read() => new SqliteTransferJournal(store).Read()
        .Where(x => x.Phase == DurableTransferPhase.Review &&
            x.Error?.StartsWith("自动收尾：原文件未移动；", StringComparison.Ordinal) != true)
        .TakeLast(30).Select(x => new TransferLocationNotice(x.File.Source, x.File.Destination)).ToArray();
}
