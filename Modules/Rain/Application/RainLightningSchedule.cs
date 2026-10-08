namespace DesktopUp.Application;

public readonly record struct RainLightningSample(double Opacity, uint StrokeSeed);

// Per-scene active-time schedule. A parameter edit reschedules the NEXT strike,
// never teleports the currently visible one. No wall clock or renderer-owned timer.
public sealed class RainLightningSchedule(uint seed)
{
    public const double DefaultInterval = 23, MinimumInterval = 8, MaximumInterval = 90, Duration = .7;
    private double _next = double.NaN, _onset = double.NegativeInfinity, _interval = DefaultInterval, _last;
    private uint _event, _stroke;
    public static double Interval(double seconds) => double.IsFinite(seconds)
        ? Math.Clamp(seconds,MinimumInterval,MaximumInterval) : DefaultInterval;
    public static uint Mix(uint value)
    {
        unchecked { value ^= value>>16; value*=0x7feb352d; value^=value>>15; value*=0x846ca68b; return value^(value>>16); }
    }
    public static double Unit(uint seed, uint salt) => (Mix(seed ^ salt) & 0xffffff)/16777215.0;
    public static double Pulse(double elapsed) => elapsed is >= 0 and <= Duration
        ? .24*Math.Sin(elapsed/Duration*Math.PI) : 0;
    public RainLightningSample Step(double seconds, bool enabled, double interval)
    {
        interval=Interval(interval);
        if(!enabled || !double.IsFinite(seconds) || seconds<0)
        { _next=double.NaN; _onset=double.NegativeInfinity; return new(0,_stroke); }
        if(double.IsNaN(_next) || seconds<_last)
        { _next=seconds+interval*17/23; _onset=double.NegativeInfinity; }
        else if(interval!=_interval) _next=seconds+interval;
        _interval=interval; _last=seconds;
        if(seconds>=_next)
        {
            _onset=seconds; _next=seconds+interval;
            _stroke=Mix(seed ^ ++_event); if(_stroke==0) _stroke=1;
        }
        return new(Pulse(seconds-_onset),_stroke);
    }
}
