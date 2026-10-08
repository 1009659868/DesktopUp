using System.Security.Cryptography;
using DesktopUp.Application;
using DesktopUp.Infrastructure;

namespace DesktopUp;

internal sealed record PortableImportTicket(string RequestId, PortableImportPlan Plan, string? SourcePath, string? SourceHash);
internal sealed record PortableApplyResult(bool Succeeded, string Message, StateCommandResult? Command = null);

internal sealed class PortableProfileController(
    IStateCommandRepository repository, StateProjectionCoordinator coordinator, IStateStore store,
    IUserDataPaths paths, Func<DisplayTopologySnapshot?> topology, Func<PortableImportPlan, bool> guard,
    Action requestRecovery)
{
    private readonly PortableProfileFile _files = new(paths);
    public byte[] Export() => PortableProfileCodec.Export(repository.Read());
    public void Save(string path, byte[] bytes, bool overwrite) => _files.WriteAtomic(path, bytes, overwrite);
    public PortableImportTicket Preview(string path, PortableImportMode mode, bool importSettings)
    {
        byte[] bytes = _files.Read(path);
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
        if (ticket.SourcePath is { } path && Convert.ToHexString(SHA256.HashData(_files.Read(path))) != ticket.SourceHash)
            return new(false, "原配置文件在预览后发生变化；未应用，请重新选择。");
        if (!guard(ticket.Plan)) return new(false, "配置、显示环境或实际占位已变化；未应用，请重新预览。");
        var backup = store.CreateBackup(ticket.Plan.IsRetry ? "pre-place-pending" : "pre-portable-import");
        if (!backup.Succeeded || backup.Revision != ticket.Plan.BasisRevision)
            return new(false, "备份失败或预览已过期；未应用。 " + backup.Error);
        if (!guard(ticket.Plan)) return new(false, "备份后环境已变化；未应用，原备份已保留。");
        var result = coordinator.ExecutePortable(new(ticket.RequestId, ticket.Plan));
        return new(result.Succeeded, result.Succeeded
            ? $"已应用。等待安置 {ticket.Plan.PendingPosterIds.Count} 张；原状态备份：{backup.FileName}。"
            : "未应用：" + result.Error, result);
    }
    public StateBackupResult Backup() => store.CreateBackup("control-center");
    public IReadOnlyList<string> PendingNames() => repository.Read().Catalog.Posters
        .Where(p => p.Poster.PlacementPending).Select(p => p.Poster.Title).ToArray();
    public void RequestRecovery() => requestRecovery();

    public static string Describe(PortableImportPlan plan)
    {
        var all = plan.Creates.Concat(plan.Updates).ToDictionary(p => p.PosterId);
        var pending = plan.PendingPosterIds.Select(id => all.TryGetValue(id, out var p) ? p.Title : id);
        return (plan.IsRetry ? $"可安置 {plan.Updates.Count} 张。" :
            $"{(plan.Mode == PortableImportMode.Append ? "追加" : "替换")}：导入 {plan.Creates.Count} 张，移除本机 {plan.Removes.Count} 张海报配置。\n" +
            $"全局偏好：{(plan.ImportSettings ? "应用文件中的中心主题/动效/性能偏好（旧包未包含的设置保持本机）" : "保持本机")}。模板册及海报专属参数独立导入；替换海报不会清空本机模板册。不会移动或删除真实文件。") +
            $"\n等待安置 {plan.PendingPosterIds.Count} 张（保留原尺寸与状态，暂不显示）：\n" +
            string.Join("\n", pending.Select(n => "• " + n)) + "\n" + string.Join("\n", plan.Warnings.Distinct());
    }
}
