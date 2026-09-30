using DesktopUp.Application;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopUp;

internal readonly record struct SceneCardPaint(Border Card, bool Selected, double? Left, double? Top);

/// <summary>Decorative view only: no window, input router, solver, state store or command service.</summary>
internal interface IPosterSceneInstance : IDisposable
{
    FrameworkElement Root { get; }
    bool WantsMotion { get; }
    void Update(SceneRenderParameters parameters, SceneFrameContext frame);
    void PaintCard(SceneCardPaint paint);
    void StopMotion();
}
