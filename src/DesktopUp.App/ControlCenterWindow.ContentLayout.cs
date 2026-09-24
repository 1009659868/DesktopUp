using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using global::Windows.System;

namespace DesktopUp;

public sealed partial class ControlCenterWindow
{
    private Func<PosterSpatialRuntime?>? _contentRuntime;
    private bool _renderingContent, _contentFocused;
    private string? _contentTargetId;
    private PosterSpatialRuntime? _contentObservedRuntime;
    private bool _contentRefreshQueued;
    internal void ConfigureContentLayout(Func<PosterSpatialRuntime?> runtime)
    {
        _contentRuntime=runtime;
        _contentObservedRuntime=runtime();
        if(_contentObservedRuntime is not null) _contentObservedRuntime.ContentEditorChanged+=ContentEditorChanged;
        ContentLayoutPanel.GotFocus+=(_,_)=>{_contentFocused=true;SettingsInteractionChanged?.Invoke();};
        ContentLayoutPanel.LostFocus+=(_,_)=>
        {
            DispatcherQueue.TryEnqueue(()=>
            {
                if(_editingClosed || _contentRuntime is null || ContentLayoutPanel.XamlRoot is null) return;
                var focused=FocusManager.GetFocusedElement(ContentLayoutPanel.XamlRoot) as DependencyObject;
                bool inside=false;
                while(focused is not null) {if(ReferenceEquals(focused,ContentLayoutPanel)){inside=true;break;} focused=Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(focused);}
                _contentFocused=inside; SettingsInteractionChanged?.Invoke();
            });
        };
        Closed+=(_,_)=>
        {
            if(_contentObservedRuntime is not null) _contentObservedRuntime.ContentEditorChanged-=ContentEditorChanged;
            _contentObservedRuntime=null; _contentFocused=false;
            _contentRuntime?.Invoke()?.CancelContentEditors();_contentRuntime=null;SettingsInteractionChanged?.Invoke();
        };
    }
    private void ContentEditorChanged(string id) {if(id==_view?.SelectedPosterId) QueueContentRefresh();}
    private void QueueContentRefresh()
    {
        if(_contentRefreshQueued || _editingClosed) return;
        _contentRefreshQueued=true;
        DispatcherQueue.TryEnqueue(()=>{_contentRefreshQueued=false;if(!_editingClosed && _contentRuntime is not null) RenderContentLayout();});
    }
    private void RenderContentLayout()
    {
        if(ContentLayoutPanel is null || _editingClosed) return;
        var id=_view?.SelectedPosterId;
        if(id!=_contentTargetId) {_contentRuntime?.Invoke()?.CancelContentEditors();_contentTargetId=id;}
        var view=_contentRuntime?.Invoke()?.ReadContentEditor(id);
        _renderingContent=true;
        try
        {
            ContentLayoutHost.IsEnabled=view?.CanEdit==true;
            ContentLayoutStatus.Text=view is null?"请先显示海报；内容布局不会强行恢复隐藏窗口。":
                $"{view.Document.Mode switch {ContentLayoutMode.Automatic=>"自动",ContentLayoutMode.Anchored=>"锚定＋自动",_=>"全手动"}} · {view.Document.Placements.Count} 项 · 布局版本 {view.Document.DocumentVersion}";
            if(view is null) {ContentItemSelector.ItemsSource=null;return;}
            ContentGridSwitch.IsChecked=view.Document.ShowBaselineGrid;
            ContentSpanSwitch.IsChecked=view.Document.ShowSpanLabels;
            // One entry point for tidying: entering shows "整理内容", editing shows "完成整理".
            ContentEditToggle.Content=view.IsEditing?"完成整理":"整理内容";
            // Per-poster switches. Both are read back from the committed document so the control always
            // shows the poster's real state, never a local guess.
            ContentFlowSelector.SelectedIndex=view.Document.FlowAxis==ContentFlowAxis.HorizontalLoop?1:0;
            ContentLoopSwitch.IsChecked=view.Document.LoopScroll;
            var items=new DemoContentItemViewSource().Read(view.Document);
            if(ContentItemSelector.ItemsSource is not IReadOnlyList<ContentItemView> existing || !existing.SequenceEqual(items)) ContentItemSelector.ItemsSource=items;
            var source=(IReadOnlyList<ContentItemView>)ContentItemSelector.ItemsSource;
            ContentItemSelector.SelectedItem=source.FirstOrDefault(p=>p.ItemId==view.SelectedItemId);
            var placement=view.Document.Placements.FirstOrDefault(p=>p.ItemId==view.SelectedItemId);
            ContentColumns.IsEnabled=ContentRows.IsEnabled=ContentFixedButton.IsEnabled=ContentKeyboardPad.IsEnabled=placement is not null;
            ContentColumns.Value=placement?.ColumnSpan??double.NaN;
            ContentRows.Value=placement?.RowSpan??double.NaN;
            ContentFixedButton.Content=placement?.FixedPosition==true?"解除位置固定":"固定位置";
            ContentLayoutNotice.Text=view.Notice??"编辑辅助只在海报编辑态显示；关闭辅助不改变排版与命中。";
        }
        finally {_renderingContent=false;}
    }
    private void ContentAction(Func<PosterSpatialRuntime,string,string?> action)
    {
        if(_renderingContent || _editingClosed || _view?.SelectedPosterId is not { } id || _contentRuntime?.Invoke() is not { } runtime) return;
        if(_settings.ProtectsInteraction || _editor.ProtectsInteraction) {ContentLayoutNotice.Text="请先完成当前外观或几何编辑。";return;}
        var error=action(runtime,id);
        RenderContentLayout();
        if(error is not null) ContentLayoutNotice.Text=error;
    }
    private void ContentEdit(ContentLayoutEdit edit)=>ContentAction((r,id)=>edit.ItemId is { } selected && r.ReadContentEditor(id)?.SelectedItemId!=selected
        ? "海报上的选择已变化；已刷新，请重新操作。" : r.ApplyContentEditor(id,edit));
    private string? SelectedContentId => (ContentItemSelector.SelectedItem as ContentItemView)?.ItemId;
    private void ContentEditToggle_Click(object sender,RoutedEventArgs e)=>ContentAction((r,id)=>r.SetContentEditing(id,r.ReadContentEditor(id)?.IsEditing!=true));
    private void ContentManual_Click(object sender,RoutedEventArgs e)=>ContentEdit(new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Manual});
    // Independent and explicit: leaving the automatic layout always goes through its own command so it
    // is never confused with simply finishing a tidy-up session.
    private void ContentRestoreAuto_Click(object sender,RoutedEventArgs e)
        =>ContentEdit(new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Automatic,ManualExit=ManualLayoutExit.RestoreAutomatic});
    private void ContentExitManual_Click(object sender,RoutedEventArgs e)
    {
        if(sender is MenuFlyoutItem {Tag:string tag} && Enum.TryParse<ManualLayoutExit>(tag,out var choice))
            ContentEdit(new(ContentLayoutEditKind.Mode){Mode=ContentLayoutMode.Automatic,ManualExit=choice});
    }
    private void ContentFlow_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(_renderingContent || ContentFlowSelector is null) return;
        if((ContentFlowSelector.SelectedItem as ComboBoxItem)?.Tag as string is not { } tag) return;
        var axis=tag=="horizontal"?ContentFlowAxis.HorizontalLoop:ContentFlowAxis.Vertical;
        ContentAction((r,id)=>r.SetContentFlowAxis(id,axis));
    }
    private void ContentLoop_Changed(object sender,RoutedEventArgs e)
    {
        if(_renderingContent || ContentLoopSwitch is null) return;
        bool enabled=ContentLoopSwitch.IsChecked==true;
        ContentAction((r,id)=>r.SetContentLoopScroll(id,enabled));
    }
    private void ContentGuides_Changed(object sender,RoutedEventArgs e)
    {
        if(!_renderingContent && ContentGridSwitch is not null && ContentSpanSwitch is not null)
            ContentEdit(new(ContentLayoutEditKind.Guides){ShowBaselineGrid=ContentGridSwitch.IsChecked==true,ShowSpanLabels=ContentSpanSwitch.IsChecked==true});
    }
    private void ContentItem_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(!_renderingContent) {var item=SelectedContentId;ContentAction((r,id)=>r.SelectContentEditor(id,item));}
    }
    private void ContentSpan_Changed(NumberBox sender,NumberBoxValueChangedEventArgs e)
    {
        if(_renderingContent || SelectedContentId is not { } item || ContentColumns is null || ContentRows is null ||
            !double.IsFinite(ContentColumns.Value) || !double.IsFinite(ContentRows.Value)) return;
        ContentEdit(new(ContentLayoutEditKind.Resize,item){ColumnSpan=(int)Math.Round(ContentColumns.Value),RowSpan=(int)Math.Round(ContentRows.Value)});
    }
    private void ContentPreset_Click(object sender,RoutedEventArgs e)
    {
        if(SelectedContentId is not { } item || sender is not Button {Tag:string tag}) return;
        ContentEdit(new(ContentLayoutEditKind.Resize,item){ColumnSpan=tag=="wide"?8:4,RowSpan=tag=="tall"?6:4});
    }
    private void ContentFixed_Click(object sender,RoutedEventArgs e)
    {
        if(SelectedContentId is not { } item) return;
        bool fixedNow=_contentRuntime?.Invoke()?.ReadContentEditor(_view?.SelectedPosterId)?.Document.Placements.FirstOrDefault(p=>p.ItemId==item)?.FixedPosition==true;
        ContentEdit(new(ContentLayoutEditKind.Fixed,item){Enabled=!fixedNow});
    }
    private void ContentAutoSize_Click(object sender,RoutedEventArgs e)
    { if(SelectedContentId is { } item) ContentEdit(new(ContentLayoutEditKind.SizeOverride,item){Enabled=false}); }
    private void ContentUndo_Click(object sender,RoutedEventArgs e)=>ContentAction((r,id)=>r.UndoContentEditor(id,(sender as Button)?.Tag as string=="redo"));
    private void ContentSample_Click(object sender,RoutedEventArgs e)
    {if(sender is Button {Tag:string tag}) ContentAction((r,id)=>r.ApplyContentSample(id,tag));}
    private void ContentKeyboard_KeyDown(object sender,KeyRoutedEventArgs e)
    {
        int dx=e.Key==VirtualKey.Left?-1:e.Key==VirtualKey.Right?1:0;
        int dy=e.Key==VirtualKey.Up?-1:e.Key==VirtualKey.Down?1:0;
        if(dx!=0 || dy!=0)
        {
            e.Handled=true;
            var selected=SelectedContentId;
            ContentAction((r,id)=>selected is null || r.ReadContentEditor(id)?.SelectedItemId!=selected
                ? "海报上的选择已变化；已刷新，请重新操作。" : r.NudgeContentEditor(id,dx,dy,DesktopUp.Windows.NativeInputKeys.ShiftDown));
        }
        else if(e.Key==VirtualKey.Escape) {e.Handled=true;ContentEditToggle.Focus(FocusState.Programmatic);}
    }
}
