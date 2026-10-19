namespace DesktopUp.Application;

public sealed record StartupTransferSettlement(int Pending, int Preserved);
public sealed record ContentStartupResult(int Pending, int Preserved, int Relocations, IReadOnlyList<string> ReturnNotices);

// Existing safe settlement operations. None may replay an unfinished Shell/file move.
// Lifecycle/relocation implementations remain separate later R1 slices.
public interface IContentStartupSteps
{
    int SettleStorageLocation();
    StartupTransferSettlement SettleTransfers();
    void EnsureSingleOwnership();
    void ReconcileRenames();
    IReadOnlyList<string> ReconcileReturns();
    IReadOnlyList<string> ReconcileCrossPoster();
    void SettleMappedIngress();
    void ReconcileReleasedExits();
}
