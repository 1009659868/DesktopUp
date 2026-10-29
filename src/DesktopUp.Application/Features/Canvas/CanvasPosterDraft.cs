using DesktopUp.Domain;

namespace DesktopUp.Application;

/// <summary>Explicit, read-only conversion of the selected poster, not an empty-work preset.
/// Uses the same resolved paint as the native host; never changes the persisted source.</summary>
public static class CanvasPosterDraft
{
    public static CanvasDesign Read(PosterSurfaceProjection target)
    {
        var poster = target.Poster;
        var presentation = target.Presentation;
        if (presentation.Parameters.Canvas?.Current is { } saved)
            return CanvasSurfaceGeometry.ForEditing(saved, poster.WidthDip, poster.ExpandedHeightDip);

        var paint = ScenePresentationAdapter.Resolve(presentation);
        // A native scene is not a bitmap or a set of inferred nodes. Refuse lossy conversion.
        if (paint.Capability != TemplateParameterSchemas.BasicCapability || presentation.IsContentHeadingVisible)
            throw new ArgumentException("当前模板尚无无损画板转换器；原海报保持不变。可另选已有作品或空白创作，不会用空白替换当前外观。");

        double width = poster.WidthDip, height = poster.ExpandedHeightDip;
        if (width is < 240 or > 2048 || height is < 240 or > 2048)
            throw new ArgumentException("当前外框超出画板编辑的240～2048 DIP范围；未自动放大、缩小或替换原海报。");
        var design = CanvasSurfaceGeometry.ForEditing(CanvasAuthoring.Blank(poster.Title), width, height);
        var ink = presentation.Parameters.TitleColors ?? new();
        double left = PosterPresentationPolicy.HorizontalPaddingDip;
        double top = presentation.HeaderHeightDip + PosterContentMetrics.ItemsTopInsetDip;
        var background = new CanvasNode(CanvasAuthoring.NewId(), "root", "draw.panel@1", new(),
            new Dictionary<string, CanvasValue> { ["fill"] = new(Text: paint.Surface.Background),
                ["opacity"] = new(Number: paint.Surface.BackgroundAlpha / 255d), ["radius"] = new(Number: 0) }, Name: "海报背景");
        var nodes = design.Recipe.Nodes.Select(n => n.Type switch
        {
            "content.slot@1" => n with { Box = new(left / width, top / height, (width - left * 2) / width,
                (height - top - PosterContentMetrics.FooterHeightDip) / height) },
            "host.title@1" => n with { Box = new(8 / width, 10 / height, (width - 14) / width, 28 / height),
                Properties = new Dictionary<string, CanvasValue> { ["text"] = new(Text: presentation.Parameters.IsTitleVisible ? poster.Title : ""),
                    ["color"] = new(Text: ink.Text), ["actions"] = new(Text: ink.Actions), ["fontSize"] = new(Number: 14) } },
            _ => n
        }).ToList();
        nodes.Insert(1, background);
        return CanvasDeclaration.Freeze(design with { Recipe = design.Recipe with { Nodes = nodes,
            ContentForeground = presentation.EffectiveContentForeground, Surface = new("rounded", 8) } });
    }
}
