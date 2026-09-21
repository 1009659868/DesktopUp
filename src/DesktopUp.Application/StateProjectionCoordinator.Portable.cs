using DesktopUp.Domain;

namespace DesktopUp.Application;

public sealed record PortableImportCommand(string RequestId, PortableImportPlan Plan);

public sealed partial class StateProjectionCoordinator
{
    private Func<PortableImportPlan, bool>? _portableRuntimeGuard;

    public void BindPortableRuntime(Func<PortableImportPlan, bool> guard)
    {
        ArgumentNullException.ThrowIfNull(guard);
        lock (_sync)
        {
            if (_portableRuntimeGuard is not null)
                throw new InvalidOperationException("Portable runtime is already bound.");
            _portableRuntimeGuard = guard;
        }
    }

    public StateCommandResult ExecutePortable(PortableImportCommand command) => Run(
        command.RequestId, StateCommandSource.ControlCenter, command.Plan.BasisRevision, command.Plan.ReadSet,
        reads => command with { Plan = FreezePortable(command.Plan) with { ReadSet = Required(reads) } },
        (frozen, before) =>
        {
            var plan = frozen.Plan;
            if (_portableRuntimeGuard?.Invoke(plan) != true)
                return StateMutationPlan.Reject(StateCommandStatus.Conflict, "显示环境或空间交互已变化，请重新预览。");
            if (plan.BasisRevision != before.Revision || !ControlCenterSpatial.CompleteReads(plan.ReadSet, before) ||
                plan.ReadSet.SettingsVersion != before.Settings.EntityVersion)
                return StateMutationPlan.Reject(StateCommandStatus.Conflict, "预览后配置已变化，请重新预览。");
            PortableImportPlan verified;
            if (plan.IsRetry)
                verified = PortableProfilePlanner.TryPlacePending(before, plan.InitialTopology);
            else
            {
                var identities = new Queue<string>(plan.Creates.Select(p =>
                    p.PosterId.StartsWith("poster-", StringComparison.Ordinal) ? p.PosterId[7..] : throw new ArgumentException("Invalid imported identity.")));
                verified = PortableProfilePlanner.Plan(plan.Document!, before, plan.InitialTopology,
                    plan.Mode, plan.ImportSettings, () => identities.Dequeue());
                if (identities.Count != 0) throw new ArgumentException("Unused imported identities.");
            }
            if (!verified.Creates.SequenceEqual(plan.Creates) || !verified.Updates.SequenceEqual(plan.Updates) ||
                !verified.Removes.SequenceEqual(plan.Removes) || verified.Settings != plan.Settings ||
                !verified.PendingPosterIds.SequenceEqual(plan.PendingPosterIds))
                return StateMutationPlan.Reject(StateCommandStatus.ValidationFailed, "导入提案与确定性校验不一致。");
            return new StateMutationPlan
            {
                Creates = plan.Creates, Updates = plan.Updates, Removes = plan.Removes, Settings = plan.Settings,
                TouchLayoutIds = plan.Updates.Select(p => p.PosterId).ToArray()
            };
        });

    private static PortableImportPlan FreezePortable(PortableImportPlan plan) => plan with
    {
        Document = plan.Document is { } document ? PortableProfileCodec.Freeze(document) : null,
        InitialTopology = plan.InitialTopology with { Monitors = Array.AsReadOnly(plan.InitialTopology.Monitors.ToArray()) },
        ReadSet = plan.ReadSet.Freeze(),
        Creates = Array.AsReadOnly(plan.Creates.ToArray()), Updates = Array.AsReadOnly(plan.Updates.ToArray()),
        Removes = Array.AsReadOnly(plan.Removes.ToArray()),
        PendingPosterIds = Array.AsReadOnly(plan.PendingPosterIds.ToArray()), Warnings = Array.AsReadOnly(plan.Warnings.ToArray())
    };
}
