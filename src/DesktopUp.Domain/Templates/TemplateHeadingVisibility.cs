using System.Collections.ObjectModel;

namespace DesktopUp.Domain;

/// <summary>Per-poster, per-template overrides; absence inherits the recipe's heading default.</summary>
public sealed record TemplateHeadingVisibility
{
    public IReadOnlyDictionary<string, bool> Values { get; }
    public TemplateHeadingVisibility(IEnumerable<KeyValuePair<string, bool>> values)
    {
        var copy = values.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        if (copy.Count > TemplateRecipeCatalog.MaximumTemplates || copy.Keys.Any(id =>
            id.Length is < 1 or > 48 || id[0] is < 'a' or > 'z' ||
            id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '-')) ||
            id is "constructor" or "prototype" or "__proto__"))
            throw new ArgumentException("Invalid template heading identity or count.");
        Values = new ReadOnlyDictionary<string, bool>(copy);
    }
    public bool Resolve(string id, bool fallback) => Values.GetValueOrDefault(id, fallback);
    public TemplateHeadingVisibility Set(string id, bool visible)
    {
        var copy = Values.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        copy[id] = visible; return new(copy);
    }
    public bool Equals(TemplateHeadingVisibility? other) => other is not null && Values.Count == other.Values.Count &&
        Values.All(p => other.Values.TryGetValue(p.Key, out var value) && value == p.Value);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var p in Values.OrderBy(p => p.Key, StringComparer.Ordinal)) { hash.Add(p.Key); hash.Add(p.Value); }
        return hash.ToHashCode();
    }
}
