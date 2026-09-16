using DesktopUp.Application;

namespace DesktopUp.Contracts.Tests;

public sealed class TitleNativeEndPolicyTests
{
    public static IEnumerable<object[]> Causes() => Enum.GetValues<TitleNativeEndReason>().Select(v => new object[] { v });
    [Theory] [MemberData(nameof(Causes))]
    public void OnlyRealCaptureTransferWithValidInputEvidenceIsSoft(TitleNativeEndReason cause)
    {
        Assert.Equal(cause == TitleNativeEndReason.CaptureTransferred,
            TitleNativeEndPolicy.AllowsSafeStop(TitleNativeEndPolicy.Resolve(cause, true, true)));
        Assert.Equal(TitleNativeEndReason.RouteInvalidated, TitleNativeEndPolicy.Resolve(cause, false, true));
        Assert.Equal(TitleNativeEndReason.RouteInvalidated, TitleNativeEndPolicy.Resolve(cause, false, false));
        Assert.Equal(TitleNativeEndReason.CursorUnavailable, TitleNativeEndPolicy.Resolve(cause, true, false));
        Assert.False(TitleNativeEndPolicy.AllowsSafeStop(TitleNativeEndPolicy.Resolve(cause, false, false)));
    }
    [Fact]
    public void UnknownNativeReasonCannotAuthorizeSafeStop() =>
        Assert.False(TitleNativeEndPolicy.AllowsSafeStop((TitleNativeEndReason)999));
}
