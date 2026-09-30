using System.Runtime.CompilerServices;
using DesktopUp.Domain;
namespace DesktopUp.Modules.Lamp;
internal static class LampDeclaration
{
    private static TemplateParameterDefinition Color(string key, string binding, string label, string group, string value, string hint = "") =>
        new(key, binding, TemplateParameterKind.Color, label, group, TemplateParameterValue.FromColor(value), hint);
    private static TemplateParameterDefinition Range(string key, string binding, string label, string group, double value, string hint = "") =>
        new(key, binding, TemplateParameterKind.Range, label, group, TemplateParameterValue.FromNumber(value), hint, "%", 0, 100, 1);
    private static readonly TemplateParameterSchema Lamp = new("lamp", "深夜书桌", TemplateParameterSchemas.LampCapability,
    [
        Color("lightColor", "lamp.color", "灯光颜色", "光影", LampTemplatePolicy.Defaults.LightColor, "仅当前海报的灯光，不改变控制中心配色。"),
        Range("softness", "lamp.softness", "柔和度", "光影", LampTemplatePolicy.Defaults.Softness),
        Range("intensity", "lamp.intensity", "灯光强度", "光影", LampTemplatePolicy.Defaults.Intensity),
        Color("background", "surface.background", "桌面底色", "材质", LampTemplatePolicy.Defaults.Background),
        Color("card", "item.color", "卡片颜色", "材质", LampTemplatePolicy.Defaults.Card),
        Range("backgroundOpacity", "surface.opacity", "背景不透明度", "材质", LampTemplatePolicy.Defaults.BackgroundOpacity,
            "只改变海报背景透明度，文字与 Item 不会一起变透明。"),
        new("motionEnabled", "scene.motion", TemplateParameterKind.Boolean, "环境动效", "动效", TemplateParameterValue.FromBoolean(true)),
        new("followPointer", "lamp.follow", TemplateParameterKind.Boolean, "灯光跟随鼠标", "动效", TemplateParameterValue.FromBoolean(true)),
        Range("idleSway", "lamp.sway", "闲置轻摆", "动效", 35),
        Range("motionSpeed", "lamp.speed", "摆动速度", "动效", 40)
    ]);
    // Build-linked declaration registration, deliberately not a runtime extension loader.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Trusted optional module composition at assembly initialization.")]
    [ModuleInitializer]
    internal static void Register() => SceneCapabilityRegistry.Register(new(TemplateParameterSchemas.LampCapability,
        new(Lamp, "交互光影", new("#17221E", "#354237", true))));
}
