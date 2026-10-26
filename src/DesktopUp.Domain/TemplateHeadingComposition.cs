using System.Text.Json;

namespace DesktopUp.Domain;

public enum TemplateHeadingComponent { Subtitle, Title, Count }

// A bounded declaration for existing text components, not arbitrary XAML or a second layout engine.
public static class TemplateHeadingComposition
{
    public static IReadOnlyList<TemplateHeadingComponent> Legacy { get; } =
        Array.AsReadOnly(new[] { TemplateHeadingComponent.Subtitle, TemplateHeadingComponent.Title, TemplateHeadingComponent.Count });

    internal static IReadOnlyList<TemplateHeadingComponent> Parse(JsonElement source)
    {
        if (source.ValueKind != JsonValueKind.Array || source.GetArrayLength() > 3)
            throw new ArgumentException("组件声明必须是至多三项的列表。");
        return Array.AsReadOnly(source.EnumerateArray().Select(value => value.ValueKind == JsonValueKind.String
            ? value.GetString() switch
            {
                "subtitle" => TemplateHeadingComponent.Subtitle, "title" => TemplateHeadingComponent.Title,
                "count" => TemplateHeadingComponent.Count, _ => throw new ArgumentException("未知的内容介绍组件。")
            } : throw new ArgumentException("组件必须使用受支持的名称。" )).ToArray());
    }

    internal static IReadOnlyList<TemplateHeadingComponent>? Freeze(TemplateRecipe recipe)
    {
        if (recipe.Schema.SchemaVersion == 1)
        {
            if (recipe.Components is not null) throw new ArgumentException("独立组件需要模板配方v2。");
            return null;
        }
        if (recipe.Schema.SchemaVersion != 2 || recipe.Schema.RendererCapability != TemplateParameterSchemas.BasicCapability ||
            recipe.Components is not { Count: <= 3 } components || components.Any(c => !Enum.IsDefined(c)) ||
            components.Distinct().Count() != components.Count || recipe.Base.Heading != (components.Count > 0))
            throw new ArgumentException("组件重复、版本不受支持或与heading声明不一致；v2目前只支持基础表面。");
        return Array.AsReadOnly(components.ToArray());
    }

    public static double Height(IReadOnlyList<TemplateHeadingComponent> components, double titleFontSizeDip)
    {
        if (components.Count == 0) return 0;
        // Legacy complete heading = 81 + title*1.35. Omitted rows reclaim their height and gap.
        return Math.Ceiling(39 + (components.Count - 1) * 5 + components.Sum(c => c switch
        {
            TemplateHeadingComponent.Subtitle => 18, TemplateHeadingComponent.Title => titleFontSizeDip * 1.35,
            TemplateHeadingComponent.Count => 14, _ => throw new ArgumentException("Unknown heading component.")
        }));
    }
}
