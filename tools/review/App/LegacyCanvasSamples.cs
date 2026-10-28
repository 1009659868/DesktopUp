namespace DesktopUp.Domain;

// Historical v1 fixtures, linked by contract tests and review builds only; never shipped.
public static class CanvasSamples
{
    public static IReadOnlyList<CanvasDesign> All { get; } = Array.AsReadOnly(new[] { Create(false), Create(true) });
    private static CanvasDesign Create(bool rainy)
    {
        static CanvasValue Param(string key) => new(Parameter: key);
        static IReadOnlyDictionary<string, CanvasValue> P(params (string, CanvasValue)[] values) => values.ToDictionary(x => x.Item1, x => x.Item2);
        var nodes = new List<CanvasNode>
        {
            new("root", null, "layout.canvas@1", new(), P()),
            new("body", "root", "draw.panel@1", rainy ? new(0, .16, 1, .84) : new(),
                P(("fill", Param("background")), ("opacity", Param("opacity")), ("radius", new(Number: rainy ? 20 : 12))))
        };
        var parameters = new List<CanvasParameter>
        {
            new("background", "背景颜色", "外观", "color", new(Text: rainy ? "#20383D" : "#282B30")),
            new("opacity", "背景不透明度", "外观", "number", new(Number: .96)),
            new("padding", "内容留白", "内容布局", "number", new(Number: 20), 8, 40),
            new("gap", "组件间距", "内容布局", "number", new(Number: 12), 0, 40)
        };
        if (rainy)
        {
            nodes.Add(new("rain", "body", "effect.rain@1", new(), P(("intensity", Param("rain")))));
            parameters.Add(new("rain", "雨效强度", "雨效", "number", new(Number: .35)));
        }
        nodes.Add(new("flow", "body", "layout.stack@1", new(), P(("padding", Param("padding")), ("gap", Param("gap")))));
        if (rainy)
        {
            nodes.Add(new("caption", "flow", "draw.text@1", new(), P(("text", Param("caption")), ("color", Param("ink")), ("fontSize", new(Number: 20)))));
            parameters.Add(new("caption", "标题文字", "文字", "string", new(Text: "雨夜书桌")));
            parameters.Add(new("ink", "文字颜色", "文字", "color", new(Text: "#E2ECE7")));
        }
        nodes.Add(new("members", "flow", "content.slot@1", new(), P()));
        if (rainy)
        {
            nodes.Add(new("lamp", "root", "draw.ellipse@1", new(.7, .015, .28, .11), P(("fill", Param("lampColor")), ("opacity", Param("lamp")))));
            parameters.Add(new("lampColor", "灯盘颜色", "灯光", "color", new(Text: "#F7C674")));
            parameters.Add(new("lamp", "灯光强度", "灯光", "number", new(Number: .7)));
        }
        return CanvasDeclaration.Freeze(new(new(rainy ? "rainy-desk" : "basic-storage", 1,
            rainy ? "雨夜书桌" : "基础收纳", nodes, parameters), new Dictionary<string, CanvasValue>()));
    }
    public static CanvasDesign Without(CanvasDesign design, string nodeId)
    {
        if (nodeId is not ("caption" or "lamp" or "rain")) throw new ArgumentException("不能移除内容槽或布局节点。");
        var nodes = design.Recipe.Nodes.Where(n => n.Id != nodeId).ToArray();
        var used = nodes.SelectMany(n => n.Properties.Values).Where(v => v.Parameter is not null).Select(v => v.Parameter!).ToHashSet(StringComparer.Ordinal);
        return CanvasDeclaration.Freeze(design with { Recipe = design.Recipe with { Nodes = nodes, Parameters = design.Recipe.Parameters.Where(p => used.Contains(p.Key)).ToArray() },
            Overrides = design.Overrides.Where(p => used.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value) });
    }
}
