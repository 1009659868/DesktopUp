using System.Runtime.CompilerServices;
using DesktopUp.Domain;
namespace DesktopUp.Modules.Rain;

internal static class RainDeclaration
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Trusted optional module composition.")]
    [ModuleInitializer]
    internal static void Register()
    {
        var current = new TemplateRecipe(new("rain", "雨夜窗边", "rain.paint@1",
        [
            new("amount", "rain.count", TemplateParameterKind.Range, "雨量", "天气", TemplateParameterValue.FromNumber(28), "固定上限 64 滴；设为 0 仍保留云朵。", "滴", 0, 64, 1),
            Color("rainColor", "rain.color", "雨的颜色", "#93C9E9"),
            Color("cloudColor", "cloud.color", "云的颜色", "#3F576D"),
            new("cloudSize", "cloud.size", TemplateParameterKind.Range, "云的尺寸", "天气", TemplateParameterValue.FromNumber(1), "只改变云的形体及可拖区域，不改变文字、按钮或内容。大云会收敛摆幅以完整显示。", "×", .6, 1.1, .05),
            new("speed", "rain.speed", TemplateParameterKind.Range, "落雨速度", "天气", TemplateParameterValue.FromNumber(1), "", "×", .5, 2, .1),
            new("wind", "cloud.wind", TemplateParameterKind.Range, "风的力度", "天气", TemplateParameterValue.FromNumber(35), "只推动云层，不移动内容。", "%", 0, 100, 1),
            new("lightning", "rain.lightning", TemplateParameterKind.Boolean, "微光闪电", "天气", TemplateParameterValue.FromBoolean(true), "低频局部微光；减少动效时停用。"),
            new("lightningInterval", "rain.lightningInterval", TemplateParameterKind.Range, "闪电间隔", "天气", TemplateParameterValue.FromNumber(23), "仅计算播放时间；越短越频繁。每次电光换一个落点，同一次不跳动。", "秒", 8, 90, 1),
            new("card", "item.color", TemplateParameterKind.Color, "卡片颜色", "材质", TemplateParameterValue.FromColor("#253D53")),
            new("motionEnabled", "scene.motion", TemplateParameterKind.Boolean, "环境动效", "动效", TemplateParameterValue.FromBoolean(true))
        ], TemplateVersion: 3), "天气场景", new("#152337", "#253D53", true));
        var v2=current with { Schema=current.Schema with { TemplateVersion=2,
            Properties=current.Schema.Properties.Where(p=>p.Binding!="cloud.size").ToArray() } };
        var v1=v2 with { Schema=v2.Schema with { TemplateVersion=1,
            Properties=v2.Schema.Properties.Where(p=>p.Binding!="rain.lightningInterval").ToArray() } };
        SceneCapabilityRegistry.Register(new("rain.paint@1",current, TransparentSurface: true, ReplacesMetaText: true,
            TitleChrome: PosterTitleChromeKind.Cloud, TitleColorBinding: "cloud.color", PreviousBuiltIns:[v1,v2]));
    }
    private static TemplateParameterDefinition Color(string key, string binding, string label, string value) =>
        new(key, binding, TemplateParameterKind.Color, label, "天气", TemplateParameterValue.FromColor(value));
}
