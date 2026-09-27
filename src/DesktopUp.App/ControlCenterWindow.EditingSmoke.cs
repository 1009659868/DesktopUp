using DesktopUp.Application;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;

namespace DesktopUp;

// Explicit isolated CLI only. Text/Invoke use actual controls; this is NOT real-mouse acceptance.
public sealed partial class ControlCenterWindow
{
    // Global profile actions remain reachable with no selection or even an empty catalog.
    internal bool VerifyMoreMenuForProbe() => MoreActionsButton.IsEnabled &&
        MoreActionsButton.Visibility == Visibility.Visible &&
        MoreActionsButton.Flyout is MenuFlyout menu && menu.Items.Contains(PortableProfileMenu) &&
        PortableProfileMenu.IsEnabled && PortableProfileMenu.Visibility == Visibility.Visible &&
        PortableProfileMenu.Items.OfType<MenuFlyoutItem>().All(item => item.IsEnabled) &&
        PortableProfileMenu.Items.OfType<MenuFlyoutItem>()
            .Select(item => Microsoft.UI.Xaml.Automation.AutomationProperties.GetAutomationId(item))
            .SequenceEqual(new[] { "PortableExport", "PortableImport", "PortablePending", "PortableBackup", "PortableRecovery" }) &&
        (_view?.SelectedPoster is not null || !RenamePosterButton.IsEnabled && !ClonePosterButton.IsEnabled && !RemoveButton.IsEnabled);

    internal ControlCenterPosterEditor EditorForProbe => _editor;
    internal bool CatalogDialogForProbe => _catalogDialog is not null;
    internal bool LegacyGridFieldsReadOnlyForProbe() => new[]{"ItemWidth","ItemHeight","Gap"}.All(key=>!_styleFields[key].IsEnabled);
    internal void CancelCatalogForProbe() => _catalogDialog?.Hide();
    internal void EditFieldForProbe(bool style, string key, string text)
    {
        ShowWorkspacePage(false); AdvancedSettings.IsExpanded = true;
        if (style) foreach (var expander in EditorSections.Children.OfType<Expander>()) expander.IsExpanded = true;
        Content.UpdateLayout();
        var field = (style ? _styleFields : _geometryFields)[key];
        if(!field.IsEnabled) throw new InvalidOperationException("Cannot edit a disabled field: "+key);
        field.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
        field.Text = text;
    }
    internal void InvokeEditorForProbe(string action)
    {
        FrameworkElement button = action switch { "geometry" => _geometryApply, "geometry-cancel" => _geometryCancel,
            "style" => _styleApply, "style-cancel" => _styleCancel, "create" => CreatePosterButton, "clone" => ClonePosterButton, _ => RenamePosterButton };
        if (button is Control control && !control.IsEnabled)
            throw new InvalidOperationException($"Control disabled: {action}; loaded={button.IsLoaded}; group={_editor.Group}; dirty={_editor.IsDirty}; canCancel={_editor.CanCancel}; error={_editor.Error}");
        if (FrameworkElementAutomationPeer.CreatePeerForElement(button)?.GetPattern(PatternInterface.Invoke) is not IInvokeProvider invoke)
            throw new InvalidOperationException("No Invoke provider: " + action);
        invoke.Invoke();
    }
    internal void RevealEditorForProbe() { ShowWorkspacePage(false); AdvancedSettings.IsExpanded = true; _geometryApply.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false }); }
}
