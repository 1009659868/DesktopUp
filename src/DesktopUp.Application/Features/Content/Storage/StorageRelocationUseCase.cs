namespace DesktopUp.Application;

// One owner for stage ordering and normal-failure completion. Startup never invokes Execute.
public sealed class StorageRelocationUseCase(IStorageRelocationExecution execution)
{
    public StorageRelocationResult Execute(StorageRelocationPlan plan, string confirmation,
        CancellationToken cancellation = default)
    {
        // Wrong confirmation cannot cancel another operation's durable intent.
        if (confirmation != plan.Fingerprint) throw new IOException("Confirm the current plan.");
        try
        {
            execution.Prepare(plan, confirmation, cancellation);
            cancellation.ThrowIfCancellationRequested();
            execution.RecordVerifiedTargets(plan.Request.OperationId);
            cancellation.ThrowIfCancellationRequested();
            execution.CommitVerifiedTargets(plan.Request.OperationId);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or
            OperationCanceledException or NotSupportedException or System.Text.Json.JsonException)
        {
            // Process-loss fault fixtures are intentionally not normal failures and bypass this filter.
            if (!execution.HasIntent(plan.Request.OperationId)) throw;
            return execution.FinishObserved(plan.Request.OperationId, () => cancellation.IsCancellationRequested)
                with { Reason = error.Message };
        }
        return execution.FinishObserved(plan.Request.OperationId, () => cancellation.IsCancellationRequested);
    }

    public StorageRelocationResult Reconcile(Guid operation) => execution.FinishObserved(operation);
}
