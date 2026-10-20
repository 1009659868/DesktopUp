using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal sealed partial class PosterVisualWindow
{
    private TextBlock? _mappingBadge;
    private void AddMappingBadge(Grid root)
    {
        _mappingBadge=new TextBlock {FontSize=13,Margin=new(0,12,82,0),HorizontalAlignment=HorizontalAlignment.Right,
            VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false};
        root.Children.Add(_mappingBadge);UpdateMappingBadge();
    }
    internal void UpdateMappingBadge(bool restored=false)
    {
        if(_mappingBadge is null || _disposed)return;
        string id=PosterId??inputSession.PosterId;
        bool mapped=spatial.ContentActionPath(id,null) is not null;
        bool failed=!spatial.MappingAvailable(id) || spatial.MappingMessage(id) is not null;
        _mappingBadge.Text=!mapped?"":failed?"!":restored?"✓":"↔";
        _mappingBadge.Foreground=ColorBrush(failed?"#FFC773":"#A8D8C8");
        _mappingBadge.Visibility=mapped?Visibility.Visible:Visibility.Collapsed;
    }
}
