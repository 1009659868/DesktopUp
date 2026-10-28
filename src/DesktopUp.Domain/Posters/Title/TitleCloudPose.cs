namespace DesktopUp.Domain;

// One bounded transform for host paint and input. Never a template-owned native callback.
public readonly record struct TitleCloudPose(double X, double Y, double Angle, double Vapor = 0, double Wisp = 0, double Scale = 1)
{
    public const double CenterY = 32;
    public static double Size(double value) => double.IsFinite(value) ? Math.Clamp(value,.6,1.1) : 1;
    public TitleCloudPose Bounded() => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Angle) &&
        double.IsFinite(Vapor) && double.IsFinite(Wisp) && double.IsFinite(Scale)
        ? new(Math.Clamp(X,-12,12), Math.Clamp(Y,-10,10), Math.Clamp(Angle,-4.5,4.5),
            Math.Clamp(Vapor,-1,1),Math.Clamp(Wisp,-1,1), Scale==0 ? 1 : Size(Scale)) : new(0,0,0);
    public TitleCloudPose Fit(double width)
    {
        var pose=Bounded(); var b=TitleCloudShape.Bounds(width);
        // Keep the requested size. Only fit motion to the remaining compact
        // budget; do not crop or silently save a smaller cloud/changed wind.
        bool Fits(double angle)
        {
            double a=angle*Math.PI/180, c=Math.Cos(a), s=Math.Sin(a);
            return pose.Scale*(b.Width*c+b.Height*s)<=width-4 &&
                pose.Scale*(b.Height*c+b.Width*s)<=56;
        }
        if(!Fits(Math.Abs(pose.Angle)))
        {
            double low=0, high=Math.Abs(pose.Angle);
            for(int i=0;i<32;i++) { double mid=(low+high)/2; if(Fits(mid)) low=mid; else high=mid; }
            pose=pose with { Angle=Math.CopySign(low,pose.Angle) };
        }
        var points=new[]{pose.Transform(b.X,b.Y,width),pose.Transform(b.Right,b.Y,width),
            pose.Transform(b.X,b.Bottom,width),pose.Transform(b.Right,b.Bottom,width)};
        double dx=Math.Max(0,2-points.Min(p=>p.X))-Math.Max(0,points.Max(p=>p.X)-(width-2));
        double dy=Math.Max(0,2-points.Min(p=>p.Y))-Math.Max(0,points.Max(p=>p.Y)-58);
        // Numerical dust must not publish a new native layout on every refit.
        if(Math.Abs(dx)<1e-9) dx=0; if(Math.Abs(dy)<1e-9) dy=0;
        return pose with { X=pose.X+dx,Y=pose.Y+dy };
    }
    public (double X, double Y) Transform(double x, double y, double width)
    {
        var pose = Bounded();
        double c = Math.Cos(pose.Angle * Math.PI / 180), s = Math.Sin(pose.Angle * Math.PI / 180);
        return (pose.Scale*((x - width / 2) * c - (y - CenterY) * s) + width / 2 + pose.X,
            pose.Scale*((x - width / 2) * s + (y - CenterY) * c) + CenterY + pose.Y);
    }
}
