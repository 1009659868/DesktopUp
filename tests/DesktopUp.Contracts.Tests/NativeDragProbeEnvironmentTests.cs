using DesktopUp.NativeDragProbe;

namespace DesktopUp.Contracts.Tests;

public sealed class NativeDragProbeEnvironmentTests
{
    private static readonly ProbeProcessToken Medium = new(101, "S-1-5-21-1-2-3-1001", 1, 0x2000, false, 3, false);
    private static ProbeProcessToken Shell => Medium with { ProcessId = 202 };

    [Fact]
    public void ReadOnlyNativeSelfTokenReadsFixedAndVariableLengthClasses()
    {
        var token = ProbeEnvironment.ReadToken(Environment.ProcessId);
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        Assert.Equal(identity.User!.Value, token.UserSid);
        Assert.Equal(process.SessionId, token.SessionId);
        Assert.Equal(Environment.ProcessId, token.ProcessId);
        Assert.InRange(token.ElevationType, 1, 3);
        Assert.True(token.IntegrityRid > 0);
    }

    [Fact]
    public void MediumMatchAdmitsProbeWithoutClaimingNativePass()
    {
        var result = ProbeEnvironment.Evaluate(Medium, Shell);
        Assert.True(result.Allowed); Assert.Equal("MediumIntegrityMatched", result.Kind);
        Assert.Equal("SameUserSessionIntegrity", result.Reason);
    }

    [Theory]
    [InlineData(1)] // UAC disabled: full token with no linked token, not an elevation action.
    [InlineData(2)]
    public void HighMatchIsLocalOnlyAndDoesNotMasqueradeAsMedium(int elevationType)
    {
        var probe = Medium with { IntegrityRid = 0x3000, Elevated = true, ElevationType = elevationType };
        var result = ProbeEnvironment.Evaluate(probe, probe with { ProcessId = 202 });
        Assert.True(result.Allowed); Assert.Equal("HighIntegrityMatched", result.Kind);
        Assert.Equal("LocalProbeOnly_NotMediumPrivilegeEvidence", result.Reason);
        Assert.True(result.Probe!.Elevated); Assert.Equal(elevationType, result.Explorer!.ElevationType);
    }

    [Theory]
    [InlineData(0x2000, 0x3000)]
    [InlineData(0x3000, 0x2000)]
    public void BothDirectionsOfIntegrityMismatchFailClosed(int probeRid, int explorerRid)
    {
        AssertRejected(ProbeEnvironment.Evaluate(Medium with { IntegrityRid = probeRid }, Shell with { IntegrityRid = explorerRid }), "DifferentIntegrity");
    }

    [Fact] public void OtherUserCannotBeUsedAsShell() => AssertRejected(ProbeEnvironment.Evaluate(Medium, Shell with { UserSid = "S-1-5-21-1-2-3-1002" }), "DifferentUser");
    [Fact] public void OtherSessionCannotBeUsedAsShell() => AssertRejected(ProbeEnvironment.Evaluate(Medium, Shell with { SessionId = 2 }), "DifferentSession");
    [Fact] public void ShellRestartDuringReadFailsClosed() => AssertRejected(ProbeEnvironment.Evaluate(Medium, Shell, false), "DesktopShellChanged");
    [Fact] public void PartialSnapshotWithQueryErrorNeverAdmits() => AssertRejected(ProbeEnvironment.Evaluate(Medium, Shell, queryError: "AccessDenied"), "QueryFailed: AccessDenied");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingTokenFailsClosed(bool missingProbe) => AssertRejected(ProbeEnvironment.Evaluate(missingProbe ? null : Medium, missingProbe ? Shell : null), "MissingTokenSnapshot");

    [Theory]
    [InlineData("sid")]
    [InlineData("session")]
    [InlineData("pid")]
    [InlineData("elevation-type")]
    public void IncompleteMetadataFailsClosed(string field)
    {
        var invalid = field switch
        {
            "sid" => Medium with { UserSid = "" },
            "session" => Medium with { SessionId = 0 },
            "pid" => Medium with { ProcessId = 0 },
            _ => Medium with { ElevationType = 0 }
        };
        AssertRejected(ProbeEnvironment.Evaluate(invalid, Shell), "IncompleteTokenSnapshot");
        AssertRejected(ProbeEnvironment.Evaluate(Medium, invalid), "IncompleteTokenSnapshot");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0x1000)]
    [InlineData(0x2100)]
    [InlineData(0x4000)]
    public void UnverifiedIntegrityLevelsAreNotAcceptedEvenWhenEqual(int rid) => AssertRejected(
        ProbeEnvironment.Evaluate(Medium with { IntegrityRid = rid }, Shell with { IntegrityRid = rid }), "UnsupportedIntegrity");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UiAccessDoesNotPermitSpecialBypass(bool probeHasUiAccess) => AssertRejected(
        ProbeEnvironment.Evaluate(Medium with { UiAccess = probeHasUiAccess }, Shell with { UiAccess = !probeHasUiAccess }), "UiAccessUnsupported");

    private static void AssertRejected(ProbeEnvironmentResult result, string reason)
    {
        Assert.False(result.Allowed); Assert.Equal("Rejected", result.Kind); Assert.Equal(reason, result.Reason);
    }
}
