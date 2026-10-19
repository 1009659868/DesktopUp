namespace DesktopUp.Application;

// Startup sequencing belongs to Application, not the window or the database.
public sealed class ContentStartupUseCase(IContentStartupSteps steps, IStructuredLog? log = null)
{
    public ContentStartupResult Run()
    {
        int relocations = steps.SettleStorageLocation();
        var transfers = steps.SettleTransfers();
        steps.EnsureSingleOwnership();
        steps.ReconcileRenames();
        var notices = steps.ReconcileReturns();
        foreach (string notice in notices) log?.Write("warning", "content.return_preserved", new { notice });
        var crossNotices = steps.ReconcileCrossPoster();
        foreach (string notice in crossNotices) log?.Write("warning", "transfer.cross_poster_preserved", new { notice });
        steps.SettleMappedIngress();
        // Only released, originally live moves; never remove an old missing card automatically.
        steps.ReconcileReleasedExits();
        return new(transfers.Pending, transfers.Preserved, relocations, notices);
    }
}
