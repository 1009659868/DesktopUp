using System.Runtime.CompilerServices;
using DesktopUp.Domain;
namespace DesktopUp.Modules.Rain;

internal static class RainDeclaration
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Trusted optional module composition.")]
    [ModuleInitializer]
    internal static void Register() => SceneCapabilityRegistry.Register(new("rain.paint@1",
        new(new("rain", "雨夜窗边", "rain.paint@1",
        [
            new("amount", "rain.count", TemplateParameterKind.Range, "雨量", "天气", TemplateParameterValue.FromNumber(28), "固定上限 64 滴；设为 0 仍保留云朵。", "滴", 0, 64, 1),
            Color("rainColor", "rain.color", "雨的颜色", "#93C9E9"),
            Color("cloudColor", "cloud.color", "云的颜色", "#3F576D"),
            new("speed", "rain.speed", TemplateParameterKind.Range, "落雨速度", "天气", TemplateParameterValue.FromNumber(1), "", "×", .5, 2, .1),
            new("wind", "cloud.wind", TemplateParameterKind.Range, "风的力度", "天气", TemplateParameterValue.FromNumber(35), "只推动云层，不移动内容。", "%", 0, 100, 1),
            new("lightning", "rain.lightning", TemplateParameterKind.Boolean, "微光闪电", "天气", TemplateParameterValue.FromBoolean(true), "低频局部微光；减少动效时停用。"),
            new("card", "item.color", TemplateParameterKind.Color, "卡片颜色", "材质", TemplateParameterValue.FromColor("#253D53")),
            new("motionEnabled", "scene.motion", TemplateParameterKind.Boolean, "环境动效", "动效", TemplateParameterValue.FromBoolean(true))
        ]), "天气场景", new("#152337", "#253D53", true)), TransparentSurface: true, ReplacesMetaText: true,
        TitleChrome: PosterTitleChromeKind.Cloud, TitleColorBinding: "cloud.color"));
    private static TemplateParameterDefinition Color(string key, string binding, string label, string value) =>
        new(key, binding, TemplateParameterKind.Color, label, "天气", TemplateParameterValue.FromColor(value));
}
