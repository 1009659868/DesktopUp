using System.Runtime.InteropServices.WindowsRuntime;
using DesktopUp.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace DesktopUp;

internal sealed record TemplateRecipeFieldProbe(string Key, string Binding, string Kind, string Label,
    string AutomationId, bool Connected, TemplateParameterValue Value);
internal sealed record TemplateRecipeUiProbe(string? SelectedPosterId, string? TemplateId, int CatalogCount,
    int Page, string Range, string[] Samples, bool PreviousEnabled, bool NextEnabled,
    string Theme, string RenderedTheme, TemplateRecipeFieldProbe[] Fields, bool DraftActive,
    string? DialogTitle, bool DialogReady, bool PrimaryEnabled, string[] DialogText, int BookCards);

// Explicit isolated App probe only. Drives actual controls and production event handlers;
// no native input synthesis, alternate state command path, or copy of recipe validation.
public sealed partial class ControlCenterWindow
{
    private static IEnumerable<T> RecipeVisuals<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T current) yield return current;
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var child in RecipeVisuals<T>(VisualTreeHelper.GetChild(root, index))) yield return child;
    }

    private static void InvokeRecipeButton(Button button)
    {
        if (!button.IsEnabled || button.Visibility != Visibility.Visible)
            throw new InvalidOperationException("The production recipe button is not enabled/visible.");
        var peer = FrameworkElementAutomationPeer.FromElement(button) ?? FrameworkElementAutomationPeer.CreatePeerForElement(button);
        if (peer?.GetPattern(PatternInterface.Invoke) is not IInvokeProvider invoke)
            throw new InvalidOperationException("The actual recipe button has no Invoke provider.");
        invoke.Invoke();
    }

    internal bool TemplateDialogReadyForProbe => _activeTemplateDialog is { IsLoaded: true, ActualWidth: > 0, ActualHeight: > 0 };
    internal bool TemplateDialogClosedForProbe => !_templateReferenceOpen && _activeTemplateDialog is null;
    internal void OpenTemplateDialogForProbe(string kind)
    {
        if (_templateReferenceOpen) throw new InvalidOperationException("A recipe dialog is already open.");
        if (kind == "import") InvokeRecipeButton(TemplateActionsHost.Children.OfType<Button>().Single(b => b.Content?.ToString() == "装入模板 ↗"));
        else if (kind == "copy") InvokeRecipeButton(TemplateActionsHost.Children.OfType<Button>().Last());
        else if (kind == "book") InvokeRecipeButton(TemplateLibraryActionsHost.Children.OfType<Button>().Single());
        else if (kind == "theme") InvokeRecipeButton(UiCThemeButton);
        else throw new ArgumentException("Unknown recipe dialog.", nameof(kind));
    }
    internal void FillTemplateJsonForProbe(string json)
    {
        var dialog = _activeTemplateDialog ?? throw new InvalidOperationException("No production import dialog.");
        RecipeVisuals<TextBox>(dialog).Single(field => AutomationProperties.GetName(field) == "模板 JSON 配方").Text = json;
    }
    internal void RenameTemplateCopyForProbe(string name)
    {
        var dialog = _activeTemplateDialog ?? throw new InvalidOperationException("No production copy dialog.");
        RecipeVisuals<TextBox>(dialog).Single(field => field.Header?.ToString() == "模板名称").Text = name;
    }
    internal void InvokeTemplateDialogForProbe(string action)
    {
        var dialog = _activeTemplateDialog ?? throw new InvalidOperationException("No production recipe dialog.");
        string text = action switch
        {
            "primary" => dialog.PrimaryButtonText, "check" => dialog.SecondaryButtonText,
            "close" => dialog.CloseButtonText, _ => throw new ArgumentException("Unknown dialog action.")
        };
        var candidates = RecipeVisuals<Button>(dialog).Where(button => button.Content?.ToString() == text).ToArray();
        if (candidates.Length != 1) throw new InvalidOperationException("Expected one actual dialog button for " + text + ".");
        InvokeRecipeButton(candidates[0]);
    }
    internal void NextTemplatePageForProbe() => InvokeRecipeButton(TemplateNext);
    internal void PreviousTemplatePageForProbe() => InvokeRecipeButton(TemplatePrevious);
    internal void ChooseCenterThemeForProbe(string name)
    {
        var dialog = _activeTemplateDialog ?? throw new InvalidOperationException("No production theme dialog.");
        var button = RecipeVisuals<Button>(dialog).Single(candidate => name == "跟随系统"
            ? candidate.Content?.ToString() == name
            : candidate.Content is StackPanel panel && panel.Children.OfType<TextBlock>().Any(text => text.Text == name));
        InvokeRecipeButton(button);
    }
    internal void ChangeRecipeNumberForProbe(string key, double value, bool commit)
    {
        if (!LampInputAvailable()) throw new InvalidOperationException("Current template parameters are unavailable.");
        _settingsHeld = true; _settingsTimer?.Stop();
        _lampNumbers[key].Value = value;
        if (commit) FinishSettingsForProbe();
    }
    internal void ChangeRecipeBooleanForProbe(string key, bool value)
    {
        if (!LampInputAvailable()) throw new InvalidOperationException("Current template parameters are unavailable.");
        _lampBooleans[key].IsOn = value;
    }
    internal void ResetRecipeControlsForProbe() => InvokeRecipeButton(_lampReset ?? throw new InvalidOperationException("No reset control."));

    internal TemplateRecipeUiProbe ReadTemplateRecipeUiForProbe()
    {
        (Content as FrameworkElement)?.UpdateLayout();
        var fields = _templateParameterSchema.Properties.Select(property =>
        {
            FrameworkElement control = property.Kind switch
            {
                TemplateParameterKind.Boolean => _lampBooleans[property.Key],
                TemplateParameterKind.Range => _lampNumbers[property.Key],
                _ => _lampColors[property.Key]
            };
            var value = property.Kind switch
            {
                TemplateParameterKind.Boolean => TemplateParameterValue.FromBoolean(_lampBooleans[property.Key].IsOn),
                TemplateParameterKind.Range => TemplateParameterValue.FromNumber(_lampNumbers[property.Key].Value),
                _ => TemplateParameterValue.FromColor(_lampColors[property.Key].Text)
            };
            string label = property.Kind == TemplateParameterKind.Boolean ? _lampBooleans[property.Key].Header?.ToString() ?? "" :
                property.Kind == TemplateParameterKind.Range ? _lampLabels[property.Key].Text : AutomationProperties.GetName(control);
            return new TemplateRecipeFieldProbe(property.Key, property.Binding, property.Kind.ToString(), label,
                AutomationProperties.GetAutomationId(control), control.IsLoaded && control.ActualWidth > 0, value);
        }).ToArray();
        var dialog = _activeTemplateDialog;
        return new(_view?.SelectedPosterId, SelectedTemplateKey, _templateCatalog.Count, _templateSamplePage,
            TemplateSampleRange.Text, _templateSamples.Keys.ToArray(), TemplatePrevious.IsEnabled, TemplateNext.IsEnabled,
            _view?.Projection.Settings.Settings.CenterTheme ?? "", UiCRoot.ActualTheme.ToString(), fields, _settings.ProtectsInteraction,
            dialog?.Title?.ToString(), TemplateDialogReadyForProbe, dialog?.IsPrimaryButtonEnabled == true,
            dialog is null ? [] : RecipeVisuals<TextBlock>(dialog).Select(text => text.Text).Where(text => text.Length > 0).ToArray(),
            dialog?.Title?.ToString() == "海报模板册" ? RecipeVisuals<Button>(dialog).Count(button =>
                button.Content is StackPanel panel && panel.Children.OfType<Grid>().Any()) : 0);
    }
    internal async Task CaptureTemplateDialogForProbeAsync(string path)
    {
        var dialog = _activeTemplateDialog ?? throw new InvalidOperationException("No dialog to capture.");
        var bitmap = new RenderTargetBitmap(); await bitmap.RenderAsync(dialog);
        if (bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0) throw new InvalidOperationException("Dialog capture is empty.");
        var pixels = await bitmap.GetPixelsAsync();
        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(path)!);
        var file = await folder.CreateFileAsync(Path.GetFileName(path), CreationCollisionOption.FailIfExists);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
    }
}
