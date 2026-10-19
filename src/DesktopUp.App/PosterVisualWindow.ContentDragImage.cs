using DesktopUp.Application;
using DesktopUp.Windows;

namespace DesktopUp;
internal sealed partial class PosterVisualWindow
{
    private NativeContentDragImage? _contentDragImage;
    private IDisposable? _contentDragImageLease;
    private bool UpdateContentDragImage(ContentGesture gesture,PosterInputEventArgs args)
    {
        if (_nativeContentDrag) return true;
        if(!IsRealContent || !gesture.CanEdit || gesture.Target!=PosterInputTargetKind.ContentItem)return true;
        try
        {
            if(_contentDragImage is null)
            {
                byte[] pixels=[];int w=0,h=0;
                if(Rasters.VisibleKeys.TryGetValue(gesture.Slot.ItemId,out var key) &&
                    Rasters.Cache?.TryGet(key,out var payload)==true && payload is not null && payload.MediaType=="image/bgra32")
                {pixels=payload.Bytes.ToArray();w=payload.PixelWidth;h=payload.PixelHeight;}
                int size=Math.Clamp((int)Math.Round(Math.Min(gesture.Tile.Bounds.Width,gesture.Tile.Bounds.Height)*gesture.ScaleX),32,160);
                var image=ContentDragPixels.Build(pixels,w,h,size,gesture.TransferSelection.Length);
                double pressX=(args.LatestPoint.X-args.DeltaX-gesture.Frame.X)/gesture.ScaleX-ContentLeftDip;
                double pressY=(args.LatestPoint.Y-args.DeltaY-gesture.Frame.Y)/gesture.ScaleY-ContentTopDip;
                int grabX=(int)(Math.Clamp((pressX-gesture.Tile.Bounds.X)/gesture.Tile.Bounds.Width,0,1)*size);
                int grabY=(int)(Math.Clamp((pressY-gesture.Tile.Bounds.Y)/gesture.Tile.Bounds.Height,0,1)*size);
                _contentDragImage=new(image.Pixels,image.Width,image.Height,grabX,grabY,args.LatestPoint.X,args.LatestPoint.Y);
                _contentDragImageLease=ledger.Acquire("window.content-drag-image",PosterId!);
            }
            else _contentDragImage.Move(args.LatestPoint.X,args.LatestPoint.Y);
            return true;
        }
        catch(Exception e)
        {
            ReleaseContentDragImage();CancelContentGesture("drag-image-unavailable");
            PublishContentFeedback("拖动图像暂不可用，本次未移动；请重新拖动。",transient:true);
            log.Write("warning","content.drag_image_failed",new{e.Message});return false;
        }
    }
    private void ReleaseContentDragImage()
    {_contentDragImage?.Dispose();_contentDragImage=null;_contentDragImageLease?.Dispose();_contentDragImageLease=null;}
}
