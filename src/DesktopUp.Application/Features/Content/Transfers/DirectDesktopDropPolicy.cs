namespace DesktopUp.Application;

public enum DirectDesktopDropDecision { Internal, Reject, Preview, Commit }
public static class DirectDesktopDropPolicy
{
    public static DirectDesktopDropDecision Resolve(PosterInputEventKind kind,PointerScreenPoint point,DesktopPixelRect frame,
        bool realItem,bool canEdit,bool actualDesktop,bool moveModifier,bool available)
    {
        if(!realItem||!canEdit||kind is not (PosterInputEventKind.GesturePreview or PosterInputEventKind.GestureCompleted))
            return DirectDesktopDropDecision.Internal;
        if(point.X>=frame.X&&point.Y>=frame.Y&&point.X<frame.X+frame.Width&&point.Y<frame.Y+frame.Height)
            return DirectDesktopDropDecision.Internal;
        if(!actualDesktop||!moveModifier||!available)return DirectDesktopDropDecision.Reject;
        return kind==PosterInputEventKind.GestureCompleted?DirectDesktopDropDecision.Commit:DirectDesktopDropDecision.Preview;
    }
}
