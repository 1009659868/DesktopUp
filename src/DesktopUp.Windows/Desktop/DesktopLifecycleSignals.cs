using DesktopUp.Application;

namespace DesktopUp.Windows;

public static class DesktopLifecycleSignals
{
    public const uint PowerBroadcast = 0x0218, SessionChange = 0x02B1;
    public static SystemLifecycleSignal? Classify(uint message, nuint value, nint session, int ownSession)
    {
        if (message == PowerBroadcast) return value switch
        {
            4 => SystemLifecycleSignal.PowerSuspend,
            6 or 7 or 18 => SystemLifecycleSignal.PowerResume,
            _ => null
        };
        if (message != SessionChange || session != ownSession) return null;
        return value switch
        {
            7 => SystemLifecycleSignal.SessionLock,
            8 or 15 => SystemLifecycleSignal.SessionUnlock,
            2 or 4 or 6 => SystemLifecycleSignal.SessionDisconnect,
            1 or 3 or 5 => SystemLifecycleSignal.SessionConnect,
            _ => null // 9 is remote-control change, NOT remote-connect; 10 is reserved.
        };
    }
}
