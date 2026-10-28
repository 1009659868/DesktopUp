namespace DesktopUp.Domain;

public readonly record struct CloudPoint(double X, double Y);

// Code-native outline from the accepted UI C core. One polygon owns paint and
// conservative grip strips; the gutter belongs to the title, not the item layout.
public static class TitleCloudShape
{
    private static readonly CloudPoint[] Outline = Sample();
    private static readonly object Gate = new();
    private static double _cachedWidth = -1;
    private static IReadOnlyList<TitleChromeRect>? _cachedGrips;
    private static CloudPoint[] Sample()
    {
        double[][] curves = [
            [16,93,11,78,22,67], [28,59,40,58,47,60], [44,41,60,29,78,35],
            [90,12,127,11,145,29], [166,9,204,14,216,38], [239,24,268,36,272,57],
            [291,47,314,58,316,75], [337,72,345,90,327,99], [310,110,288,103,268,105],
            [248,113,228,107,208,109], [184,118,167,109,147,112],
            [124,116,103,110,83,112], [67,111,45,108,35,94]
        ];
        var points = new List<CloudPoint> { new(35,94) };
        foreach (var c in curves)
        {
            var start = points[^1];
            for (int i = 1; i <= 12; i++)
            {
                double t = i / 12.0, u = 1-t;
                points.Add(new(u*u*u*start.X+3*u*u*t*c[0]+3*u*t*t*c[2]+t*t*t*c[4],
                    u*u*u*start.Y+3*u*u*t*c[1]+3*u*t*t*c[3]+t*t*t*c[5]));
            }
        }
        return points.ToArray();
    }
    public static IReadOnlyList<CloudPoint> Resolve(double width) => Outline
        .Select(p => new CloudPoint(16+p.X/360*(width-32), 3+p.Y*.45)).ToArray();

    public static TitleChromeRect Bounds(double width)
    {
        var points = Resolve(width);
        double left = points.Min(p=>p.X), top = points.Min(p=>p.Y);
        return new(left,top,points.Max(p=>p.X)-left,points.Max(p=>p.Y)-top);
    }
    private static List<(double Left, double Right)> Spans(IReadOnlyList<CloudPoint> points, double y)
    {
        var xs = new List<double>();
        for (int i=0; i<points.Count-1; i++)
        {
            var a=points[i]; var b=points[i+1];
            if ((a.Y<=y && b.Y>y) || (b.Y<=y && a.Y>y))
                xs.Add(a.X+(y-a.Y)*(b.X-a.X)/(b.Y-a.Y));
        }
        xs.Sort(); var spans = new List<(double,double)>();
        for(int i=0;i+1<xs.Count;i+=2) spans.Add((xs[i],xs[i+1]));
        return spans;
    }
    public static bool Contains(double width, double x, double y) =>
        Spans(Resolve(width),y).Any(p=>x>=p.Left-1e-6 && x<=p.Right+1e-6);

    public static IReadOnlyList<TitleChromeRect> GripBands(double width)
    {
        lock(Gate)
        {
            if(_cachedWidth==width && _cachedGrips is not null) return _cachedGrips;
            _cachedGrips=BuildGripBands(width); _cachedWidth=width; return _cachedGrips;
        }
    }
    private static IReadOnlyList<TitleChromeRect> BuildGripBands(double width)
    {
        var points=Resolve(width); var result=new List<TitleChromeRect>();
        // Intersect at row edges AND every intervening polygon vertex. This does
        // not bridge the transparent gaps between the cloud's upper lobes.
        for(int y=0;y+3<=60;y++)
        {
            var levels = points.Where(p=>p.Y>y && p.Y<y+3).SelectMany(p=>new[]{p.Y-1e-7,p.Y+1e-7})
                .Prepend(y+1e-7).Append(y+3-1e-7);
            var safe=new List<(double Left,double Right)> { (0,width) };
            foreach(double level in levels)
            {
                var spans=Spans(points,level); var next=new List<(double,double)>();
                foreach(var a in safe) foreach(var b in spans)
                    if(Math.Min(a.Right,b.Right)>Math.Max(a.Left,b.Left))
                        next.Add((Math.Max(a.Left,b.Left),Math.Min(a.Right,b.Right)));
                safe=next; if(safe.Count==0) break;
            }
            foreach(var span in safe)
                if(span.Right-span.Left>1) result.Add(new(span.Left+.5,y,span.Right-span.Left-1,3));
        }
        return result.AsReadOnly();
    }
}
