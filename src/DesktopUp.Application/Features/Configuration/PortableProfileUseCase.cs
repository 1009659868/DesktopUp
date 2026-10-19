using System.Security.Cryptography;

namespace DesktopUp.Application;

public sealed record PortableImportTicket(string RequestId, PortableImportPlan Plan, string? SourcePath, string? SourceHash);
public sealed record PortableApplyResult(bool Succeeded, string Message, StateCommandResult? Command = null);

public sealed class PortableProfileUseCase(
    IStateCommandRepository repository, IStateCommandService commands, IStateStore store,
    IPortableProfileFiles files, Func<DisplayTopologySnapshot?> topology, Func<PortableImportPlan, bool> guard)
{
    public byte[] Export() => PortableProfileCodec.Export(repository.Read());
    public void Save(string path, byte[] bytes, bool overwrite) => files.WriteAtomic(path, bytes, overwrite);
    public PortableImportTicket Preview(string path, PortableImportMode mode, bool importSettings)
    {
        byte[] bytes = files.Read(path);
        var document = PortableProfileCodec.Parse(bytes);
        var environment = topology() ?? throw new InvalidOperationException("桌面尚未稳定或正在拖动/收展，请稍后重试。");
        var plan = PortableProfilePlanner.Plan(document, repository.Read(), environment, mode, importSettings);
        return new("portable-import-" + Guid.NewGuid().ToString("N"), plan, path, Convert.ToHexString(SHA256.HashData(bytes)));
    }
    public PortableImportTicket PreviewPending()
    {
        var environment = topology() ?? throw new InvalidOperationException("桌面尚未稳定或正在交互。");
        return new("portable-place-" + Guid.NewGuid().ToString("N"),
            PortableProfilePlanner.TryPlacePending(repository.Read(), environment), null, null);
    }
    public PortableApplyResult Apply(PortableImportTicket ticket)
    {
        if (ticket.SourcePath is { } path && Convert.ToHexString(SHA256.HashData(files.Read(path))) != ticket.SourceHash)
            return new(false, "原配置文件在预览后发生变化；未应用，请重新选择。");
        if (!guard(ticket.Plan)) return new(false, "配置、显示环境或实际占位已变化；未应用，请重新预览。");
        var backup = store.CreateBackup(ticket.Plan.IsRetry ? "pre-place-pending" : "pre-portable-import");
        if (!backup.Succeeded || backup.Revision != ticket.Plan.BasisRevision)
            return new(false, "备份失败或预览已过期；未应用。 " + backup.Error);
        if (!guard(ticket.Plan)) return new(false, "备份后环境已变化；未应用，原备份已保留。");
        var result = commands.ExecutePortable(new(ticket.RequestId, ticket.Plan));
        return new(result.Succeeded, result.Succeeded
            ? $"已应用。等待安置 {ticket.Plan.PendingPosterIds.Count} 张；原状态备份：{backup.FileName}。"
            : "未应用：" + result.Error, result);
    }
    public StateBackupResult Backup() => store.CreateBackup("control-center");
    public IReadOnlyList<string> PendingNames() => repository.Read().Catalog.Posters
        .Where(p => p.Poster.PlacementPending).Select(p => p.Poster.Title).ToArray();
}
